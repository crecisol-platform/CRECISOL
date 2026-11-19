using Crecisol.Application.Identity;

namespace Crecisol.Infrastructure.Identity;

/// <summary>Server-authoritative, centrally scoped session inactivity policy.</summary>
public sealed partial class MySqlOperationalIdentityFlow
{
    private const int MinSessionIdleMinutes = 5;
    private const int MaxSessionIdleMinutes = 120;

    private async Task<int> SessionIdleMinutes(Unit u, bool exclusive = false)
    {
        if (u.IdleMinutes is { } cached) return cached;
        var row = await u.One(exclusive
            ? "SELECT idle_minutes,revision FROM c05_session_idle_policy WHERE policy_id=1 FOR UPDATE"
            : "SELECT idle_minutes,revision FROM c05_session_idle_policy WHERE policy_id=1 FOR SHARE");
        if (row is null) throw new InvalidOperationException("Session inactivity policy unavailable.");
        var idle = checked((int)Number(row, "idle_minutes"));
        if (idle is < MinSessionIdleMinutes or > MaxSessionIdleMinutes)
            throw new InvalidOperationException("Invalid session inactivity policy.");
        u.IdleMinutes = idle;
        u.PolicyRevision = Number(row, "revision");
        return idle;
    }

    private static async Task SlideTenantSession(Unit u, byte[] tenant, byte[] hash, int minutes)
    {
        if (await u.Run("""
            UPDATE c05_identity_session
            SET last_seen_at_utc=UTC_TIMESTAMP(6),
                expires_at_utc=DATE_ADD(UTC_TIMESTAMP(6),INTERVAL @idle MINUTE)
            WHERE tenant_id=@t AND token_hmac=@h AND revoked_at_utc IS NULL
              AND expires_at_utc>UTC_TIMESTAMP(6)
              AND last_seen_at_utc>UTC_TIMESTAMP(6)-INTERVAL @idle MINUTE
            """, ("t", tenant), ("h", hash), ("idle", minutes)) != 1)
            throw new InvalidOperationException("Session renewal declined.");
        var row = await u.One("SELECT expires_at_utc FROM c05_identity_session WHERE tenant_id=@t AND token_hmac=@h",
            ("t", tenant), ("h", hash));
        if (row is null) throw new InvalidOperationException("Session renewal unavailable.");
        u.SessionExpiresAtUtc = Utc(row, "expires_at_utc");
    }

    private static async Task SlidePlatformSession(Unit u, byte[] hash, int minutes)
    {
        if (await u.Run("""
            UPDATE c05_platform_session
            SET last_seen_at_utc=UTC_TIMESTAMP(6),
                expires_at_utc=DATE_ADD(UTC_TIMESTAMP(6),INTERVAL @idle MINUTE)
            WHERE token_hmac=@h AND revoked_at_utc IS NULL
              AND expires_at_utc>UTC_TIMESTAMP(6)
              AND last_seen_at_utc>UTC_TIMESTAMP(6)-INTERVAL @idle MINUTE
            """, ("h", hash), ("idle", minutes)) != 1)
            throw new InvalidOperationException("Operator session renewal declined.");
        var row = await u.One("SELECT expires_at_utc FROM c05_platform_session WHERE token_hmac=@h", ("h", hash));
        if (row is null) throw new InvalidOperationException("Operator session renewal unavailable.");
        u.SessionExpiresAtUtc = Utc(row, "expires_at_utc");
    }

    private async Task<OperationalIdentityResult> PlatformSessionPolicy(Unit u,
        Dictionary<string, object> operatorPrincipal, byte[] tokenHash,
        OperationalIdentityRequest request, CancellationToken ct)
    {
        var permitted = Number(operatorPrincipal, "can_manage_session_policy") == 1;
        var minutes = await SessionIdleMinutes(u, request.Operation == "platform/session-policy/update");
        var revision = u.PolicyRevision!.Value;
        if (request.Operation == "platform/session-policy")
            return new(true, new { idleMinutes = minutes, revision, canManage = permitted });
        if (request.Operation != "platform/session-policy/update" || !permitted ||
            !int.TryParse(Field(request, "minutes"), out var requested) ||
            requested is < MinSessionIdleMinutes or > MaxSessionIdleMinutes ||
            !long.TryParse(Field(request, "expectedRevision"), out var expected) ||
            expected != revision ||
            !await PlatformQuota("session-policy:" + Hex(u.Actor!), 5, ct) ||
            !await PlatformTotp(u, operatorPrincipal, Field(request, "code")))
            return new(false);
        if (await u.Run("""
            UPDATE c05_session_idle_policy
            SET idle_minutes=@idle,revision=revision+1,changed_at_utc=UTC_TIMESTAMP(6),changed_by=@admin
            WHERE policy_id=1 AND revision=@revision
            """, ("idle", requested), ("admin", u.Actor!), ("revision", revision)) != 1)
            return new(false);
        // A shorter policy invalidates stale sessions now. Increasing the
        // limit later cannot resurrect any of them, including other tenants.
        await u.Run("""
            UPDATE c05_identity_session SET revoked_at_utc=UTC_TIMESTAMP(6)
            WHERE revoked_at_utc IS NULL AND
                  (expires_at_utc<=UTC_TIMESTAMP(6) OR
                   last_seen_at_utc<=UTC_TIMESTAMP(6)-INTERVAL @idle MINUTE)
            """, ("idle", requested));
        await u.Run("""
            UPDATE c05_platform_session SET revoked_at_utc=UTC_TIMESTAMP(6)
            WHERE revoked_at_utc IS NULL AND
                  (expires_at_utc<=UTC_TIMESTAMP(6) OR
                   last_seen_at_utc<=UTC_TIMESTAMP(6)-INTERVAL @idle MINUTE)
            """, ("idle", requested));
        u.IdleMinutes = requested;
        u.PolicyRevision = revision + 1;
        await SlidePlatformSession(u, tokenHash, requested);
        return new(true, new { idleMinutes = requested, revision = revision + 1, canManage = true });
    }
}
