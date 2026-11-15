using System.Buffers;
using System.Security.Cryptography;
using Crecisol.Api.Configuration;
using Crecisol.Api.Contracts;
using Crecisol.Application.Identity;
using Crecisol.Application.Tenancy;
using Crecisol.CredentialSecurity;
using Crecisol.Domain.Tenancy;
using Crecisol.Infrastructure.Identity;
using MySqlConnector;
using Microsoft.Extensions.Hosting;

namespace Crecisol.Api.Modules.Identity;

/// <summary>
/// Isolated local registration completion. It is explicitly restricted to a
/// loopback disposable database and example.test hosts; no production email or
/// password-screen provider is implied by this local substitute.
/// </summary>
public static class OperationalRegistrationCompletionModule
{
    public const string RoutePattern = "/api/v1/identity/registration-completions";

    public static IServiceCollection AddLocalRegistrationCompletion(
        this IServiceCollection services, string? trustedDsn,
        IReadOnlyCollection<string> allowedHosts,
        Func<string, string?>? readSetting = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        readSetting ??= Environment.GetEnvironmentVariable;
        if (string.IsNullOrWhiteSpace(trustedDsn))
            throw new InvalidOperationException("Local account completion requires isolated test hosts and database.");
        var options = new MySqlConnectionStringBuilder(trustedDsn);
        if (options.Server is not ("127.0.0.1" or "localhost" or "::1") ||
            options.Database != "crecisol_c05" || options.AllowLoadLocalInfile ||
            allowedHosts.Count is < 1 or > 16 ||
            allowedHosts.Any(host => !host.EndsWith(".example.test", StringComparison.Ordinal)))
            throw new InvalidOperationException("Local account completion requires isolated test hosts and database.");

        var mailboxKey = ReadKey(readSetting, "CRECISOL_C05_MAILBOX_HMAC_KEY");
        var peerKey = ReadKey(readSetting, "CRECISOL_C05_PEER_HMAC_KEY");
        var loginKey = ReadKey(readSetting, "CRECISOL_C05_LOGIN_LOOKUP_HMAC_KEY");
        var mailboxEncryptionKey = ReadKey(readSetting, "CRECISOL_C05_MAILBOX_ENCRYPTION_KEY");
        var outboxEncryptionKey = ReadKey(readSetting, "CRECISOL_C05_PROOF_OUTBOX_ENCRYPTION_KEY");
        var keyVersionText = readSetting("CRECISOL_C05_KEY_VERSION");
        if (!int.TryParse(keyVersionText, out var keyVersion) || keyVersion < 1 ||
            !int.TryParse(readSetting("CRECISOL_C05_LOCAL_SMTP_PORT"), out var smtpPort) ||
            smtpPort is < 1024 or > 65535)
            throw new InvalidOperationException("Local proof dispatch settings are invalid.");
        services.AddSingleton<ITrustedCompromisedPasswordScreen,
            LocalTestCompromisedPasswordScreen>();
        services.AddSingleton(sp => new Argon2idPasswordHasher(
            PasswordHashingPolicy.Default,
            sp.GetRequiredService<ITrustedCompromisedPasswordScreen>()));
        var accountCompletion = new MySqlOperationalRegistrationAccountCompletion(
            trustedDsn, mailboxKey, peerKey, loginKey);
        services.AddSingleton(accountCompletion);
        services.AddSingleton<MySqlLocalProofOutboxDispatcher>(sp =>
        {
            try
            {
                return new MySqlLocalProofOutboxDispatcher(
                    trustedDsn, keyVersion, mailboxEncryptionKey, outboxEncryptionKey,
                    smtpPort, sp.GetRequiredService<ILogger<MySqlLocalProofOutboxDispatcher>>());
            }
            finally
            {
                CryptographicOperations.ZeroMemory(mailboxEncryptionKey);
                CryptographicOperations.ZeroMemory(outboxEncryptionKey);
            }
        });
        services.AddHostedService<LocalProofOutboxHostedService>();
        CryptographicOperations.ZeroMemory(mailboxKey);
        CryptographicOperations.ZeroMemory(peerKey);
        CryptographicOperations.ZeroMemory(loginKey);
        return services;
    }

    public static RouteHandlerBuilder MapOperationalRegistrationCompletion(
        this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        _ = endpoints.ServiceProvider.GetRequiredService<
            MySqlOperationalRegistrationAccountCompletion>();
        _ = endpoints.ServiceProvider.GetRequiredService<Argon2idPasswordHasher>();

        return endpoints.MapPost(RoutePattern, async (
            HttpContext context,
            HttpTrustedRegistrationRequest directRequest,
            ITrustedRegistrationNetworkPeer network,
            ResolveVerifiedTenantHost hostResolver,
            ITrustedTenantCatalogReader tenants,
            MySqlOperationalRegistrationAccountCompletion completion,
            Argon2idPasswordHasher hasher,
            ILoggerFactory loggerFactory,
            CancellationToken ct) =>
        {
            SetPrivateHeaders(context.Response);
            var preflightEligible = NeutralRegistrationTransportPreflight.IsEligible(context.Request);
            if (context.Request.ContentLength is null or <= 0 or
                    > OperationalRegistrationCompletionPayload.MaxBytes || !preflightEligible)
            {
                loggerFactory.CreateLogger("Crecisol.C05.RegistrationCompletion").LogInformation(
                    "C05 local registration completion preflight declined (Length={Length}, Eligible={Eligible}).",
                    context.Request.ContentLength, preflightEligible);
                return Results.Accepted(value: new { accepted = true });
            }

            byte[]? rented = null;
            byte[]? code = null;
            try
            {
                var length = checked((int)context.Request.ContentLength.Value);
                rented = ArrayPool<byte>.Shared.Rent(length);
                var total = 0;
                while (total < length)
                {
                    var read = await context.Request.Body.ReadAsync(
                        rented.AsMemory(total, length - total), ct);
                    if (read == 0) return Results.Accepted(value: new { accepted = true });
                    total += read;
                }
                if (!OperationalRegistrationCompletionPayload.TryParse(
                        rented.AsMemory(0, total), out var payload) || payload is null ||
                    !payload.Mailbox.EndsWith("@example.test", StringComparison.Ordinal))
                    return Results.Accepted(value: new { accepted = true });

                var directHost = await directRequest.ReadCurrentDirectHostAsync(ct);
                if (directHost is null) return Results.Accepted(value: new { accepted = true });
                var verified = await hostResolver.ResolveAsync(directHost, ct);
                var tenant = await tenants.FindByIdAsync(verified.TenantId, ct);
                var peer = await network.ReadVerifiedPeerAddressAsync(ct);
                var now = DateTimeOffset.UtcNow;
                if (tenant is null || tenant.TenantId != verified.TenantId ||
                    tenant.State != TenantLifecycleState.Active || tenant.UpdatedAtUtc > now ||
                    peer is null || peer.Value.Length != 16)
                    return Results.Accepted(value: new { accepted = true });

                if (!await completion.TryReserveBeforeHashAsync(
                        verified.TenantId, peer.Value, now, ct))
                    return Results.Accepted(value: new { accepted = true });

                code = Convert.FromHexString(payload.Code);
                var password = payload.Password.ToCharArray();
                try
                {
                    var phc = hasher.CreateVerifier(password).ExportForTrustedCredentialStore();
                    var outcome = await completion.CompletePendingAsync(
                        verified.TenantId, payload.ProofId, code,
                        payload.Mailbox, phc, now, ct);
                    loggerFactory.CreateLogger("Crecisol.C05.RegistrationCompletion").LogInformation(
                        "C05 local registration completion outcome: {Outcome}.", outcome);
                }
                finally { Array.Clear(password); }
                return Results.Accepted(value: new { accepted = true });
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                loggerFactory.CreateLogger("Crecisol.C05.RegistrationCompletion").LogWarning(
                    "C05 local registration completion was declined ({ErrorType}).",
                    exception.GetType().Name);
                // Same public receipt for invalid proof, duplicate identity,
                // password rejection, inactive tenant or unavailable storage.
                return Results.Accepted(value: new { accepted = true });
            }
            finally
            {
                if (code is not null) CryptographicOperations.ZeroMemory(code);
                if (rented is not null)
                {
                    CryptographicOperations.ZeroMemory(rented);
                    ArrayPool<byte>.Shared.Return(rented);
                }
            }
        })
        .AllowAnonymous()
        .WithName("CompleteNeutralMailboxRegistrationAsPendingAccount");
    }

    private static byte[] ReadKey(Func<string, string?> readSetting, string name)
    {
        var value = readSetting(name);
        try
        {
            var bytes = string.IsNullOrWhiteSpace(value) ? null : Convert.FromHexString(value);
            if (bytes is null || bytes.Length != 32)
                throw new InvalidOperationException("C05 cryptographic configuration is invalid.");
            return bytes;
        }
        catch (FormatException)
        {
            throw new InvalidOperationException("C05 cryptographic configuration is invalid.");
        }
    }

    private static void SetPrivateHeaders(HttpResponse response)
    {
        response.Headers.CacheControl = "no-store";
        response.Headers.Pragma = "no-cache";
        response.Headers["X-Content-Type-Options"] = "nosniff";
        response.Headers["Referrer-Policy"] = "no-referrer";
    }
}

internal sealed class LocalProofOutboxHostedService(
    MySqlLocalProofOutboxDispatcher dispatcher) : BackgroundService
{
    protected override Task ExecuteAsync(CancellationToken stoppingToken) =>
        dispatcher.RunAsync(stoppingToken);
}

internal sealed class LocalTestCompromisedPasswordScreen : ITrustedCompromisedPasswordScreen
{
    private static readonly HashSet<string> CommonCompromised = new(
        ["password123456", "123456789012345", "qwerty123456789", "letmein12345678"],
        StringComparer.OrdinalIgnoreCase);

    public PasswordCompromiseVerdict Check(ReadOnlySpan<char> candidate)
    {
        if (candidate.Length < PasswordHashingPolicy.MinAcceptedRunes)
            return PasswordCompromiseVerdict.Compromised;
        return CommonCompromised.Contains(candidate.ToString())
            ? PasswordCompromiseVerdict.Compromised
            : PasswordCompromiseVerdict.Allowed;
    }

    public override string ToString() => "LocalTestCompromisedPasswordScreen[redacted]";
}
