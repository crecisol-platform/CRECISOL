using System.Security.Cryptography;
using Crecisol.Application.Identity;
using Crecisol.Domain.Authorization;

namespace Crecisol.Infrastructure.Identity;

public sealed partial class MySqlOperationalIdentityFlow
{
    private static Guid C06ExceptionId(byte[] id) => new(id, bigEndian: true);

    private async Task<OperationalIdentityResult> C06ExceptionalAccess(Unit u,
        Dictionary<string, object> principal, Dictionary<string, object> session,
        OperationalIdentityRequest request, CancellationToken ct)
    {
        var actor = Bytes(principal, "operator_id");
        var sessionId = Bytes(session, "session_id");
        var revision = Number(principal, "security_revision");
        if (!u.VerifiedActor || u.Actor is not { } verified || !verified.SequenceEqual(actor) ||
            !Bytes(session, "operator_id").SequenceEqual(actor) ||
            revision != Number(session, "security_revision") ||
            !C06ExceptionalAccessPolicy.IsOperation(request.Operation)) return new(false);

        byte[]? linked = null;
        string? Input(string key) => request.Fields.TryGetValue(key, out var value) ? value : null;

        async Task<OperationalIdentityResult> Finish(bool allowed, object? value = null)
        {
            // Commit of both this ledger and the outer C05 ledger is required
            // before ExecuteCoreAsync can release ANY private response.
            if (await u.Run("""
                INSERT INTO c06_exception_audit
                (event_id,exception_id,operator_id,session_id,action_code,allowed,event_at_utc)
                VALUES(@event,@id,@actor,@session,@action,@allowed,UTC_TIMESTAMP(6))
                """, ("event", Id()), ("id", (object?)linked ?? DBNull.Value), ("actor", actor),
                ("session", sessionId), ("action", request.Operation), ("allowed", allowed)) != 1)
                throw new InvalidOperationException("Exceptional audit unavailable.");
            u.Subject = linked;
            return new(allowed, value);
        }

        if (!await PlatformQuota("c06-exception:" + Hex(actor), 12, ct))
            return await Finish(false);

        var isOpen = request.Operation == "platform/breakglass/open";
        var isApprove = request.Operation == "platform/breakglass/approve";
        var isRead = request.Operation == "platform/breakglass/read";
        var isClose = request.Operation == "platform/breakglass/close";
        var code = Input("code");
        if (!isClose && !C06ExceptionalAccessPolicy.ValidProofShape(code))
            return await Finish(false);
        if ((isOpen || isApprove) && !C06ExceptionalAccessPolicy.ValidPurpose(Input("reason")))
            return await Finish(false);
        if (isApprove && !C06ExceptionalAccessPolicy.ValidPurpose(Input("reviewReason")))
            return await Finish(false);

        if (isOpen)
        {
            if (!C06ExceptionalAccessPolicy.ValidIdentifier(Input("tenantId")) ||
                !C06ExceptionalAccessPolicy.ValidIdentifier(Input("branchId")) ||
                !C06ExceptionalAccessPolicy.ValidIdentifier(Input("resourceId"))) return await Finish(false);
            var tenant = GuidBytes(Input("tenantId")!);
            var branch = GuidBytes(Input("branchId")!);
            var resource = GuidBytes(Input("resourceId")!);
            // Selectors never determine permission or placement. Only the
            // exact active, provisioned entitlement and verified local catalog do.
            var entitlement = await C06ExceptionEntitlement(u, actor, revision, tenant, branch, resource, 1);
            if (entitlement is null || !await C06ExceptionResourceActive(u, tenant, branch, resource) ||
                !await PlatformTotp(u, principal, code!)) return await Finish(false);
            var id = Id();
            var purpose = Digest("c06-exception-purpose:" + Hex(id), Input("reason")!.Trim());
            if (await u.Run("""
                INSERT INTO c06_exception_request
                (exception_id,requester_id,session_id,security_revision,entitlement_id,
                 tenant_id,branch_id,resource_id,purpose_hmac,requested_at_utc,expires_at_utc)
                VALUES(@id,@actor,@session,@revision,@grant,@t,@b,@r,@purpose,UTC_TIMESTAMP(6),
                       LEAST(UTC_TIMESTAMP(6)+INTERVAL 10 MINUTE,@grantExpiry))
                """, ("id", id), ("actor", actor), ("session", sessionId), ("revision", revision),
                ("grant", Bytes(entitlement, "entitlement_id")), ("t", tenant), ("b", branch),
                ("r", resource), ("purpose", purpose), ("grantExpiry", Utc(entitlement, "expires_at_utc").UtcDateTime)) != 1)
                return await Finish(false);
            linked = id; u.PurposeHash = purpose;
            var receipt = await u.One("SELECT expires_at_utc FROM c06_exception_request WHERE exception_id=@id FOR SHARE", ("id", id));
            if (receipt is null) throw new InvalidOperationException("Exceptional receipt unavailable.");
            return await Finish(true, new { exceptionId = C06ExceptionId(id), state = "pending",
                expiresAtUtc = Utc(receipt, "expires_at_utc") });
        }

        if (!C06ExceptionalAccessPolicy.ValidIdentifier(Input("exceptionId"))) return await Finish(false);
        var requested = GuidBytes(Input("exceptionId")!);
        var row = await u.One("SELECT * FROM c06_exception_request WHERE exception_id=@id FOR SHARE", ("id", requested));
        if (row is null) return await Finish(false);
        var requester = Bytes(row, "requester_id");
        var requestSession = Bytes(row, "session_id");
        var requestRevision = Number(row, "security_revision");
        var ownRequest = actor.SequenceEqual(requester) && sessionId.SequenceEqual(requestSession) && revision == requestRevision;
        var independent = C06ExceptionalAccessPolicy.IndependentReview(C06ExceptionId(requester), C06ExceptionId(actor));
        // Do not link unrelated caller attempts to the target or expose its metadata.
        if ((!isApprove && !ownRequest) || (isApprove && !independent)) return await Finish(false);
        var closed = await u.One("SELECT exception_id FROM c06_exception_close WHERE exception_id=@id FOR SHARE", ("id", requested));
        if (isClose)
        {
            linked = requested;
            // Closing authority is intentionally easier than acquiring authority.
            if (closed is not null) return await Finish(false);
            if (await u.Run("INSERT INTO c06_exception_close(exception_id,closed_by,closed_at_utc) VALUES(@id,@a,UTC_TIMESTAMP(6))",
                ("id", requested), ("a", actor)) != 1) return await Finish(false);
            return await Finish(true, new { state = "closed" });
        }

        var targetTenant = Bytes(row, "tenant_id");
        var targetBranch = Bytes(row, "branch_id");
        var targetResource = Bytes(row, "resource_id");
        var nowRow = await u.One("SELECT UTC_TIMESTAMP(6) AS now_utc");
        if (nowRow is null) return await Finish(false);
        var now = Utc(nowRow, "now_utc");
        if (closed is not null || now < Utc(row, "requested_at_utc") || now >= Utc(row, "expires_at_utc"))
            return await Finish(false);
        var requestGrant = await C06ExceptionEntitlement(u, requester, requestRevision, targetTenant, targetBranch, targetResource, 1);
        if (requestGrant is null || !Bytes(requestGrant, "entitlement_id").SequenceEqual(Bytes(row, "entitlement_id")) ||
            !await C06ExceptionResourceActive(u, targetTenant, targetBranch, targetResource))
            return await Finish(false);
        var approval = await u.One("SELECT * FROM c06_exception_approval WHERE exception_id=@id FOR SHARE", ("id", requested));
        if (isApprove)
        {
            var reviewGrant = await C06ExceptionEntitlement(u, actor, revision, targetTenant, targetBranch, targetResource, 2);
            if (reviewGrant is null) return await Finish(false);
            linked = requested;
            // Independent reviewer receives the incident justification through
            // the approved support procedure, not a list of private resources.
            var assertedPurpose = Digest("c06-exception-purpose:" + Hex(requested), Input("reason")!.Trim());
            if (approval is not null ||
                !CryptographicOperations.FixedTimeEquals(assertedPurpose, Bytes(row, "purpose_hmac")) ||
                !await C06ExceptionLiveSession(u, requester, requestSession, requestRevision) ||
                !await PlatformTotp(u, principal, code!)) return await Finish(false);
            var reviewPurpose = Digest("c06-exception-review:" + Hex(requested), Input("reviewReason")!.Trim());
            if (await u.Run("""
                INSERT INTO c06_exception_approval
                (exception_id,reviewer_id,review_session_id,review_revision,entitlement_id,review_hmac,approved_at_utc,expires_at_utc)
                VALUES(@id,@a,@session,@revision,@grant,@reason,UTC_TIMESTAMP(6),LEAST(@requestExpiry,@grantExpiry))
                """, ("id", requested), ("a", actor), ("session", sessionId), ("revision", revision),
                ("grant", Bytes(reviewGrant, "entitlement_id")), ("reason", reviewPurpose),
                ("requestExpiry", Utc(row, "expires_at_utc").UtcDateTime),
                ("grantExpiry", Utc(reviewGrant, "expires_at_utc").UtcDateTime)) != 1) return await Finish(false);
            u.PurposeHash = reviewPurpose;
            return await Finish(true, new { state = "approved" });
        }

        linked = requested;
        if (!isRead || approval is null) return await Finish(false);
        var reviewer = Bytes(approval, "reviewer_id");
        var reviewRevision = Number(approval, "review_revision");
        var reviewerGrant = await C06ExceptionEntitlement(u, reviewer, reviewRevision, targetTenant, targetBranch, targetResource, 2);
        if (reviewerGrant is null ||
            !Bytes(reviewerGrant, "entitlement_id").SequenceEqual(Bytes(approval, "entitlement_id")) ||
            !await C06ExceptionLiveSession(u, reviewer, Bytes(approval, "review_session_id"), reviewRevision) ||
            !C06ExceptionalAccessPolicy.IndependentReview(C06ExceptionId(requester), C06ExceptionId(reviewer)) ||
            !await PlatformTotp(u, principal, code!)) return await Finish(false);

        // Time is checked again after real proof verification, not just before.
        var finalClock = await u.One("SELECT UTC_TIMESTAMP(6) AS now_utc");
        if (finalClock is null) return await Finish(false);
        var evidence = new C06ExceptionalReadEvidence(true, ownRequest, true, true, true, true,
            true, true, closed is not null, C06ExceptionId(actor), C06ExceptionId(requester),
            C06ExceptionId(sessionId), C06ExceptionId(requestSession), revision, requestRevision,
            Utc(row, "requested_at_utc"), Utc(approval, "approved_at_utc"),
            Utc(approval, "expires_at_utc"), Utc(finalClock, "now_utc"));
        if (!C06ExceptionalAccessPolicy.AllowsRead(evidence)) return await Finish(false);

        // The only private projection. Scope is exclusively the locked request.
        // Recheck every deadline in the actual read; no lists, export, owner
        // information, cross-resource selectors, or blanket tenant permission.
        var projection = await u.One("""
            SELECT resource.display_reference
            FROM c06_exception_request req
            JOIN c06_exception_approval app ON app.exception_id=req.exception_id
            JOIN c06_exception_entitlement rg ON rg.entitlement_id=req.entitlement_id
            JOIN c06_exception_entitlement ag ON ag.entitlement_id=app.entitlement_id
            JOIN c05_identity_private_resource resource
              ON resource.tenant_id=req.tenant_id AND resource.branch_id=req.branch_id AND resource.resource_id=req.resource_id
            JOIN c05_identity_branch branch ON branch.tenant_id=resource.tenant_id AND branch.branch_id=resource.branch_id
            JOIN c04_tenant_catalog tenant ON tenant.tenant_id=resource.tenant_id
            WHERE req.exception_id=@id AND req.requester_id=@actor AND req.session_id=@session AND req.security_revision=@revision
              AND req.requested_at_utc<=UTC_TIMESTAMP(6) AND req.expires_at_utc>UTC_TIMESTAMP(6)
              AND app.approved_at_utc<=UTC_TIMESTAMP(6) AND app.expires_at_utc>UTC_TIMESTAMP(6)
              AND rg.revoked_at_utc IS NULL AND ag.revoked_at_utc IS NULL
              AND rg.expires_at_utc>UTC_TIMESTAMP(6) AND ag.expires_at_utc>UTC_TIMESTAMP(6)
              AND resource.resource_state=1 AND branch.active=1 AND tenant.lifecycle_state=3
              AND NOT EXISTS(SELECT 1 FROM c06_exception_close c WHERE c.exception_id=req.exception_id)
            FOR SHARE
            """, ("id", requested), ("actor", actor), ("session", sessionId), ("revision", revision));
        return projection is null ? await Finish(false) : await Finish(true, new { reference = Text(projection, "display_reference") });
    }

    private async Task<Dictionary<string, object>?> C06ExceptionEntitlement(Unit u, byte[] actor, long revision,
        byte[] tenant, byte[] branch, byte[] resource, int capability)
    {
        var matches = await u.All("""
            SELECT entitlement_id,expires_at_utc FROM c06_exception_entitlement
            WHERE operator_id=@a AND security_revision=@v AND tenant_id=@t AND branch_id=@b AND resource_id=@r
              AND capability=@cap AND issued_at_utc<=UTC_TIMESTAMP(6) AND expires_at_utc>UTC_TIMESTAMP(6) AND revoked_at_utc IS NULL
            LIMIT 2 FOR SHARE
            """, ("a", actor), ("v", revision), ("t", tenant), ("b", branch), ("r", resource), ("cap", capability));
        return matches.Count == 1 ? matches[0] : null;
    }

    private async Task<bool> C06ExceptionResourceActive(Unit u, byte[] tenant, byte[] branch, byte[] resource) =>
        await u.One("""
            SELECT r.resource_id FROM c05_identity_private_resource r
            JOIN c05_identity_branch b ON b.tenant_id=r.tenant_id AND b.branch_id=r.branch_id
            JOIN c04_tenant_catalog t ON t.tenant_id=r.tenant_id
            WHERE r.tenant_id=@t AND r.branch_id=@b AND r.resource_id=@r AND r.resource_state=1
              AND b.active=1 AND t.lifecycle_state=3 AND t.catalog_revision>0 AND t.updated_at_utc<=UTC_TIMESTAMP(6)
            FOR SHARE
            """, ("t", tenant), ("b", branch), ("r", resource)) is not null;

    private async Task<bool> C06ExceptionLiveSession(Unit u, byte[] actor, byte[] session, long revision)
    {
        var idle = await SessionIdleMinutes(u);
        return await u.One("""
            SELECT s.session_id FROM c05_platform_session s
            JOIN c05_platform_operator p ON p.operator_id=s.operator_id AND p.security_revision=s.security_revision
            WHERE s.session_id=@s AND s.operator_id=@a AND s.security_revision=@v
              AND p.active=1 AND p.recovery_pending=0 AND s.revoked_at_utc IS NULL
              AND s.expires_at_utc>UTC_TIMESTAMP(6)
              AND s.last_seen_at_utc>UTC_TIMESTAMP(6)-INTERVAL @idle MINUTE
            FOR SHARE
            """, ("s", session), ("a", actor), ("v", revision), ("idle", idle)) is not null;
    }
}
