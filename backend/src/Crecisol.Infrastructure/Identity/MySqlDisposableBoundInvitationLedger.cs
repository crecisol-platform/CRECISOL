using System.Data;
using System.Security.Cryptography;
using Crecisol.Application.Identity;
using Crecisol.Domain.Identity;
using Crecisol.Domain.Tenancy;
using Crecisol.Infrastructure.Tenancy;
using MySqlConnector;

namespace Crecisol.Infrastructure.Identity;

/// <summary>
/// ONLY disposable c05_02_disposable on loopback. Both stored procedures run
/// with EXECUTE-only SQL credentials; no direct invitation or scope table writes.
/// The issue routine binds immutable issuer/branch/resource/action metadata in
/// the SAME transaction as the pending invitation. The claim routine rechecks
/// those exact rows plus the recipient and consent in ONE UPDATE/CAS.
/// This is not a production session, identity or tenant membership adapter.
/// </summary>
public sealed class MySqlDisposableBoundInvitationLedger : ITrustedBoundInvitationLedger
{
    private const string RequiredAction = "identity.invitation.issue";
    private readonly string trustedDsn;

    public MySqlDisposableBoundInvitationLedger(string? trustedLocalTestDsn)
    {
        if (string.IsNullOrWhiteSpace(trustedLocalTestDsn))
            throw new InvalidOperationException("Local C05-02 bound claim DSN required.");
        var settings = new MySqlConnectionStringBuilder(trustedLocalTestDsn)
        {
            Pooling = false,
            ConnectionTimeout = 5,
            DefaultCommandTimeout = 12
        };
        if (settings.Database != "c05_02_disposable" ||
            settings.Server is not ("127.0.0.1" or "localhost" or "::1") ||
            settings.AllowLoadLocalInfile)
            throw new InvalidOperationException("Only isolated loopback bound claim storage is allowed.");
        trustedDsn = settings.ConnectionString;
    }

    public async Task<bool> TryIssueAuthorizedOnceAsync(
        InvitationSnapshot pending,
        AccountTenantBindingEvidence issuer,
        TenantBranchResourceScopeEvidence scope,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(pending);
        ArgumentNullException.ThrowIfNull(issuer);
        ArgumentNullException.ThrowIfNull(scope);
        cancellationToken.ThrowIfCancellationRequested();

        if (pending.State != InvitationLifecycleState.Pending ||
            pending.Revision != 1 ||
            pending.IssuedUtc != pending.ChangedUtc ||
            pending.Profile is not (AccountTenantProfileKind.Employee or
                                    AccountTenantProfileKind.Consumer) ||
            pending.TenantId != issuer.TenantId ||
            pending.IssuedBy != issuer.ActorId ||
            scope.ActorId != issuer.ActorId ||
            scope.TenantId != issuer.TenantId ||
            scope.RequiredAction.Value != RequiredAction ||
            issuer.SessionRevision != scope.SessionRevision ||
            issuer.LinkRevision != scope.MembershipRevision ||
            issuer.HostRevision != scope.HostRevision ||
            issuer.TenantCatalogRevision != scope.TenantCatalogRevision ||
            issuer.AccountRevision <= 0 || issuer.LinkRevision <= 0 ||
            issuer.SessionRevision <= 0 || issuer.HostRevision <= 0 ||
            issuer.TenantCatalogRevision <= 0 ||
            scope.BranchRevision <= 0 || scope.ResourceRevision <= 0 ||
            scope.BranchGrantRevision <= 0 ||
            pending.ExpiresUtc <= pending.IssuedUtc ||
            pending.ExpiresUtc - pending.IssuedUtc > TimeSpan.FromDays(3))
            throw new UnauthorizedAccessException("Invitation issue unavailable.");

        var digest = pending.Digest.CopyForProtectedPersistenceAdapter();
        try
        {
            await using var db = new MySqlConnection(trustedDsn);
            await db.OpenAsync(cancellationToken);
            await using var command = new MySqlCommand("c05_02_issue_bound_once", db)
            {
                CommandType = CommandType.StoredProcedure
            };
            AddId(command, "p_tenant", pending.TenantId.Value);
            AddId(command, "p_invitation", pending.Id.Value);
            AddId(command, "p_issuer", issuer.ActorId.Value);
            AddId(command, "p_link", issuer.LinkId.Value);
            AddId(command, "p_recipient", pending.Recipient.Value);
            command.Parameters.Add("p_profile", MySqlDbType.VarChar).Value =
                pending.Profile == AccountTenantProfileKind.Employee ? "EMPLOYEE" : "CONSUMER";
            command.Parameters.Add("p_digest", MySqlDbType.Binary).Value = digest;
            AddLong(command, "p_terms", pending.TermsVersion);
            command.Parameters.Add("p_issued", MySqlDbType.DateTime).Value =
                pending.IssuedUtc.UtcDateTime;
            command.Parameters.Add("p_expires", MySqlDbType.DateTime).Value =
                pending.ExpiresUtc.UtcDateTime;
            AddLong(command, "p_account_rev", issuer.AccountRevision);
            AddLong(command, "p_session_rev", issuer.SessionRevision);
            AddLong(command, "p_link_rev", issuer.LinkRevision);
            AddLong(command, "p_host_rev", issuer.HostRevision);
            AddLong(command, "p_catalog_rev", issuer.TenantCatalogRevision);
            AddId(command, "p_branch", scope.BranchId.Value);
            AddId(command, "p_resource", scope.ResourceId.Value);
            AddLong(command, "p_branch_rev", scope.BranchRevision);
            AddLong(command, "p_resource_rev", scope.ResourceRevision);
            AddLong(command, "p_grant_rev", scope.BranchGrantRevision);
            command.Parameters.Add("p_action", MySqlDbType.VarChar).Value = RequiredAction;
            command.Parameters.Add("p_scope", MySqlDbType.VarChar).Value =
                scope.Scope switch
                {
                    BranchGrantScope.OwnerOnly => "OWNER_ONLY",
                    BranchGrantScope.BranchWide => "BRANCH_WIDE",
                    _ => throw new UnauthorizedAccessException("Invalid grant scope.")
                };
            var result = await command.ExecuteScalarAsync(cancellationToken);
            return result switch
            {
                "ISSUED" => true,
                "DENIED" => false,
                _ => throw new InvalidOperationException("Unexpected bound issue response.")
            };
        }
        finally
        {
            CryptographicOperations.ZeroMemory(digest);
        }
    }

    public async Task<bool> TryClaimBoundOnceAtomicallyAsync(
        TenantId verifiedTenant, InvitationId invitationId,
        RecipientChannelBindingId verifiedChannel, TenantActorId verifiedActor,
        InvitationTokenDigest digest, DateTimeOffset nowUtc,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(verifiedTenant);
        ArgumentNullException.ThrowIfNull(invitationId);
        ArgumentNullException.ThrowIfNull(verifiedChannel);
        ArgumentNullException.ThrowIfNull(verifiedActor);
        ArgumentNullException.ThrowIfNull(digest);
        InvitationSnapshot.RequireUtc(nowUtc);
        cancellationToken.ThrowIfCancellationRequested();

        var protectedDigest = digest.CopyForProtectedPersistenceAdapter();
        try
        {
            await using var db = new MySqlConnection(trustedDsn);
            await db.OpenAsync(cancellationToken);
            await using var command = new MySqlCommand("c05_02_claim_bound_once", db)
            {
                CommandType = CommandType.StoredProcedure
            };
            AddId(command, "p_tenant", verifiedTenant.Value);
            AddId(command, "p_invitation", invitationId.Value);
            AddId(command, "p_actor", verifiedActor.Value);
            AddId(command, "p_recipient", verifiedChannel.Value);
            command.Parameters.Add("p_digest", MySqlDbType.Binary).Value = protectedDigest;

            // This is a metadata-provenance witness, NOT a browser-selected
            // terms version or revision. The procedure obtains both from the
            // invitation row itself under the atomic UPDATE instead.
            // A read-only stored routine below returns no tenant data to
            // the caller; avoid adding a SELECT grant to this worker.
            command.Parameters.Add("p_terms", MySqlDbType.Int64).Value = 0L;
            command.Parameters.Add("p_revision", MySqlDbType.Int64).Value = 0L;

            var result = await command.ExecuteScalarAsync(cancellationToken);
            return result switch
            {
                "CLAIMED" => true,
                "DENIED" => false,
                _ => throw new InvalidOperationException("Unexpected bound claim response.")
            };
        }
        finally
        {
            CryptographicOperations.ZeroMemory(protectedDigest);
        }
    }

    private static void AddId(MySqlCommand command, string parameter, Guid id) =>
        command.Parameters.Add(parameter, MySqlDbType.Binary).Value =
            MySqlRfc4122Guid.Encode(id);

    private static void AddLong(MySqlCommand command, string parameter, long value) =>
        command.Parameters.Add(parameter, MySqlDbType.Int64).Value = value;
}
