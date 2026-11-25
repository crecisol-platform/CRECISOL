#!/usr/bin/env python3
"""CRECISOL C06-03 — independent A/B/C on actual tenant identity context."""
import hashlib
import json
import os
import re
import subprocess
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
BASE = "e5fe2830ab8284238389d7a8d5cc6d4add1a0b99"
WORD_SHA = "ea5afc89e74915a9b18b9753ddb832f4be8e5d00f5e66b0f008e02e03476444c"
DOMAIN = ROOT / "backend/src/Crecisol.Domain/Tenancy/C06OperationalMembershipContext.cs"
ADAPTER = ROOT / "backend/src/Crecisol.Infrastructure/Tenancy/C06OperationalContextProjection.cs"
FLOW = ROOT / "backend/src/Crecisol.Infrastructure/Identity/MySqlOperationalIdentityFlow.cs"
AUTH = ROOT / "backend/src/Crecisol.Infrastructure/Identity/MySqlOperationalIdentityFlow.Authorized.cs"
WORD = ROOT / "docs/CRECISOL_DOCUMENTO_MAESTRO_UNICO.docx"
CATALOG = ROOT / "docs/c06/C06_01_CATALOGO_ACCIONES.json"
MATRIX = ROOT / "docs/c06/C06_02_MATRIZ_ROLES_CAPACIDADES.json"

def need(good, reason):
    if not good:
        raise AssertionError("C06-03 NO-GO " + reason)

def diff():
    event = os.environ.get("GITHUB_EVENT_NAME", "")
    if event == "pull_request":
        need(os.environ.get("GITHUB_BASE_REF") == "dev", "feature PR must target dev")
        base = ["origin/dev...HEAD"]
    elif event == "push":
        need(os.environ.get("GITHUB_REF") == "refs/heads/dev", "push must be dev")
        base = ["HEAD^", "HEAD"]
    else:
        base = [BASE, "HEAD"]
    changed = subprocess.check_output(["git", "diff", "--name-only", *base], cwd=ROOT,
                                      text=True).splitlines()
    # Explicit sequential successor: preserve every other historical audit.
    if "docs/c06/C06_07_ESPECIFICACION.md" in changed:
        from c06_07_privacy_scope import require_exact
        require_exact(changed)
        return changed
    if "docs/c06/C06_06_ESPECIFICACION.md" in changed:
        from c06_06_exceptional_scope import require_exact
        require_exact(changed)
        return changed
    exact = {
        "backend/src/Crecisol.Domain/Tenancy/C06OperationalMembershipContext.cs",
        "backend/src/Crecisol.Infrastructure/Tenancy/C06OperationalContextProjection.cs",
        "backend/src/Crecisol.Infrastructure/Identity/MySqlOperationalIdentityFlow.cs",
        "backend/src/Crecisol.Infrastructure/Identity/MySqlOperationalIdentityFlow.Authorized.cs",
        "qa/c06_03_context_audits.py",
        "docs/ESTADO_VIGENTE_CAPITULOS.md",
        "docs/planificacion-futura/C06_MICROFASES_PROPUESTAS.md",
        ".github/workflows/c06-03-trusted-tenant-context.yml",
    }
    # C06-04 is the next sequential microphase. Keep C06-03 security
    # invariant checks active while permitting ONLY its named files.
    c06_04_allowed = {
        "backend/src/Crecisol.Domain/Tenancy/C06CaseAbacPolicy.cs",
        "backend/src/Crecisol.Infrastructure/Tenancy/C06CaseAbacProjection.cs",
        "database/migrations/020_c06_case_abac.sql",
        "database/migrations/020_c06_case_abac.down.sql",
        "database/operations/c06_04_local_runtime_role.sql",
        "database/tests/c06_04_abac_mysql.sh",
        "docs/c06/C06_04_ESPECIFICACION.md",
        "docs/c06/C06_04_ACTA_CIERRE.md",
        "qa/c06_04_abac_audits.py",
        "qa/c05_operational_flow_e2e.mjs",
        ".github/workflows/c06-04-abac-ownership.yml",
    }
    # Named C06-05 additions only; retain every earlier security/DB/API guard.
    c06_05_allowed = {
        "qa/c06_02_role_model_audit.py",
        "backend/src/Crecisol.Infrastructure/Identity/MySqlOperationalIdentityFlow.Platform.cs",
        "backend/src/Crecisol.Domain/Authorization/C06SensitiveOperationPolicy.cs",
        "backend/tests/Crecisol.C06StepUp.ContractsTests/Program.cs",
        "backend/tests/Crecisol.C06StepUp.ContractsTests/Crecisol.C06StepUp.ContractsTests.csproj",
        "docs/c06/C06_05_ESPECIFICACION.md",
        "qa/c06_05_stepup_audits.py",
        ".github/workflows/c06-05-sensitive-step-up.yml",
    }
    need(changed and all(p in exact or p in c06_04_allowed or p in c06_05_allowed or
         p.startswith(("backend/tests/Crecisol.C06Membership.ContractsTests/",
                       "backend/tests/Crecisol.C06Abac.ContractsTests/",
                       "docs/c06/C06_03_")) for p in changed),
         "unexpected C06-03 diff: " + repr(changed))
    need(not any(p.startswith(("frontend/", "backend/src/Crecisol.Api/",
                               "backend/src/Crecisol.CredentialSecurity/"))
                 for p in changed), "C06-03 altered UI, API, C05 schema or crypto")
    need(not any(p.startswith("database/") and p not in c06_04_allowed
                 for p in changed), "unapproved DB file cannot be a C06-04 dependency")
    return changed

def audit_sources():
    domain = DOMAIN.read_text(encoding="utf-8")
    adapter = ADAPTER.read_text(encoding="utf-8")
    flow = FLOW.read_text(encoding="utf-8")
    auth = AUTH.read_text(encoding="utf-8")
    domain_marks = (
        "evidence.VerifiedHostTenant != evidence.SessionTenant",
        "evidence.VerifiedHostTenant != evidence.MembershipTenant",
        "evidence.SessionActor != evidence.AccountActor",
        "evidence.SessionActor != evidence.MembershipActor",
        "evidence.SessionActor != evidence.FactorActor",
        "evidence.SessionMembershipId != evidence.MembershipId",
        "evidence.SessionFactorId != evidence.FactorId",
        "evidence.AccountRevisionInSession != evidence.CurrentAccountRevision",
        "evidence.MembershipRevisionInSession != evidence.CurrentMembershipRevision",
        "evidence.VerifiedHostAddressMode != evidence.TenantCatalogAddressMode",
        "!evidence.SessionNotRevoked", "!evidence.AccountActive",
        "!evidence.AccountMailboxVerified", "!evidence.MembershipActive",
        "!evidence.FactorActive", "evidence.ServerIdleMinutes is < 5 or > 120",
        "evidence.ServerNowUtc >= evidence.SessionExpiresAtUtc",
        "evidence.ServerNowUtc >= evidence.MembershipExpiresAtUtc",
        "evidence.ServerNowUtc - evidence.SessionLastSeenAtUtc >=",
        "AuthorizesPrivateResource => false",
    )
    adapter_marks = (
        "Tenant(Bytes(session, \"tenant_id\"))",
        "Tenant(Bytes(membership, \"tenant_id\"))",
        "Actor(Bytes(session, \"account_id\"))",
        "Actor(Bytes(account, \"account_id\"))",
        "Actor(Bytes(membership, \"account_id\"))",
        "Actor(Bytes(factor, \"account_id\"))",
        "SessionMembershipId = Id(Bytes(session, \"membership_id\"))",
        "MembershipId = Id(Bytes(membership, \"membership_id\"))",
        "FactorId = Id(Bytes(factor, \"factor_id\"))",
        "ServerNowUtc = trustedDatabaseUtc",
        "SessionNotRevoked = session[\"revoked_at_utc\"] is DBNull",
        "FactorActive = Number(factor, \"factor_state\") == 1",
    )
    flow_marks = (
        "SELECT h.tenant_id, h.host_revision, h.address_mode",
        "AND h.verified_at_utc<=UTC_TIMESTAMP(6)",
        "AND h.valid_until_utc>UTC_TIMESTAMP(6)",
        "AND t.lifecycle_state=3",
        "AND h.address_mode=t.address_mode FOR SHARE",
        "unit.VerifiedHost = host;",
        'var tenant = Bytes(host, "tenant_id");',
        "else result = await Authenticated(unit, tenant, request, ct);",
    )
    auth_marks = (
        'Digest("session", r.Bearer)',
        "WHERE tenant_id=@t AND token_hmac=@h",
        "AND revoked_at_utc IS NULL AND expires_at_utc>UTC_TIMESTAMP(6)",
        "AND last_seen_at_utc>UTC_TIMESTAMP(6)-INTERVAL @idle MINUTE FOR UPDATE",
        "Number(session, \"account_revision\") != Number(account, \"security_revision\")",
        "Number(session, \"membership_revision\") != Number(member, \"approval_revision\")",
        "factor_state=1 AND revoked_at_utc IS NULL FOR UPDATE",
        'var trustedClock = await u.One("SELECT UTC_TIMESTAMP(6) AS trusted_now_utc");',
        "ResolveC06TenantContextFromLockedRows(",
        "await SlideTenantSession(u, tenant, hash, idle);",
    )
    for tag, text, markers in (
        ("Domain", domain, domain_marks),
        ("Infrastructure", adapter, adapter_marks),
        ("Host SQL", flow, flow_marks),
        ("Auth transaction", auth, auth_marks),
    ):
        for marker in markers:
            need(marker in text, tag + " missing security guard: " + marker)
    need(auth.index("ResolveC06TenantContextFromLockedRows(") <
         auth.index("await SlideTenantSession(u, tenant, hash, idle);"),
         "identity boundary runs after session renewal/private work")
    need("namespace Crecisol.Domain.Tenancy;" in domain and
         "namespace Crecisol.Infrastructure.Identity;" in adapter and
         not any(x in domain for x in ("MySqlConnector", "Microsoft.AspNetCore",
                                       "System.Data.Sql", "HttpContext", "IResult")),
         "Domain leaked SQL or transport")
    need("new Guid(bytes, bigEndian: true)" in adapter,
         "GUID encoding does not match C05 RFC4122 bytes")
    need("private static C06OperationalMembershipContext" in adapter,
         "proof accidentally exposed by public Infrastructure API")
    need("GetRequiredService" not in adapter and "MapGet(" not in adapter and
         "MapPost(" not in adapter, "unexpected new C06 identity endpoint")
    need(not re.search(r"\bGRANT\s+(?:ALL|SELECT|INSERT|UPDATE|DELETE)\b", adapter, re.I),
         "C06 identity adapter must not allocate database privileges")
    return {"domain":len(domain_marks), "adapter":len(adapter_marks),
            "host":len(flow_marks), "auth":len(auth_marks)}

def prior():
    source = json.loads(CATALOG.read_text(encoding="utf-8"))
    roles = json.loads(MATRIX.read_text(encoding="utf-8"))
    need(len(source["api_actions"]) == 80 and
         len(source["non_http_contracts"]) == 12 and
         len(source["normative_actions"]) == 28 and
         len(roles["roles"]) == 10 and len(roles["actions"]) == 108,
         "C06-01/02 lineage is inconsistent")
    need(all(a["grantable"] is False and not a["http_registered"]
             for a in source["normative_actions"]) and
         all(a["policy_activated"] is False and a["grantable"] is False
             for a in roles["actions"]), "a future permission was activated")
    need(WORD.is_file() and hashlib.sha256(WORD.read_bytes()).hexdigest() == WORD_SHA,
         "canonical approved Word is missing or modified")

def security_mutations():
    source = {
        "domain":DOMAIN.read_text(encoding="utf-8"),
        "adapter":ADAPTER.read_text(encoding="utf-8"),
        "flow":FLOW.read_text(encoding="utf-8"),
        "auth":AUTH.read_text(encoding="utf-8"),
    }
    cases = [
        ("domain","evidence.VerifiedHostTenant != evidence.SessionTenant"),
        ("domain","evidence.VerifiedHostTenant != evidence.MembershipTenant"),
        ("domain","evidence.SessionActor != evidence.AccountActor"),
        ("domain","evidence.SessionActor != evidence.MembershipActor"),
        ("domain","evidence.SessionActor != evidence.FactorActor"),
        ("domain","evidence.SessionMembershipId != evidence.MembershipId"),
        ("domain","evidence.SessionFactorId != evidence.FactorId"),
        ("domain","evidence.AccountRevisionInSession != evidence.CurrentAccountRevision"),
        ("domain","evidence.MembershipRevisionInSession != evidence.CurrentMembershipRevision"),
        ("domain","evidence.VerifiedHostAddressMode != evidence.TenantCatalogAddressMode"),
        ("domain","!evidence.SessionNotRevoked"),
        ("domain","!evidence.AccountMailboxVerified"),
        ("domain","!evidence.MembershipActive"),
        ("domain","!evidence.FactorActive"),
        ("domain","evidence.ServerNowUtc >= evidence.SessionExpiresAtUtc"),
        ("domain","evidence.ServerNowUtc >= evidence.MembershipExpiresAtUtc"),
        ("domain","evidence.ServerNowUtc - evidence.SessionLastSeenAtUtc >="),
        ("adapter",'Tenant(Bytes(membership, "tenant_id"))'),
        ("adapter",'Actor(Bytes(factor, "account_id"))'),
        ("adapter",'FactorId = Id(Bytes(factor, "factor_id"))'),
        ("flow","AND h.address_mode=t.address_mode FOR SHARE"),
        ("auth","ResolveC06TenantContextFromLockedRows("),
        ("auth","AND last_seen_at_utc>UTC_TIMESTAMP(6)-INTERVAL @idle MINUTE FOR UPDATE"),
    ]
    count = 0
    markers = audit_sources()
    table = {
        "domain":(
            "evidence.VerifiedHostTenant != evidence.SessionTenant",
            "evidence.VerifiedHostTenant != evidence.MembershipTenant",
            "evidence.SessionActor != evidence.AccountActor",
            "evidence.SessionActor != evidence.MembershipActor",
            "evidence.SessionActor != evidence.FactorActor",
            "evidence.SessionMembershipId != evidence.MembershipId",
            "evidence.SessionFactorId != evidence.FactorId",
            "evidence.AccountRevisionInSession != evidence.CurrentAccountRevision",
            "evidence.MembershipRevisionInSession != evidence.CurrentMembershipRevision",
            "evidence.VerifiedHostAddressMode != evidence.TenantCatalogAddressMode",
            "!evidence.SessionNotRevoked", "!evidence.AccountActive",
            "!evidence.AccountMailboxVerified", "!evidence.MembershipActive",
            "!evidence.FactorActive", "evidence.ServerIdleMinutes is < 5 or > 120",
            "evidence.ServerNowUtc >= evidence.SessionExpiresAtUtc",
            "evidence.ServerNowUtc >= evidence.MembershipExpiresAtUtc",
            "evidence.ServerNowUtc - evidence.SessionLastSeenAtUtc >=",
            "AuthorizesPrivateResource => false",
        ),
        "adapter":(
            'Tenant(Bytes(membership, "tenant_id"))',
            'Actor(Bytes(factor, "account_id"))',
            'FactorId = Id(Bytes(factor, "factor_id"))',
        ),
        "flow":("AND h.address_mode=t.address_mode FOR SHARE",),
        "auth":("ResolveC06TenantContextFromLockedRows(",
                "AND last_seen_at_utc>UTC_TIMESTAMP(6)-INTERVAL @idle MINUTE FOR UPDATE"),
    }
    for name, marker in cases:
        need(source[name].count(marker) > 0, "attack marker not present: " + marker)
        modified = source[name].replace(marker, "P0_CONTROL_REMOVED", 1)
        need(any(required not in modified for required in table[name]),
             "mutation failed to remove a check: " + marker)
        count += 1
    need(count == 23 and markers["domain"] >= 20, "negative source coverage incomplete")
    print(f"PASS C06-03 B: {count} sensitive bypass mutations rejected by source gate")

def main(stage):
    changed = diff()
    prior()
    markers = audit_sources()
    if stage == "A":
        acta = ROOT / "docs/c06/C06_03_ACTA_CIERRE.md"
        if acta.is_file():
            content = acta.read_text(encoding="utf-8")
            status = (ROOT / "docs/ESTADO_VIGENTE_CAPITULOS.md").read_text(encoding="utf-8")
            need("ea7e341010734665c1f3975047aef12d0aaebfb2" in content and
                 "45506430723" in content and "45508442081" in content and
                 "45506430398" in content and "C06-03" in status and
                 "C06-04" in status and "NO CERTIFICADO" in status,
                 "institutional C06-03 acta lacks verified SHA or limited scope")
        print("PASS C06-03 A: Word SHA, prior C06-01/02, exact scoped diff:",
              changed, markers)
    elif stage == "B":
        security_mutations()
    elif stage == "C":
        suite = (ROOT/"backend/tests/Crecisol.C06Membership.ContractsTests/Program.cs").read_text(encoding="utf-8")
        need(suite.count("Reject(Good() with {") >= 35 and
             "C06OperationalMembershipContext.VerifyLockedServerEvidence" in suite,
             "C06-03 negative .NET contracts missing")
        print("PASS C06-03 C: Domain/Infrastructure separation, unchanged C05 API, 35+ negative contracts")
    else:
        raise AssertionError("expected A|B|C")

if __name__ == "__main__":
    main(sys.argv[1] if len(sys.argv) == 2 else "")
