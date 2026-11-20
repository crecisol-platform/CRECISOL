#!/usr/bin/env python3
"""C05 F03 actual approvals inbox — A/B/C source gates.

These fail closed on missing contracts and security predicates. Source audits
are NOT substitutes for the .NET 10 + MySQL 8.4 HTTP integration run.
"""
from pathlib import Path
import sys

ROOT = Path(__file__).resolve().parents[1]
API = ROOT / "backend/src/Crecisol.Api/Modules/Identity/OperationalIdentityModule.cs"
FLOW = ROOT / "backend/src/Crecisol.Infrastructure/Identity"
PORTAL = ROOT / "frontend/src/app/pages"
SERVICE = ROOT / "frontend/src/app/identity/identity-api.service.ts"
E2E = ROOT / "qa/c05_operational_flow_e2e.mjs"
FRONTEND = ROOT / "frontend/tests/c05-f03-account.spec.ts"
WORKFLOW = ROOT / ".github/workflows/c05-ux-f03-navigation.yml"


def need(ok: bool, desc: str) -> None:
    if not ok:
        raise AssertionError("C05 F03 APPROVALS NO-GO: " + desc)


def read(path: Path) -> str:
    need(path.is_file(), "missing " + str(path))
    return path.read_text("utf-8")


def contains(text: str, values: tuple[str, ...], what: str) -> None:
    for value in values:
        need(value in text, what + ": missing " + value)


def a() -> None:
    route = read(API)
    flow = read(FLOW / "MySqlOperationalIdentityFlow.Authorized.cs")
    queue = read(FLOW / "MySqlOperationalIdentityFlow.PendingApprovals.cs")
    service = read(SERVICE)
    view = read(PORTAL / "identity-workspace.component.html")
    contains(route, ('["memberships/pending"] = ["cursor"]',),
             "strict server request contract")
    contains(flow, ('r.Operation == "memberships/pending"', "PendingMemberships(u, tenant, actor, r)",
                    'or "memberships/pending"'),
             "authenticated server operation")
    contains(queue, ("PendingApprovalPageSize = 12", "nextCursor", "MaskedPendingMailbox",
                     'id = new Guid(Bytes(row, "membership_id"), true).ToString("D")'),
             "bounded list response")
    contains(service, ("async loadPendingMemberships(", "'memberships/pending', { cursor }",
                       "this.pendingMemberships.set([])", "this.pendingLoaded.set(false)"),
             "frontend network and logout invalidation")
    contains(view, ("identity.pendingLoaded()", "pendingRows()", "pendingRequested.emit(true)",
                    'select id="membership-id" name="membershipId" required'),
             "review inbox and selected case")
    print("F03 APPR/A PASS: bounded server list + selected account review + session cleanup")


def b() -> None:
    flow = read(FLOW / "MySqlOperationalIdentityFlow.Authorized.cs")
    queue = read(FLOW / "MySqlOperationalIdentityFlow.PendingApprovals.cs")
    service = read(SERVICE)
    portal = read(PORTAL / "identity-portal.component.ts")
    html = read(PORTAL / "identity-workspace.component.html")
    contains(flow, ("r.Bearer is not { Length: 64 }", "await Membership(u, tenant, actor)",
                    "last_seen_at_utc>UTC_TIMESTAMP(6)-INTERVAL @idle MINUTE",
                    "if (factor is null) return new(false);",
                    "HasCurrentTerms(u, tenant, actor)"),
             "session and terms authorization preserved")
    contains(queue, ("tenant_id=@t", "account_id=@a", "permission_code='MEMBERSHIP_APPROVE'",
                     "scope_kind=2 AND resource_id IS NULL AND branch_id=@t",
                     "revoked_at_utc IS NULL", "expires_at_utc>UTC_TIMESTAMP(6)",
                     "m.tenant_id=@t AND m.membership_state=0",
                     "m.account_id<>@a", "a.mailbox_verified_at_utc IS NOT NULL",
                     "a.account_state IN (0,1)", "LIMIT 13"),
             "permission, tenant and bounded verified members")
    contains(queue, ("cursor.Split('/', 2)", "TryParseExact(", "TryParseExact(pieces[1],",
                     "return new(false);", "MaskedPendingMailbox(", "Unprotect(", "ZeroMemory(plain)"),
             "keyset and masked contact memory handling")
    contains(service, ("private pendingRequestRevision = 0;",
                       "const loadRevision = ++this.pendingRequestRevision",
                       "loadRevision !== this.pendingRequestRevision",
                       "++this.pendingRequestRevision;"),
             "late approved response must not restore PII after permission denial or logout")
    contains(portal, ("!this.identity.pendingMemberships().some(",
                      "field('confirmed') !== 'on'"), "explicit trusted selection")
    contains(html, ('name="confirmed" required', 'name="code" autocomplete="one-time-code"',
                    '<option value="" selected disabled>Selecciona una decisión',
                    '[value]="request.id"', 'id="membership-id" name="membershipId" required'),
             "MFA, none-default decision and no manual UUID")
    need('input id="membership-id"' not in html,
         "manual UUID entry reintroduced")
    for sensitive in ("mailbox_ciphertext", "password_phc", "protected_profile", "factor_seed"):
        need(sensitive not in queue, "extra sensitive field in queue query: " + sensitive)
    print("F03 APPR/B PASS: server-backed RBAC/tenant constraints, redacted PII, IDOR controls, MFA")


def c() -> None:
    e2e = read(E2E)
    ui_tests = read(FRONTEND)
    wf = read(WORKFLOW)
    css = read(PORTAL / "identity-workspace.component.css")
    contains(e2e, ("branch-scoped grant cannot list pending", "token from tenant A cannot enumerate tenant B",
                   "malformed keyset cursor fails closed", "authorized tenant-scoped approval inbox",
                   "Angular obtains allowed pending memberships through real tenant-authorized API"),
             "real .NET/MySQL HTTP integration negatives")
    contains(ui_tests, ("approval inbox: authorized", "approval inbox: unauthorized",
                        "stale allowed response cannot restore data after a newer permission denial",
                        "WCAG AA", "noHorizontalOverflow", "width: 320"),
             "Angular accessibility and privacy cases")
    contains(css, (".identity-approval-queue", ".identity-pending-list", ".identity-pending-card",
                   "forced-colors: active", "@media (max-width: 480px)"),
             "responsive premium approval queue")
    contains(wf, ("dotnet build backend/src/Crecisol.Api/Crecisol.Api.csproj",
                  "node qa/c05_operational_flow_e2e.mjs",
                  "python3 qa/c05_ux_f03_approvals_audits.py"),
             "actual real-backend CI gates")
    print("F03 APPR/C PASS: runtime negative tests present, responsive UI and full-stack CI required")


if __name__ == "__main__":
    try:
        need(len(sys.argv) == 2 and sys.argv[1] in ("A", "B", "C"),
             "usage: approvals_audits.py A|B|C")
        {"A": a, "B": b, "C": c}[sys.argv[1]]()
    except (AssertionError, OSError) as error:
        print("FAIL:", error, file=sys.stderr)
        raise SystemExit(1) from error
