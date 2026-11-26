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

        // Do not persist factor codes, raw reasons, bearer tokens or private
        // content. The HMAC is operation metadata, not a cryptographic proof
        // that the MFA was actually checked/consumed upstream.
        var (purpose, intent) = Digests(evidence, verifiedSessionId, serverKey, reason);
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
                Add(command, "g", Key(Guid.NewGuid()));
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
                Add(command, "g", Key(stored!.Id));
                Add(command, "expected", decision.ExpectedRevision);
                BindTransition(command, evidence, decision, purpose, intent);
                if (await command.ExecuteNonQueryAsync(ct) != 1)
                    throw new InvalidOperationException("Delegation revocation was not appended.");
            }
            return true;
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

    // Internal metadata digest also usable by a future confirmation/proof
    // adapter. Fixed-width binary IDs and integers plus length-framed UTF8
    // prevent delimiter/canonicalization ambiguity. Clock of execution is
    // intentionally excluded: the same intent stays bound across confirmation.
    internal static (byte[] Purpose, byte[] Intent) Digests(C06LockedDelegationEvidence e,
        Guid verifiedSessionId, byte[] serverKey, string reason)
    {
        if (verifiedSessionId == Guid.Empty || serverKey is not { Length: >= 32 } || !ValidReason(reason))
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
            writer.Write("CRECISOL/C06/delegation/intent/v1");
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
