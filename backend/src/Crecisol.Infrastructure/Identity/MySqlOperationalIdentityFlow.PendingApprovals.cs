using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Crecisol.Application.Identity;

namespace Crecisol.Infrastructure.Identity;

/// <summary>
/// Tenant-scoped read-only membership review inventory. No caller-provided tenant,
/// role, mailbox, account identifier, or unbounded offset can widen this query.
/// The opaque keyset cursor controls pagination only; it confers no authority.
/// </summary>
public sealed partial class MySqlOperationalIdentityFlow
{
    private const int PendingApprovalPageSize = 12;

    private async Task<OperationalIdentityResult> PendingMemberships(
        Unit u, byte[] tenant, byte[] actor, Dictionary<string, object> membership,
        Dictionary<string, object> session, OperationalIdentityRequest request, DateTimeOffset trustedNow)
    {
        // Same revocable tenant-wide permission as the audited decide endpoint.
        // A branch-only grant, expired grant, or grant from another tenant denies.
        var authority = await u.One("""
            SELECT grant_id,grant_revision FROM c05_identity_resource_grant
            WHERE tenant_id=@t AND account_id=@a
              AND permission_code='MEMBERSHIP_APPROVE'
              AND scope_kind=2 AND resource_id IS NULL AND branch_id=@t
              AND revoked_at_utc IS NULL AND granted_at_utc<=UTC_TIMESTAMP(6)
              AND expires_at_utc>UTC_TIMESTAMP(6)
            LIMIT 2 FOR SHARE
            """, ("t", tenant), ("a", actor));
        if (authority is null) return new(false);

        // No caller-specified offset: bounded keyset pagination is stable even
        // when an earlier request is approved or denied between pages.
        // Scope is derived from locked server evidence, never from cursor claims.
        // Bind even to grant revision and exact host; rotation/revocation/context
        // changes cannot make an old continuation a new authorization.
        var host = u.VerifiedHost ?? throw new UnauthorizedAccessException("Missing verified host.");
        var scope = string.Join("/", request.Host, Hex(tenant), Hex(actor),
            Hex(Bytes(membership, "membership_id")), Hex(Bytes(session, "session_id")),
            Hex(Bytes(authority, "grant_id")),
            Number(authority, "grant_revision").ToString(CultureInfo.InvariantCulture),
            Number(membership, "approval_revision").ToString(CultureInfo.InvariantCulture),
            Number(session, "session_revision").ToString(CultureInfo.InvariantCulture),
            Number(session, "account_revision").ToString(CultureInfo.InvariantCulture),
            Number(host, "host_revision").ToString(CultureInfo.InvariantCulture),
            Number(host, "catalog_revision").ToString(CultureInfo.InvariantCulture));
        var cursor = Field(request, "cursor");
        DateTime afterTime = DateTime.UnixEpoch;
        byte[] afterMembership = Guid.Empty.ToByteArray(true);
        if (cursor != "start")
        {
            if (!C06PendingCursor.TryDecode(tokenKey, scope, cursor, trustedNow, out afterTime, out var afterId))
                return new(false);
            afterMembership = afterId.ToByteArray(true);
        }

        // Select only the verified pending accounts within the authenticated
        // tenant. A contact may be absent; never return an unredacted mailbox.
        var rows = await u.All("""
            SELECT m.membership_id,m.account_id,m.created_at_utc,c.protected_mailbox
            FROM c05_identity_membership m
            INNER JOIN c05_identity_account a ON a.account_id=m.account_id
            LEFT JOIN c05_identity_contact c ON c.account_id=m.account_id
            WHERE m.tenant_id=@t AND m.membership_state=0 AND m.account_id<>@a
              AND a.mailbox_verified_at_utc IS NOT NULL AND a.account_state IN (0,1)
              AND (m.created_at_utc>@after OR
                (m.created_at_utc=@after AND m.membership_id>@cursorId))
            ORDER BY m.created_at_utc ASC,m.membership_id ASC
            LIMIT 13
            """,
            ("t", tenant), ("a", actor), ("after", afterTime),
            ("cursorId", afterMembership));

        var visible = rows.Take(PendingApprovalPageSize).ToArray();
        var requests = visible.Select(row => new
        {
            id = new Guid(Bytes(row, "membership_id"), true).ToString("D"),
            requestedAtUtc = Utc(row, "created_at_utc"),
            mailboxHint = MaskedPendingMailbox(row)
        }).ToArray();
        var last = visible.LastOrDefault();
        string? nextCursor = rows.Count > PendingApprovalPageSize && last is not null
            ? C06PendingCursor.Encode(tokenKey, scope, Utc(last, "created_at_utc").UtcDateTime,
                new Guid(Bytes(last, "membership_id"), true), trustedNow)
            : null;

        return new(true, new { requests, nextCursor });
    }

    private string MaskedPendingMailbox(Dictionary<string, object> row)
    {
        const string unavailable = "Correo verificado; identidad no disponible";
        if (!row.TryGetValue("protected_mailbox", out var data) ||
            data is not byte[] envelope || envelope.Length < 29)
            return unavailable;
        byte[] plain;
        try
        {
            plain = Unprotect(envelope, "contact:" + Hex(Bytes(row, "account_id")));
        }
        catch (CryptographicException)
        {
            // Corrupt or rotated contact data must not reveal raw PII or SQL.
            return unavailable;
        }
        try
        {
            var address = Encoding.UTF8.GetString(plain);
            var at = address.LastIndexOf('@');
            if (at < 1 || at >= address.Length - 3 || address.Length > 254)
                return unavailable;
            var prefix = address[..Math.Min(at, 2)];
            return prefix + "•••@" + address[(at + 1)..];
        }
        finally
        {
            CryptographicOperations.ZeroMemory(plain);
        }
    }
}
