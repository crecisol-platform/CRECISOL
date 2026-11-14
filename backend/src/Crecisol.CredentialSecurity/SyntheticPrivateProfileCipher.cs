using System.Security.Cryptography;
using System.Text;
using Crecisol.Application.Identity;
using Crecisol.Domain.Identity;
using Crecisol.Domain.Tenancy;

namespace Crecisol.CredentialSecurity;

/// <summary>
/// C05-09 isolated TEST envelope. Independently supplied 256-bit AES-GCM key;
/// random nonce per update and AAD scoped to exact tenant + actor. No static
/// embedded secrets, no KMS, no actual PII, no production configuration.
/// </summary>
public sealed class SyntheticPrivateProfileCipher : ITrustedPrivateProfileCipher, IDisposable
{
    private readonly byte[] key;
    private bool disposed;
    private static readonly UTF8Encoding StrictUtf8=new(false,true);

    public SyntheticPrivateProfileCipher(ReadOnlySpan<byte> keyMaterial)
    {
        if(keyMaterial.Length!=32 ||
           keyMaterial.IndexOfAnyExcept((byte)0)<0)
            throw new InvalidOperationException("Fresh 256-bit profile key required.");
        key=keyMaterial.ToArray();
    }

    private static byte[] AssociatedData(TenantId tenant,TenantActorId actor)
    {
        ArgumentNullException.ThrowIfNull(tenant);
        ArgumentNullException.ThrowIfNull(actor);
        var aad=new byte[14+16+16];
        Encoding.ASCII.GetBytes("CRECISOL.C05-09",aad);
        tenant.Value.TryWriteBytes(aad.AsSpan(14,16),bigEndian:true,out _);
        actor.Value.TryWriteBytes(aad.AsSpan(30,16),bigEndian:true,out _);
        return aad;
    }

    public byte[] EncryptForTenantActor(TenantId tenant,TenantActorId actor,
        PrivateDisplayName text)
    {
        ObjectDisposedException.ThrowIf(disposed,this);
        ArgumentNullException.ThrowIfNull(text);
        byte[] clear=StrictUtf8.GetBytes(text.Value);
        byte[] aad=AssociatedData(tenant,actor);
        byte[] nonce=RandomNumberGenerator.GetBytes(12);
        byte[] tag=new byte[16];
        byte[] body=new byte[clear.Length];
        try
        {
            using var gcm=new AesGcm(key,16);
            gcm.Encrypt(nonce,clear,body,tag,aad);
            var result=new byte[1+12+16+body.Length];
            result[0]=1;
            nonce.CopyTo(result,1);
            tag.CopyTo(result,13);
            body.CopyTo(result,29);
            return result;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(clear);
            CryptographicOperations.ZeroMemory(aad);
            CryptographicOperations.ZeroMemory(nonce);
            CryptographicOperations.ZeroMemory(tag);
            CryptographicOperations.ZeroMemory(body);
        }
    }

    public PrivateDisplayName DecryptForTenantActor(TenantId tenant,
        TenantActorId actor,ReadOnlySpan<byte> envelope)
    {
        ObjectDisposedException.ThrowIf(disposed,this);
        if(envelope.Length is < 31 or > 512 || envelope[0]!=1)
            throw new CryptographicException("Protected profile envelope invalid.");
        byte[] aad=AssociatedData(tenant,actor);
        byte[] clear=new byte[envelope.Length-29];
        try
        {
            using var gcm=new AesGcm(key,16);
            gcm.Decrypt(envelope.Slice(1,12),envelope.Slice(29),
                envelope.Slice(13,16),clear,aad);
            return PrivateDisplayName.Parse(StrictUtf8.GetString(clear));
        }
        finally
        {
            CryptographicOperations.ZeroMemory(aad);
            CryptographicOperations.ZeroMemory(clear);
        }
    }

    public void Dispose()
    {
        if(disposed)return;
        CryptographicOperations.ZeroMemory(key);
        disposed=true;
        GC.SuppressFinalize(this);
    }
    public override string ToString() => "SyntheticPrivateProfileCipher[redacted]";
}
