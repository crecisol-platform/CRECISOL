using System.Net;
using Crecisol.Api.Contracts;
using Crecisol.Application.Identity;
using Crecisol.Application.Tenancy;
using Crecisol.Domain.Tenancy;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Crecisol.NeutralRegistration.IntegrationTests;

/// <summary>
/// Only the disposable integration executable maps this route. The trusted
/// host fixtures are server-owned; no HTTP header can set tenant/revision.
/// The exact same application+MySQL authorization runs behind real Kestrel.
/// </summary>
internal sealed class LocalMfaPrivateHttpHarness : IAsyncDisposable
{
    private const string HostB = "tenant-b.fixture.test";
    private const string HostA = "tenant-a.fixture.test";
    private const string Route = "/__test_only/c05-integration/private/cases/";
    private readonly WebApplication app;
    private readonly HttpClient client;
    private readonly VerifiedTenantHostHint trustedB;
    private readonly VerifiedTenantHostHint trustedA;

    private LocalMfaPrivateHttpHarness(
        WebApplication app, HttpClient client,
        VerifiedTenantHostHint trustedB, VerifiedTenantHostHint trustedA)
    {
        this.app = app;
        this.client = client;
        this.trustedB = trustedB;
        this.trustedA = trustedA;
    }

    public static async Task<LocalMfaPrivateHttpHarness> StartAsync(
        ReadMfaAuthorizedPrivateResource reader,
        VerifiedTenantHostHint trustedB,
        VerifiedTenantHostHint trustedA,
        CancellationToken cancellationToken)
    {
        var builder = WebApplication.CreateBuilder();
        builder.Logging.ClearProviders(); // No sensitive bearer or resource logs.
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        var app = builder.Build();
        var endpoint = new PrivateResourceHttpRead(
            new FixedTestHostCatalog(trustedB, trustedA), reader);
        app.MapGet(Route + "{resourceId}",
            (HttpContext ctx, string resourceId) =>
                endpoint.ExecuteAsync(ctx, resourceId, ctx.RequestAborted));
        await app.StartAsync(cancellationToken);
        var url = app.Urls.Single(value =>
            value.StartsWith("http://127.0.0.1:", StringComparison.Ordinal));
        return new LocalMfaPrivateHttpHarness(
            app, new HttpClient { BaseAddress = new Uri(url) }, trustedB, trustedA);
    }

    public async Task<bool> AllowedAsync(
        string bearer, VerifiedTenantHostHint host, TenantResourceId resource,
        CancellationToken cancellationToken)
    {
        using var response = await RequestAsync(
            bearer, HostFor(host), resource.Value.ToString("D"), null, null,
            cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        return response.StatusCode == HttpStatusCode.OK &&
            body.Contains("\"externalKey\":\"synthetic-b-owner-case\"",
                StringComparison.Ordinal) &&
            !body.Contains(bearer, StringComparison.Ordinal) &&
            NoStore(response);
    }

    public async Task<bool> DeniedAsync(
        string? bearer, VerifiedTenantHostHint host, TenantResourceId resource,
        CancellationToken cancellationToken)
    {
        using var response = await RequestAsync(
            bearer, HostFor(host), resource.Value.ToString("D"), null, null,
            cancellationToken);
        return await IsNotFoundAsync(response, cancellationToken);
    }

    public async Task VerifyAdversarialAsync(
        string bearer, TenantResourceId own, TenantResourceId foreignOwner,
        TenantResourceId foreignBranch, TenantResourceId foreignTenant,
        Action<bool, string> pass, CancellationToken ct)
    {
        pass(await AllowedAsync(bearer, trustedB, own, ct),
            "Real loopback HTTP GET + MySQL permits only owned B1 resource after MFA");
        pass(await DeniedAsync(bearer, trustedA, own, ct) &&
             await DeniedAsync(bearer, trustedB, foreignOwner, ct) &&
             await DeniedAsync(bearer, trustedB, foreignBranch, ct) &&
             await DeniedAsync(bearer, trustedB, foreignTenant, ct),
            "HTTP denies foreign tenant, actor owner, branch and resource identifiers");
        pass(await DeniedAsync(null, trustedB, own, ct) &&
             await DeniedAsync("cs5_" + new string('Z', 43), trustedB, own, ct),
            "HTTP denies absent or forged MFA-bound bearer without resource disclosure");

        foreach (var header in new[]
        {
            "X-Tenant-Id", "X-Actor-Id", "X-Branch-Id", "X-Role",
            "X-Action", "X-Forwarded-Host", "X-Forwarded-For",
            "X-Original-Host", "X-Real-IP", "Forwarded", "Cookie"
        })
        {
            using var response = await RequestAsync(
                bearer, HostB, own.Value.ToString("D"), header,
                header == "Cookie" ? "session=fake" : "tenant-a.fixture.test", ct);
            pass(await IsNotFoundAsync(response, ct),
                "HTTP fails closed on untrusted header " + header);
        }

        using (var unknownHost = await RequestAsync(
            bearer, "unlisted.fixture.test", own.Value.ToString("D"),
            null, null, ct))
            pass(await IsNotFoundAsync(unknownHost, ct),
                "Unknown direct Host cannot select any tenant in the backend catalog");
        using (var query = await RequestAsync(
            bearer, HostB, own.Value.ToString("D") + "?tenant=A",
            null, null, ct))
            pass(await IsNotFoundAsync(query, ct),
                "Query tenant selector is rejected before the protected SQL read");
        using (var badId = await RequestAsync(
            bearer, HostB, "not-a-uuid", null, null, ct))
            pass(await IsNotFoundAsync(badId, ct),
                "Malformed opaque resource selector is denied");
        using (var duplicateBearer = await RequestAsync(
            bearer, HostB, own.Value.ToString("D"), "Authorization",
            "Bearer " + bearer + ", Bearer " + bearer, ct))
            pass(await IsNotFoundAsync(duplicateBearer, ct),
                "Ambiguous Authorization header cannot bypass bearer parser");
    }

    private async Task<HttpResponseMessage> RequestAsync(
        string? bearer, string host, string routeId,
        string? forgedHeader, string? forgedValue, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, Route + routeId);
        request.Headers.Host = host;
        if (bearer is not null)
            request.Headers.TryAddWithoutValidation("Authorization", "Bearer " + bearer);
        if (forgedHeader is not null && forgedValue is not null)
        {
            request.Headers.Remove(forgedHeader);
            request.Headers.TryAddWithoutValidation(forgedHeader, forgedValue);
        }
        return await client.SendAsync(request, ct);
    }

    private static async Task<bool> IsNotFoundAsync(
        HttpResponseMessage response, CancellationToken ct)
    {
        var body = await response.Content.ReadAsStringAsync(ct);
        return response.StatusCode == HttpStatusCode.NotFound &&
            !body.Contains("synthetic-b-owner-case", StringComparison.Ordinal) &&
            NoStore(response);
    }

    private static bool NoStore(HttpResponseMessage response) =>
        response.Headers.TryGetValues("Cache-Control", out var values) &&
        values.Any(value => value.Contains("no-store", StringComparison.Ordinal));

    private string HostFor(VerifiedTenantHostHint hint) =>
        hint.Equals(trustedB) ? HostB :
        hint.Equals(trustedA) ? HostA : "unlisted.fixture.test";

    public async ValueTask DisposeAsync()
    {
        client.Dispose();
        await app.StopAsync();
        await app.DisposeAsync();
    }

    private sealed class FixedTestHostCatalog(
        VerifiedTenantHostHint trustedB, VerifiedTenantHostHint trustedA)
        : ITrustedPrivateResourceHostCatalog
    {
        public Task<VerifiedTenantHostHint?> ResolveDirectHostAsync(
            string directHost, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            VerifiedTenantHostHint? result =
                string.Equals(directHost, HostB, StringComparison.OrdinalIgnoreCase)
                    ? trustedB :
                string.Equals(directHost, HostA, StringComparison.OrdinalIgnoreCase)
                    ? trustedA : null;
            return Task.FromResult(result);
        }
    }
}
