using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using Crecisol.Application.Identity;
using Crecisol.Domain.Identity;
using Crecisol.Domain.Tenancy;

namespace Crecisol.Infrastructure.Identity;

/// <summary>
/// C05-10 TEST-ONLY domain-separated 256-bit HMAC. A tenant never inherits
/// another tenant's source/subject budget. No raw IP/email in MySQL or logs.
/// No hardcoded secret: runtime-provided random material is zeroed on dispose.
/// </summary>
public sealed class SyntheticPreAuthDigestProtector :
    ITrustedPreAuthDigestProtector,IDisposable
{
    private readonly byte[] key;
    private bool disposed;

    public SyntheticPreAuthDigestProtector(ReadOnlySpan<byte> keyMaterial)
    {
        if(keyMaterial.Length!=32 || keyMaterial.IndexOfAnyExcept((byte)0)<0)
            throw new InvalidOperationException("A fresh 256-bit HMAC key is required.");
        key=keyMaterial.ToArray();
    }

    private PreAuthKeyedDigest Compute(TenantId tenant,
        PreAuthRateLimitPurpose purpose,string domain,ReadOnlySpan<byte> payload)
    {
        ObjectDisposedException.ThrowIf(disposed,this);
        ArgumentNullException.ThrowIfNull(tenant);
        if(!Enum.IsDefined(purpose) || domain is not ("SUBJECT" or "SOURCE"))
            throw new InvalidOperationException("Untrusted HMAC scope.");
        byte[] label=Encoding.ASCII.GetBytes("CRECISOL.C05-10."+domain);
        byte[] buffer=new byte[label.Length+1+16+1+payload.Length];
        try
        {
            label.CopyTo(buffer,0);
            // Exact domain separator prevents suffix/prefix reuse.
            buffer[label.Length]=0;
            if(!tenant.Value.TryWriteBytes(buffer.AsSpan(label.Length+1,16),
                bigEndian:true,out int written) || written!=16)
                throw new InvalidOperationException("Invalid tenant digest scope.");
            buffer[label.Length+17]=(byte)purpose;
            payload.CopyTo(buffer.AsSpan(label.Length+18));
            byte[] digest=HMACSHA256.HashData(key,buffer);
            try { return PreAuthKeyedDigest.FromTrustedProtector(digest); }
            finally {CryptographicOperations.ZeroMemory(digest);}
        }
        finally
        {
            CryptographicOperations.ZeroMemory(buffer);
            CryptographicOperations.ZeroMemory(label);
        }
    }

    public PreAuthKeyedDigest FingerprintSubject(TenantId tenant,
        PreAuthRateLimitPurpose purpose,CanonicalLoginHandle login)
    {
        ArgumentNullException.ThrowIfNull(login);
        byte[] encoded=Encoding.ASCII.GetBytes(login.Value);
        try{return Compute(tenant,purpose,"SUBJECT",encoded);}
        finally {CryptographicOperations.ZeroMemory(encoded);}
    }

    public PreAuthKeyedDigest FingerprintSocketPeer(TenantId tenant,
        PreAuthRateLimitPurpose purpose,IPAddress actualRemoteIp)
    {
        ArgumentNullException.ThrowIfNull(actualRemoteIp);
        IPAddress normalized=actualRemoteIp.IsIPv4MappedToIPv6
            ?actualRemoteIp.MapToIPv4():actualRemoteIp;
        if(normalized.Equals(IPAddress.Any) ||
           normalized.Equals(IPAddress.None) ||
           normalized.Equals(IPAddress.Broadcast) ||
           normalized.Equals(IPAddress.IPv6Any) ||
           normalized.Equals(IPAddress.IPv6None) ||
           normalized.IsIPv6Multicast ||
           normalized.AddressFamily is not
               (AddressFamily.InterNetwork or AddressFamily.InterNetworkV6))
            throw new InvalidOperationException("Unusable server socket origin.");
        byte[] address=normalized.GetAddressBytes();
        try
        {
            // IPv6 /64 prevents trivial per-request suffix rotation from
            // multiplying anonymous source budgets, without storing raw IP.
            if(address.Length==16)
                address.AsSpan(8,8).Clear();
            return Compute(tenant,purpose,"SOURCE",address);
        }
        finally {CryptographicOperations.ZeroMemory(address);}
    }

    public void Dispose()
    {
        if(disposed)return;
        CryptographicOperations.ZeroMemory(key);
        disposed=true;
        GC.SuppressFinalize(this);
    }

    public override string ToString() => "SyntheticPreAuthDigestProtector[redacted]";
}
