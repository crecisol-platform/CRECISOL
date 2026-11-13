using System.Data;
using System.Security.Cryptography;
using Crecisol.Application.Identity;
using Crecisol.Domain.Identity;
using Crecisol.Domain.Tenancy;
using Crecisol.Infrastructure.Tenancy;
using MySqlConnector;

namespace Crecisol.Infrastructure.Identity;

/// <summary>
/// C05-02 disposable, backend-only issuer authorization adapter.
/// Revalidates the *exact* account, session, link, host/catalog, tenant,
/// branch, resource/owner, action/grant and policy in a single InnoDB
/// transaction with issuance. Caller evidence is never itself a permission.
/// Only local c05_02_disposable and a restricted EXECUTE-only SQL role.
/// No browser entrypoint, authenticated production session or DI binding.
/// </summary>
public sealed class MySqlDisposableScopedInvitationLedger : ITrustedScopedInvitationLedger
{
    private const string RequiredAction = "identity.invitation.issue";
    private readonly string trustedDsn;

    public MySqlDisposableScopedInvitationLedger(string? serverHeldLocalTestDsn)
    {
        if (string.IsNullOrWhiteSpace(serverHeldLocalTestDsn))
            throw new InvalidOperationException("Isolated local issuer authorization required.");
        var settings = new MySqlConnectionStringBuilder(serverHeldLocalTestDsn)
        {
            Pooling = false,
            ConnectionTimeout = 5,
            DefaultCommandTimeout = 8
        };
        if (settings.Database != "c05_02_disposable" ||
            settings.Server is not ("127.0.0.1" or "localhost" or "::1") ||
            settings.AllowLoadLocalInfile)
            throw new InvalidOperationException("Only disposable loopback issuer SQL is permitted.");
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
            scope.SessionRevision != issuer.SessionRevision ||
            scope.MembershipRevision != issuer.LinkRevision ||
            scope.HostRevision != issuer.HostRevision ||
            scope.TenantCatalogRevision != issuer.TenantCatalogRevision ||
            issuer.AccountRevision <= 0 || issuer.LinkRevision <= 0 ||
            issuer.SessionRevision <= 0 || issuer.HostRevision <= 0 ||
            issuer.TenantCatalogRevision <= 0 ||
            scope.ResourceRevision <= 0 || scope.BranchRevision <= 0 ||
            scope.BranchGrantRevision <= 0 ||
            pending.ExpiresUtc <= pending.IssuedUtc ||
            pending.ExpiresUtc - pending.IssuedUtc > TimeSpan.FromDays(3))
            throw new UnauthorizedAccessException("Invitation issuance unavailable.");

        var digest = pending.Digest.CopyForProtectedPersistenceAdapter();
        try
        {
            await using var db = new MySqlConnection(trustedDsn);
            await db.OpenAsync(cancellationToken);
            await using var cmd = new MySqlCommand("c05_02_issue_scoped_once", db)
            {
                CommandType = CommandType.StoredProcedure
            };
            Id(cmd, "p_tenant", issuer.TenantId.Value);
            Id(cmd, "p_invitation", pending.Id.Value);
            Id(cmd, "p_issuer", issuer.ActorId.Value);
            Id(cmd, "p_link", issuer.LinkId.Value);
            Id(cmd, "p_recipient", pending.Recipient.Value);
            cmd.Parameters.Add("p_profile", MySqlDbType.VarChar).Value =
                pending.Profile switch
                {
                    AccountTenantProfileKind.Employee => "EMPLOYEE",
                    AccountTenantProfileKind.Consumer => "CONSUMER",
                    _ => throw new UnauthorizedAccessException("Forbidden profile.")
                };
            cmd.Parameters.Add("p_digest", MySqlDbType.Binary).Value = digest;
            Long(cmd, "p_terms", pending.TermsVersion);
            cmd.Parameters.Add("p_issued", MySqlDbType.DateTime).Value =
                pending.IssuedUtc.UtcDateTime;
            cmd.Parameters.Add("p_expires", MySqlDbType.DateTime).Value =
                pending.ExpiresUtc.UtcDateTime;
            Long(cmd, "p_account_rev", issuer.AccountRevision);
            Long(cmd, "p_session_rev", issuer.SessionRevision);
            Long(cmd, "p_link_rev", issuer.LinkRevision);
            Long(cmd, "p_host_rev", issuer.HostRevision);
            Long(cmd, "p_catalog_rev", issuer.TenantCatalogRevision);
            Id(cmd, "p_branch", scope.BranchId.Value);
            Id(cmd, "p_resource", scope.ResourceId.Value);
            Long(cmd, "p_branch_rev", scope.BranchRevision);
            Long(cmd, "p_resource_rev", scope.ResourceRevision);
            Long(cmd, "p_grant_rev", scope.BranchGrantRevision);
            cmd.Parameters.Add("p_action", MySqlDbType.VarChar).Value = RequiredAction;
            cmd.Parameters.Add("p_scope", MySqlDbType.VarChar).Value =
                scope.Scope switch
                {
                    BranchGrantScope.OwnerOnly => "OWNER_ONLY",
                    BranchGrantScope.BranchWide => "BRANCH_WIDE",
                    _ => throw new UnauthorizedAccessException("Unknown scoped grant.")
                };

            var result = await cmd.ExecuteScalarAsync(cancellationToken);
            return result switch
            {
                "ISSUED" => true,
                "DENIED" => false,
                _ => throw new InvalidOperationException("Unexpected atomic issuer result.")
            };
        }
        finally
        {
            CryptographicOperations.ZeroMemory(digest);
        }
    }

    private static void Id(MySqlCommand command, string key, Guid id) =>
        command.Parameters.Add(key, MySqlDbType.Binary).Value =
            MySqlRfc4122Guid.Encode(id);

    private static void Long(MySqlCommand command, string key, long value) =>
        command.Parameters.Add(key, MySqlDbType.Int64).Value = value;
}
