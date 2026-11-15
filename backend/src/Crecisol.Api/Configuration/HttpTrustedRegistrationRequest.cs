using Crecisol.Api.Configuration;
using Crecisol.Application.Identity;
using Crecisol.Application.Tenancy;
using Crecisol.Domain.Tenancy;
using Microsoft.AspNetCore.Http;

namespace Crecisol.Api.Configuration;

/// <summary>
/// Reads only the direct server socket request. Forwarded headers are rejected
/// by StrictVerifiedHostTransport; the network peer never comes from XFF.
/// </summary>
public sealed class HttpTrustedRegistrationRequest(IHttpContextAccessor accessor) :
    ITrustedCurrentDirectHostReader, ITrustedRegistrationNetworkPeer
{
    public ValueTask<CanonicalTenantHost?> ReadCurrentDirectHostAsync(
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var request = accessor.HttpContext?.Request;
        if (request is null || !StrictVerifiedHostTransport.TryReadDirectHost(request, out var host))
            return ValueTask.FromResult<CanonicalTenantHost?>(null);
        return ValueTask.FromResult(host);
    }

    public ValueTask<ReadOnlyMemory<byte>?> ReadVerifiedPeerAddressAsync(
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var address = accessor.HttpContext?.Connection.RemoteIpAddress;
        if (address is null) return ValueTask.FromResult<ReadOnlyMemory<byte>?>(null);
        return ValueTask.FromResult<ReadOnlyMemory<byte>?>(address.MapToIPv6().GetAddressBytes());
    }
}

