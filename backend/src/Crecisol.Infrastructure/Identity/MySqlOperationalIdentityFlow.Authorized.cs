using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Crecisol.Application.Identity;
using Crecisol.Domain.Authorization;
using MySqlConnector;

namespace Crecisol.Infrastructure.Identity;

/// <summary>
/// One operational schema and one transaction per security transition. Account,
/// membership, factor and session are revalidated under locks, never from claims.
/// The opt-in composition is restricted to isolated loopback/example.test use.
/// </summary>
public sealed partial class MySqlOperationalIdentityFlow
{
    private async Task<OperationalIdentityResult> Authenticated(Unit u, byte[] tenant, OperationalIdentityRequest r, CancellationToken ct)
    {
        if (r.Bearer is not { Length: 64 }) return new(false);
        var hash = Digest("session", r.Bearer);
        var peek = await u.One("SELECT account_id FROM c05_identity_session WHERE tenant_id=@t AND token_hmac=@h", ("t", tenant), ("h", hash));
        if (peek is null) return new(false);
        var actor = Bytes(peek, "account_id");
        var account = await Account(u, actor);
        var member = await Membership(u, tenant, actor);
        var idle = await SessionIdleMinutes(u);
        var session = await u.One("""
            SELECT * FROM c05_identity_session WHERE tenant_id=@t AND token_hmac=@h
            AND revoked_at_utc IS NULL AND expires_at_utc>UTC_TIMESTAMP(6)
            AND last_seen_at_utc>UTC_TIMESTAMP(6)-INTERVAL @idle MINUTE FOR UPDATE
            """, ("t", tenant), ("h", hash), ("idle", idle));
        if (session is null)
        {
            // An expired session must never revive if an administrator later
            // increases the global inactivity limit.
            await u.Run("""
                UPDATE c05_identity_session SET revoked_at_utc=UTC_TIMESTAMP(6)
                WHERE tenant_id=@t AND token_hmac=@h AND revoked_at_utc IS NULL
                  AND (expires_at_utc<=UTC_TIMESTAMP(6)
                       OR last_seen_at_utc<=UTC_TIMESTAMP(6)-INTERVAL @idle MINUTE)
                """, ("t", tenant), ("h", hash), ("idle", idle));
            return new(false);
        }
        if (account is null || member is null ||
            !Bytes(session, "membership_id").SequenceEqual(Bytes(member, "membership_id")) ||
            Number(session, "account_revision") != Number(account, "security_revision") ||
            Number(session, "membership_revision") != Number(member, "approval_revision")) return new(false);
        var factor = await u.One("SELECT * FROM c05_identity_mfa_factor WHERE account_id=@a AND factor_id=@f AND factor_state=1 AND revoked_at_utc IS NULL FOR UPDATE",
            ("a", actor), ("f", Bytes(session, "mfa_factor_id")));
        if (factor is null) return new(false);

        // C06-03 runs before ANY authenticated operation, including session
        // reads, keepalive, account changes and tenant-private resource work.
        // Actor/tenant/membership/factor are sourced exclusively from locked
        // C05/C04 backend rows, never browser IDs, roles or JWT assertions.
        var trustedClock = await u.One("SELECT UTC_TIMESTAMP(6) AS trusted_now_utc");
        if (trustedClock is null || u.VerifiedHost is null)
            throw new UnauthorizedAccessException("Trusted identity context unavailable.");
        var c06Context = ResolveC06TenantContextFromLockedRows(
            u.VerifiedHost, tenant, account, member, session, factor, idle,
            Utc(trustedClock, "trusted_now_utc"));

        u.VerifiedActor = true;
        await SlideTenantSession(u, tenant, hash, idle);
        if (r.Operation is "privacy/requests/list" or "privacy/requests/prepare" or "privacy/requests/confirm")
            return await C06OwnPrivacyRequest(u,tenant,actor,account,member,session,factor,idle,r,ct);
        // Explicit C06-11 laboratory overlay; historical HTTP operations remain unchanged.
        if (r.Operation == "delegation/catalog")
            return await C06DelegationCatalogPage(u, tenant, actor, account, member, session, factor, idle, r, ct);
        if (r.Operation is "delegation/inspect" or "delegation/prepare" or "delegation/confirm")
            return await C06DelegationTransition(u, tenant, actor, account, member, session, factor, idle, r, ct);
        if (r.Operation == "delegation/read")
            return await C06ReadDelegatedCase(u, tenant, actor, account, member, session, factor, idle, r);
        if (r.Operation == "sessions/keepalive")
            return new(true, new { expiresAtUtc = u.SessionExpiresAtUtc, idleMinutes = idle });
        if (r.Operation == "terms/accept")
            return new(await AcceptTerms(u, tenant, actor, r), new { accepted = true });
        if (r.Operation == "security/events")
        {
            var events = await u.All("SELECT action_code,outcome_code,created_at_utc FROM c05_identity_audit WHERE tenant_id=@t AND (actor_account_id=@a OR (subject_account_id=@a AND actor_account_id IS NULL)) ORDER BY created_at_utc DESC,audit_id DESC LIMIT 50", ("t", tenant), ("a", actor));
            return new(true, new { events = events.Select(e => new { action = e["action_code"], outcome = e["outcome_code"], atUtc = e["created_at_utc"] }) });
        }
        if (r.Operation == "factor/recover-passkey")
            return await RecoverUsingPasskey(u, tenant, account, session, factor, r);
        if (r.Operation.StartsWith("profile/", StringComparison.Ordinal)) return await Profile(u, tenant, actor, r);
        if ((r.Operation is "private/read" or "memberships/pending" or "memberships/decide" or "invitations/issue") &&
            !await HasCurrentTerms(u, tenant, actor)) return new(false);
        // A tenant-wide approver may read only verified, pending memberships.
        // The authorization grant is independently revalidated inside this
        // same tenant-scoped transaction before any applicant data is fetched.
        if (r.Operation == "memberships/pending")
            return await PendingMemberships(u, tenant, actor, member, session, r, Utc(trustedClock, "trusted_now_utc"));
        // Restore needs only the validated session window, not an inventory.
        if (r.Operation == "browser/restore")
            return new(true, new OperationalSessionInventory(u.SessionExpiresAtUtc!.Value, Array.Empty<OperationalSessionSummary>()));
        if (r.Operation == "sessions")
        {
            var rows = await u.All("""
                SELECT s.session_id,s.issued_at_utc,s.expires_at_utc,s.last_seen_at_utc,s.device_label,s.auth_method
                FROM c05_identity_session s JOIN c05_identity_mfa_factor f ON f.account_id=s.account_id AND f.factor_id=s.mfa_factor_id
                WHERE s.tenant_id=@t AND s.account_id=@a AND s.membership_id=@mid AND s.revoked_at_utc IS NULL AND s.expires_at_utc>UTC_TIMESTAMP(6)
                AND s.last_seen_at_utc>UTC_TIMESTAMP(6)-INTERVAL @idle MINUTE AND s.account_revision=@ar AND s.membership_revision=@mr
                AND f.factor_state=1 AND f.revoked_at_utc IS NULL ORDER BY s.last_seen_at_utc DESC,s.session_id DESC
                LIMIT 4097
                """, ("t", tenant), ("a", actor), ("mid", Bytes(member, "membership_id")), ("ar", Number(account, "security_revision")), ("mr", Number(member, "approval_revision")), ("idle", idle));
            // Existing closed DTO admits 4096 rows; never silently truncate an inventory.
            if (rows.Count > 4096) return new(false);
            return new(true, new OperationalSessionInventory(u.SessionExpiresAtUtc!.Value, rows.Select(row => new OperationalSessionSummary(
                new Guid(Bytes(row, "session_id"), true), Utc(row, "issued_at_utc"), Utc(row, "expires_at_utc"), Utc(row, "last_seen_at_utc"),
                Text(row, "device_label"), Text(row, "auth_method"))).ToArray()));
        }
        if (r.Operation is "logout" or "sessions/revoke" or "sessions/rotate")
        {
            var id = r.Operation == "sessions/revoke" ? GuidBytes(Field(r, "sessionId")) : Bytes(session, "session_id");
            var changed = await u.Run("UPDATE c05_identity_session SET revoked_at_utc=UTC_TIMESTAMP(6) WHERE tenant_id=@t AND account_id=@a AND session_id=@id AND revoked_at_utc IS NULL",
                ("t", tenant), ("a", actor), ("id", id));
            if (changed != 1) return new(false);
            if (r.Operation != "sessions/rotate") return new(true, new { accepted = true });
            // Rotation never refreshes the absolute MFA lifetime.
            u.SessionMethod = Text(session, "auth_method");
            return await IssueSession(u, tenant, account, member, Bytes(factor, "factor_id"), Bytes(session, "session_family"),
                (DateTime)session["mfa_verified_at_utc"]);
        }
        if (r.Operation == "private/read")
            return await C06ReadAuthorizedCase(u, tenant, actor, member, c06Context, r);
        if (r.Operation is "passkeys" or "passkeys/register")
            return await AuthenticatedPasskey(u, tenant, r, account, session, ct);
        // C06-05: closed, server-selected purpose. Input validity never replaces
        // C05's locked one-use factor consumption or the operation's permissions.
        if (!u.VerifiedActor || !C06StepUpInputValid(C06StepUpRealm.Tenant, r)) return new(false);
        // A passkey-authenticated session still uses the enrolled TOTP/backup
        // for independent step-up on changes of security settings.
        factor = await u.One("SELECT * FROM c05_identity_mfa_factor WHERE account_id=@a AND factor_kind=1 AND factor_state=1 AND revoked_at_utc IS NULL FOR UPDATE", ("a", actor));
        if (factor is null) return new(false);
        // All mutations below require a fresh factor proof; the browser cannot
        // declare step-up, approve itself, change roles, or choose a grant.
        if (!await Reserve(tenant, "stepup:" + Hex(actor), 8, ct) ||
            !await AcceptTotp(u, actor, factor, Field(r, "code"))) return new(false);
        if (r.Operation is "passkeys/register-options" or "passkeys/revoke")
            return await AuthenticatedPasskey(u, tenant, r, account, session, ct);
        if (r.Operation == "invitations/issue") return await IssueInvitation(u, tenant, account, r);
        if (r.Operation == "sessions/revoke-all")
        {
            await RevokeAll(u, actor);
            await NotifySecurity(u, tenant, actor, "SESSIONS_REVOKED");
            return new(true, new { accepted = true });
        }
        if (r.Operation == "pairings/approve") return await ApprovePairing(u, tenant, account, member, session, r);
        if (r.Operation == "factor/replace")
        {
            if (!crypto.VerifyPassword(Field(r, "currentPassword"), Text(account, "password_phc"))) return new(false);
            // Keep the current factor usable until the new seed is actually
            // proven. Interrupting enrollment cannot strand the account.
            await RevokeAll(u, actor);
            return await Challenge(u, tenant, account, member, true, Bytes(factor, "factor_id"));
        }
        if (r.Operation == "password/change")
        {
            if (!crypto.VerifyPassword(Field(r, "currentPassword"), Text(account, "password_phc"))) return new(false);
            await ReplacePassword(u, tenant, actor, crypto.HashPassword(Field(r, "newPassword")));
            return new(true, new { accepted = true });
        }
        if (r.Operation == "mailbox/request")
        {
            var mailbox = CanonicalLoginHandle.Parse(Field(r, "mailbox")).Value;
            if (!mailbox.EndsWith("@example.test", StringComparison.Ordinal)) return new(false);
            await StageProof(u, tenant, account, "MAILBOX", mailbox, Hex(Bytes(session, "session_id")));
            await NotifySecurity(u, tenant, actor, "MAILBOX_CHANGE_REQUESTED");
            return new(true, new { accepted = true });
        }
        if (r.Operation == "mailbox/complete")
        {
            var proof = await Proof(u, tenant, Field(r, "token"), "MAILBOX");
            if (proof is null || !Bytes(proof, "account_id").SequenceEqual(actor) ||
                Number(proof, "account_revision") != Number(account, "security_revision")) return new(false);
            var payload = DecodePayload(proof, tenant);
            if (payload.Binding != Hex(Bytes(session, "session_id"))) return new(false);
            var envelope = Protect(Encoding.UTF8.GetBytes(payload.Mailbox), "mailbox:" + Hex(actor));
            await u.Run("""
                UPDATE c05_identity_account SET login_lookup_hmac=@l,mailbox_ciphertext=@c,mailbox_nonce=@n,mailbox_tag=@tag,
                mailbox_key_version=1,mailbox_crypto_scheme='OPERATIONAL_V1',mailbox_verified_at_utc=UTC_TIMESTAMP(6),security_revision=security_revision+1,updated_at_utc=UTC_TIMESTAMP(6) WHERE account_id=@a
                """, ("l", LoginDigest(payload.Mailbox)), ("c", envelope[28..]), ("n", envelope[..12]), ("tag", envelope[12..28]), ("a", actor));
            await RevokeAll(u, actor);
            await NotifySecurity(u, tenant, actor, "MAILBOX_CHANGED");
            await ChangeContact(u, actor, payload.Mailbox);
            await NotifySecurity(u, tenant, actor, "MAILBOX_CONFIRMED");
            return new(true, new { accepted = true });
        }
        if (r.Operation == "memberships/decide")
        {
            var authority = await u.One("""
                SELECT grant_id FROM c05_identity_resource_grant WHERE tenant_id=@t AND account_id=@a
                AND permission_code='MEMBERSHIP_APPROVE' AND scope_kind=2 AND resource_id IS NULL
                AND branch_id=@t
                AND revoked_at_utc IS NULL AND granted_at_utc<=UTC_TIMESTAMP(6)
                AND expires_at_utc>UTC_TIMESTAMP(6) LIMIT 1 FOR SHARE
                """, ("t", tenant), ("a", actor));
            if (authority is null) return new(false);
            var target = await u.One("SELECT account_id FROM c05_identity_membership WHERE tenant_id=@t AND membership_id=@m", ("t", tenant), ("m", GuidBytes(Field(r, "membershipId"))));
            if (target is null || Bytes(target, "account_id").SequenceEqual(actor)) return new(false);
            var targetId = Bytes(target, "account_id");
            u.Subject = targetId;
            var targetAccount = await u.One("SELECT account_state FROM c05_identity_account WHERE account_id=@a AND mailbox_verified_at_utc IS NOT NULL FOR UPDATE", ("a", targetId));
            if (targetAccount is null || Number(targetAccount, "account_state") is not (0 or 1)) return new(false);
            var targetMember = await u.One("SELECT * FROM c05_identity_membership WHERE tenant_id=@t AND account_id=@a AND membership_state=0 FOR UPDATE", ("t", tenant), ("a", targetId));
            if (targetMember is null) return new(false);
            var approve = Field(r, "decision") == "approve";
            if (!approve && Field(r, "decision") != "reject") return new(false);
            await u.Run("""
                INSERT INTO c05_identity_membership_approval VALUES(@t,@m,@a,@s,@d,@reason,UTC_TIMESTAMP(6))
                """, ("t", tenant), ("m", Bytes(targetMember, "membership_id")), ("a", actor), ("s", Bytes(session, "session_id")),
                ("d", approve ? 1 : 2), ("reason", Digest("reason", Field(r, "reason"))));
            await u.Run("""
                UPDATE c05_identity_membership SET membership_state=@state,role_code=@role,approval_revision=approval_revision+1,
                approved_by=@approver,approved_at_utc=UTC_TIMESTAMP(6),granted_at_utc=IF(@state=1,UTC_TIMESTAMP(6),NULL),
                expires_at_utc=IF(@state=1,UTC_TIMESTAMP(6)+INTERVAL 1 YEAR,NULL) WHERE tenant_id=@t AND account_id=@a
                """, ("state", approve ? 1 : 3), ("role", approve ? "MEMBER" : "REJECTED"), ("approver", actor), ("t", tenant), ("a", targetId));
            if (approve) await u.Run("UPDATE c05_identity_account SET account_state=1,updated_at_utc=UTC_TIMESTAMP(6) WHERE account_id=@a AND account_state=0", ("a", targetId));
            return new(true, new { accepted = true });
        }
        return new(false);
    }

    private static bool C06StepUpInputValid(C06StepUpRealm realm, OperationalIdentityRequest request)
    {
        request.Fields.TryGetValue("code", out var code);
        request.Fields.TryGetValue("reason", out var reason);
        return C06SensitiveOperationPolicy.AcceptsInput(realm, request.Operation, code, reason);
    }

    // The operator session is independently resolved and locked in Platform().
    // Store only an HMAC of the declared purpose in the existing platform audit;
    // the audit outcome still comes from the actual operation and fresh factor.
    private bool C06PlatformStepUpPurpose(Unit u, Dictionary<string, object> principal,
        OperationalIdentityRequest request)
    {
        if (!u.VerifiedActor || u.Actor is not { } actor ||
            !actor.SequenceEqual(Bytes(principal, "operator_id")) ||
            !C06StepUpInputValid(C06StepUpRealm.Platform, request)) return false;
        var reason = C06SensitiveOperationPolicy.RequiresHumanReason(C06StepUpRealm.Platform, request.Operation)
            ? Field(request, "reason") : string.Empty;
        u.PurposeHash = Digest("c06-step-up-purpose", request.Operation + "\n" + reason);
        return true;
    }
}
