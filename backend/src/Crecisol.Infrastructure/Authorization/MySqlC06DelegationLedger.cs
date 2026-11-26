using System.Security.Cryptography;
using System.Text;
using Crecisol.Domain.Authorization;
using Crecisol.Domain.Tenancy;
using MySqlConnector;

namespace Crecisol.Infrastructure.Authorization;

/// <summary>
/// Internal persistence step only. Never expose this class through HTTP or DI.
/// The caller must hold the authenticated transaction, load the other authority
/// rows under lock, independently verify the commercial condition and consume
/// a real operation-bound MFA proof. This class cannot establish that identity.
/// No connection string, proof, privilege, or commit is created here.
/// </summary>
internal sealed class MySqlC06DelegationLedger(MySqlConnection connection, MySqlTransaction transaction)
{
    private sealed record StoredGrant(Guid Id, C06DelegationGrant Evidence);
    private sealed record StoredHead(C06DelegationCycleHead Evidence, Guid RoleId,
        long IssuanceRevision, DateTimeOffset? TerminalAtUtc);

    /// <summary>
    /// Append one immutable event in the caller's transaction after comparing
    /// actual stored state with the policy input. Exceptions propagate so the
    /// caller rolls back; a successful return is NOT a committed grant.
    /// </summary>
    internal async Task<bool> AppendAsync(C06LockedDelegationEvidence evidence,
        Guid verifiedSessionId, byte[] serverKey, string reason, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(evidence);
        if (transaction.Connection != connection)
            throw new InvalidOperationException("Delegation transaction is not active.");
        if (verifiedSessionId == Guid.Empty || serverKey is not { Length: >= 32 } ||
            !ValidReason(reason))
            return false;

        var decision = C06ScopedRoleDelegationPolicy.Decide(evidence);
        if (decision is null) return false;
        var i = evidence.Intent;
        var stored = await ReadExactAsync(i, ct);
        if (stored?.Evidence != evidence.Existing) return false;
        if (decision.Command == C06DelegationCommand.Assign && stored is not null ||
            decision.Command == C06DelegationCommand.Revoke && stored is null)
            return false;

        await AppendEventAsync(evidence, decision,
            decision.Command == C06DelegationCommand.Assign ? Guid.NewGuid() : stored!.Id,
            verifiedSessionId, serverKey, reason, ct);
        return true;
    }

    /// <summary>
    /// Isolated candidate for migration 028. Not used by an HTTP handler.
    /// The caller must still prove identity, commercial access, current ABAC,
    /// and operation-bound MFA inside its own transaction. A returned ID is
    /// not committed and never authorizes a read by itself.
    /// </summary>
    internal async Task<Guid?> AppendCycleAsync(C06LockedDelegationEvidence evidence,
        C06DelegationCycleHead? expectedHead, Guid verifiedSessionId, byte[] serverKey,
        string reason, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(evidence);
        if (transaction.Connection != connection)
            throw new InvalidOperationException("Delegation transaction is not active.");
        if (verifiedSessionId == Guid.Empty || serverKey is not { Length: >= 32 } ||
            !ValidReason(reason))
            return null;

        var i = evidence.Intent;
        var current = await ReadCycleEvidenceAsync(i, ct);
        if (current.Grant != evidence.Existing || current.Head != expectedHead)
            return null;

        C06DelegationDecision? decision;
        Guid grantId;
        if (i.Command == C06DelegationCommand.Assign && current.Head is not null)
        {
            grantId = Guid.NewGuid();
            var reissue = C06ScopedRoleDelegationPolicy.DecideReissue(evidence, current.Head, grantId);
            if (reissue is null) return null;
            decision = new(C06DelegationCommand.Assign, reissue.ExpectedRevision, reissue.NextRevision);
        }
        else
        {
            decision = C06ScopedRoleDelegationPolicy.Decide(evidence);
            if (decision is null) return null;
            if (decision.Command == C06DelegationCommand.Assign)
            {
                if (current.Head is not null || current.Grant is not null) return null;
                grantId = Guid.NewGuid();
            }
            else
            {
                if (current.Head is null || current.Grant is null) return null;
                grantId = current.Head.GrantId;
            }
        }

        // The immutable ledger insert and 028 head trigger are one SQL
        // statement; CAS or FK failure propagates to the caller's rollback.
        await AppendEventAsync(evidence, decision, grantId, verifiedSessionId, serverKey,
            reason, ct, cycle:true, cycleHead:current.Head);
        var after = await ReadCycleEvidenceAsync(i, ct);
        if (after.Head is not { } head || after.Grant is not { } grant ||
            head.GrantId != grantId || grant.GrantId != grantId ||
            head.Revision != decision.NextRevision || grant.Revision != decision.NextRevision ||
            (decision.Command == C06DelegationCommand.Assign &&
                (grant.RevokedAtUtc is not null || grant.FromUtc != evidence.NowUtc || grant.UntilUtc != i.UntilUtc)) ||
            (decision.Command == C06DelegationCommand.Revoke && grant.RevokedAtUtc != evidence.NowUtc))
            throw new InvalidOperationException("Delegation lifecycle transition not preserved.");
        return grantId;
    }

    private async Task AppendEventAsync(C06LockedDelegationEvidence evidence,
        C06DelegationDecision decision, Guid grantId, Guid verifiedSessionId, byte[] serverKey,
        string reason, CancellationToken ct, bool cycle = false, C06DelegationCycleHead? cycleHead = null)
    {
        var i = evidence.Intent;
        // Do not persist factor codes, raw reasons, bearer tokens or private
        // content. The HMAC is operation metadata, not a cryptographic proof
        // that the MFA was actually checked/consumed upstream.
        var (purpose, intent) = Digests(evidence, verifiedSessionId, serverKey, reason, cycle, cycleHead);
        try
        {
            if (decision.Command == C06DelegationCommand.Assign)
            {
                await using var command = Command("""
                    INSERT INTO c06_delegation_grant
                    (tenant_id,grant_id,branch_id,resource_id,action_id,subject_membership_id,
                     role_id,issuer_membership_id,role_revision,subject_revision,issuer_revision,
                     ceiling_revision,revision,issued_at_utc,expires_at_utc,purpose_hmac,intent_hmac)
                    VALUES(@t,@g,@b,@r,@action,@subject,@role,@actor,@rr,@sr,@ar,@cr,@revision,@now,@until,@purpose,@intent)
                    """);
                BindScope(command, i);
                Add(command, "g", Key(grantId));
                Add(command, "role", Key(i.RoleId));
                Add(command, "rr", i.ExpectedRoleRevision);
                Add(command, "sr", i.ExpectedSubjectMembershipRevision);
                Add(command, "until", i.UntilUtc!.Value.UtcDateTime);
                BindTransition(command, evidence, decision, purpose, intent);
                if (await command.ExecuteNonQueryAsync(ct) != 1)
                    throw new InvalidOperationException("Delegation issuance was not appended.");
            }
            else
            {
                // Use only the grant ID obtained by exact-scope lookup above,
                // never an ID supplied by a transport or a different tenant.
                await using var command = Command("""
                    INSERT INTO c06_delegation_revocation
                    (tenant_id,grant_id,branch_id,resource_id,action_id,actor_membership_id,
                     actor_revision,ceiling_revision,expected_revision,revision,revoked_at_utc,purpose_hmac,intent_hmac)
                    VALUES(@t,@g,@b,@r,@action,@actor,@ar,@cr,@expected,@revision,@now,@purpose,@intent)
                    """);
                BindScope(command, i, includeSubject: false);
                Add(command, "g", Key(grantId));
                Add(command, "expected", decision.ExpectedRevision);
                BindTransition(command, evidence, decision, purpose, intent);
                if (await command.ExecuteNonQueryAsync(ct) != 1)
                    throw new InvalidOperationException("Delegation revocation was not appended.");
            }
        }
        finally
        {
            CryptographicOperations.ZeroMemory(purpose);
            CryptographicOperations.ZeroMemory(intent);
        }
    }

    internal async Task<C06DelegationGrant?> ReadEvidenceAsync(C06DelegationIntent i, CancellationToken ct) =>
        (await ReadExactAsync(i, ct))?.Evidence;

    private async Task<StoredGrant?> ReadExactAsync(C06DelegationIntent i, CancellationToken ct)
    {
        await using var command = Command("""
            SELECT g.grant_id,g.role_id,s.account_id,g.issued_at_utc,g.expires_at_utc,
                   COALESCE(v.revision,g.revision) AS revision,v.revoked_at_utc
              FROM c06_delegation_grant g
              JOIN c05_identity_membership s ON s.tenant_id=g.tenant_id AND s.membership_id=g.subject_membership_id
              LEFT JOIN c06_delegation_revocation v ON v.tenant_id=g.tenant_id AND v.grant_id=g.grant_id
             WHERE g.tenant_id=@t AND g.branch_id=@b AND g.resource_id=@r
               AND g.action_id=@action AND g.subject_membership_id=@subject
             LIMIT 2 FOR SHARE
            """);
        BindScope(command, i);
        await using var rows = await command.ExecuteReaderAsync(ct);
        if (!await rows.ReadAsync(ct)) return null;
        var result = new StoredGrant(Id(rows.GetFieldValue<byte[]>(0)), new C06DelegationGrant(
            i.Scope, TenantActorId.From(Id(rows.GetFieldValue<byte[]>(2))), i.SubjectMembershipId,
            Id(rows.GetFieldValue<byte[]>(1)), rows.GetInt64(5), Utc(rows.GetDateTime(3)),
            Utc(rows.GetDateTime(4)), rows.IsDBNull(6) ? null : Utc(rows.GetDateTime(6))));
        if (await rows.ReadAsync(ct)) throw new InvalidOperationException("Ambiguous delegation ledger.");
        return result;
    }

    /// <summary>
    /// Read the head independently, then the latest immutable issuance and
    /// revocation. An absent or inconsistent head cannot manufacture absence
    /// of a grant. No private resource content is selected.
    /// </summary>
    internal async Task<(C06DelegationGrant? Grant, C06DelegationCycleHead? Head)> ReadCycleEvidenceAsync(
        C06DelegationIntent i, CancellationToken ct)
    {
        if (transaction.Connection != connection)
            throw new InvalidOperationException("Delegation transaction is not active.");
        StoredHead? head = null;
        await using (var command = Command("""
            SELECT h.grant_id,h.role_id,h.issuance_revision,h.revision,h.terminal_at_utc,s.account_id
              FROM c06_delegation_cycle_head h
              JOIN c05_identity_membership s ON s.tenant_id=h.tenant_id AND s.membership_id=h.subject_membership_id
             WHERE h.tenant_id=@t AND h.branch_id=@b AND h.resource_id=@r
               AND h.action_id=@action AND h.subject_membership_id=@subject
             LIMIT 2 FOR SHARE
            """))
        {
            BindScope(command, i);
            await using var rows = await command.ExecuteReaderAsync(ct);
            if (await rows.ReadAsync(ct))
            {
                var grantId = Id(rows.GetFieldValue<byte[]>(0));
                head = new(new(i.Scope, TenantActorId.From(Id(rows.GetFieldValue<byte[]>(5))),
                    i.SubjectMembershipId, grantId, rows.GetInt64(3)),
                    Id(rows.GetFieldValue<byte[]>(1)), rows.GetInt64(2),
                    rows.IsDBNull(4) ? null : Utc(rows.GetDateTime(4)));
                if (await rows.ReadAsync(ct))
                    throw new InvalidOperationException("Ambiguous delegation lifecycle head.");
            }
        }

        await using (var command = Command("""
            SELECT g.grant_id,g.role_id,s.account_id,g.issued_at_utc,g.expires_at_utc,
                   COALESCE(v.revision,g.revision) AS revision,v.revoked_at_utc,g.revision
              FROM c06_delegation_grant g
              JOIN c05_identity_membership s ON s.tenant_id=g.tenant_id AND s.membership_id=g.subject_membership_id
              LEFT JOIN c06_delegation_revocation v ON v.tenant_id=g.tenant_id AND v.grant_id=g.grant_id
             WHERE g.tenant_id=@t AND g.branch_id=@b AND g.resource_id=@r
               AND g.action_id=@action AND g.subject_membership_id=@subject
             ORDER BY g.revision DESC LIMIT 1 FOR SHARE
            """))
        {
            BindScope(command, i);
            await using var rows = await command.ExecuteReaderAsync(ct);
            if (!await rows.ReadAsync(ct))
            {
                if (head is not null) throw new InvalidOperationException("Delegation lifecycle issuance missing.");
                return (null, null);
            }

            var grant = new C06DelegationGrant(i.Scope,
                TenantActorId.From(Id(rows.GetFieldValue<byte[]>(2))), i.SubjectMembershipId,
                Id(rows.GetFieldValue<byte[]>(1)), rows.GetInt64(5), Utc(rows.GetDateTime(3)),
                Utc(rows.GetDateTime(4)), rows.IsDBNull(6) ? null : Utc(rows.GetDateTime(6)))
                { GrantId = Id(rows.GetFieldValue<byte[]>(0)) };
            if (head is null || head.Evidence.GrantId != grant.GrantId ||
                head.Evidence.Subject != grant.Subject || head.RoleId != grant.RoleId ||
                head.IssuanceRevision != rows.GetInt64(7) || head.Evidence.Revision != grant.Revision ||
                head.TerminalAtUtc != grant.RevokedAtUtc)
                throw new InvalidOperationException("Delegation lifecycle head does not match immutable history.");
            return (grant, head.Evidence);
        }
    }

    // Internal metadata digest also usable by a future confirmation/proof
    // adapter. Fixed-width binary IDs and integers plus length-framed UTF8
    // prevent delimiter/canonicalization ambiguity. Clock of execution is
    // intentionally excluded: the same intent stays bound across confirmation.
    internal static (byte[] Purpose, byte[] Intent) Digests(C06LockedDelegationEvidence e,
        Guid verifiedSessionId, byte[] serverKey, string reason, bool cycle = false,
        C06DelegationCycleHead? cycleHead = null)
    {
        if (verifiedSessionId == Guid.Empty || serverKey is not { Length: >= 32 } || !ValidReason(reason) ||
            (!cycle && cycleHead is not null))
            throw new ArgumentException("Invalid delegation metadata.");
        var reasonBytes = Encoding.UTF8.GetBytes("CRECISOL/C06/delegation/purpose/v1:" + reason.Trim().Normalize(NormalizationForm.FormC));
        byte[] purpose;
        try { purpose = HMACSHA256.HashData(serverKey, reasonBytes); }
        finally { CryptographicOperations.ZeroMemory(reasonBytes); }
        try
        {
            using var stream = new MemoryStream();
            using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
            var i = e.Intent;
            writer.Write(cycle ? "CRECISOL/C06/delegation/intent/v2" : "CRECISOL/C06/delegation/intent/v1");
            writer.Write(Key(e.Identity.TenantId.Value));
            writer.Write(Key(e.Identity.ActorId.Value));
            writer.Write(Key(e.Identity.MembershipId));
            writer.Write(Key(verifiedSessionId));
            writer.Write(e.Identity.SessionRevision);
            writer.Write(e.Identity.AccountRevision);
            writer.Write(e.Identity.MembershipRevision);
            writer.Write(e.Identity.HostRevision);
            writer.Write(e.Identity.TenantCatalogRevision);
            writer.Write((int)i.Command);
            writer.Write(Key(i.Scope.Tenant.Value));
            writer.Write(Key(i.Scope.Branch.Value));
            writer.Write(Key(i.Scope.Resource.Value));
            writer.Write(i.Scope.Action.Value);
            writer.Write(Key(i.Subject.Value));
            writer.Write(Key(i.SubjectMembershipId));
            writer.Write(Key(i.RoleId));
            writer.Write(i.ExpectedRoleRevision);
            writer.Write(i.ExpectedSubjectMembershipRevision);
            writer.Write(i.ExpectedCeilingRevision);
            writer.Write(i.ExpectedGrantRevision);
            if (cycle)
            {
                writer.Write(cycleHead is not null);
                if (cycleHead is not null)
                {
                    writer.Write(Key(cycleHead.GrantId));
                    writer.Write(cycleHead.Revision);
                }
            }
            writer.Write(i.UntilUtc.HasValue);
            if (i.UntilUtc is { } until) writer.Write(until.UtcTicks);
            writer.Write(purpose);
            writer.Flush();
            var data = stream.ToArray();
            try { return (purpose, HMACSHA256.HashData(serverKey, data)); }
            finally { CryptographicOperations.ZeroMemory(data); }
        }
        catch { CryptographicOperations.ZeroMemory(purpose); throw; }
    }

    internal static bool ValidReason(string? reason)
    {
        if (reason is null || reason.Length > 240 || reason.Trim().Length < 8 ||
            reason.Any(char.IsControl) || !reason.Any(char.IsLetterOrDigit)) return false;
        try
        {
            // Reject malformed UTF-16 rather than mapping distinct inputs to U+FFFD.
            _ = new UTF8Encoding(false, true).GetByteCount(reason);
            return true;
        }
        catch (EncoderFallbackException) { return false; }
    }

    private MySqlCommand Command(string sql) => new(sql, connection, transaction);
    private static void Add(MySqlCommand command, string name, object value) =>
        command.Parameters.AddWithValue("@" + name, value);
    private static byte[] Key(Guid value) => value.ToByteArray(bigEndian: true);
    private static Guid Id(byte[] value) => new(value, bigEndian: true);
    private static DateTimeOffset Utc(DateTime value) =>
        new(DateTime.SpecifyKind(value, DateTimeKind.Utc));
    private static void BindScope(MySqlCommand command, C06DelegationIntent i, bool includeSubject = true)
    {
        Add(command, "t", Key(i.Scope.Tenant.Value));
        Add(command, "b", Key(i.Scope.Branch.Value));
        Add(command, "r", Key(i.Scope.Resource.Value));
        Add(command, "action", i.Scope.Action.Value);
        if (includeSubject) Add(command, "subject", Key(i.SubjectMembershipId));
    }
    private static void BindTransition(MySqlCommand command, C06LockedDelegationEvidence e,
        C06DelegationDecision decision, byte[] purpose, byte[] intent)
    {
        Add(command, "actor", Key(e.Identity.MembershipId));
        Add(command, "ar", e.Identity.MembershipRevision);
        Add(command, "cr", e.Intent.ExpectedCeilingRevision);
        Add(command, "revision", decision.NextRevision);
        Add(command, "now", e.NowUtc.UtcDateTime);
        Add(command, "purpose", purpose);
        Add(command, "intent", intent);
    }
}
