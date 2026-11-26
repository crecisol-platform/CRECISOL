#!/usr/bin/env python3
"""C06-01: gate de inventario HTTP y C04 interno. NO autoriza solicitudes."""
import copy
import hashlib
import json
import re
import sys
import unicodedata
import zipfile
import xml.etree.ElementTree as ET
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
SOURCE = "backend/src/Crecisol.Api/Modules/Identity/OperationalIdentityModule.cs"
PROGRAM = "backend/src/Crecisol.Api/Program.cs"
CATALOG = ROOT / "docs/c06/C06_01_CATALOGO_ACCIONES.json"
WORD = ROOT / "docs/CRECISOL_DOCUMENTO_MAESTRO_UNICO.docx"
WORD_SHA256 = "ea5afc89e74915a9b18b9753ddb832f4be8e5d00f5e66b0f008e02e03476444c"
PRE_TENANT = set("login mfa/enroll mfa/verify recovery/request recovery/complete memberships/request passkeys/options passkeys/login terms invitations/accept pairings/start pairings/complete incidents/report assisted/request assisted/complete assisted/handoff-complete".split())
PRE_PLATFORM = set("platform/login platform/mfa platform/operator-recovery/request platform/operator-recovery/complete platform/operator-recovery/enroll platform/incidents/report".split())
ELEVATED_TENANT = set("memberships/pending memberships/decide invitations/issue private/read".split())
ELEVATED_PLATFORM = set("platform/case platform/cases/find platform/abuse-summary platform/review platform/release-share platform/session-policy/update".split())
TENANT_SESSIONS = set("sessions sessions/keepalive sessions/rotate sessions/revoke sessions/revoke-all logout browser/restore".split())
PLATFORM_SESSIONS = set("platform/session platform/restore platform/logout platform/sessions platform/sessions/keepalive platform/sessions/rotate platform/sessions/revoke platform/sessions/revoke-all platform/session-policy platform/session-policy/update".split())

WORD_CASE_ACTIONS = set("cases.read note.create diagnosis.publish quote.request approval.decide delivery.confirm case.close dispute.manage".split())
EXPECTED_NORMATIVE = WORD_CASE_ACTIONS | set(("finance.revenue.read finance.margin.read finance.cash.manage finance.refund.issue finance.report.export "
    "privacy.subject.access privacy.subject.rectify privacy.subject.erase privacy.subject.object "
    "authorization.role.assign authorization.role.revoke authorization.role.delegate "
    "platform.support.metadata.read platform.breakglass.open platform.breakglass.read platform.breakglass.close "
    "media.private.read media.private.export realtime.channel.subscribe realtime.channel.publish").split())
TEN_CROSS = ["TEN-001", "TEN-002", "TEN-008", "TEN-009", "TEN-010", "TEN-021", "TEN-032"]

def need(value, reason):
    if not value:
        raise AssertionError("C06-01 NO-GO: " + reason)

def operations(source):
    a = "private static readonly IReadOnlyDictionary<string, string[]> Operations"
    b = "public static IServiceCollection"
    need(source.count(a) == 1 and source.count(b) == 1, "source operation bounds")
    block = source.split(a, 1)[1].split(b, 1)[0]
    matches = re.findall(r'\["([^"]+)"\]\s*=\s*\[([^\]]*)\]', block)
    need(len(matches) == len(set(name for name, _ in matches)) and len(matches) >= 1, "duplicate or empty API operations")
    return {name: re.findall(r'"([^"]+)"', fields) for name, fields in matches}

def boundary(op):
    if op.startswith("platform/"):
        if op in PRE_PLATFORM: return "platform_recovery_or_challenge"
        if op in ELEVATED_PLATFORM: return "platform_privileged"
        if op in PLATFORM_SESSIONS: return "platform_session"
        return "platform_operator"
    if op in PRE_TENANT: return "tenant_preauth_or_proof"
    if op in ELEVATED_TENANT:
        return "tenant_branch_resource_grant" if op == "private/read" else "tenant_approver_grant"
    if op in TENANT_SESSIONS: return "tenant_own_session"
    return "tenant_own_account"

STANDALONE = {
    ("GET", "/api/v1/public/branding/platform"):
        ("public", True, "always_with_verified_host_and_readonly_DSN", PROGRAM, "anonymous_public_projection"),
    ("POST", "/api/v1/identity/registration-intents"):
        ("tenant", True, "C05_REGISTRATION_ENABLED=true",
         "backend/src/Crecisol.Api/Modules/Identity/NeutralRegistrationModule.cs", "tenant_preauth_or_proof"),
    ("POST", "/api/v1/identity/registration-proofs/confirm"):
        ("tenant", True, "C05_REGISTRATION_ENABLED=true_and_completion_disabled",
         "backend/src/Crecisol.Api/Modules/Identity/OperationalRegistrationProofModule.cs", "tenant_preauth_or_proof"),
    ("POST", "/api/v1/identity/registration-completions"):
        ("tenant", True, "C05_LOCAL_ACCOUNT_COMPLETION_ENABLED=true",
         "backend/src/Crecisol.Api/Modules/Identity/OperationalRegistrationCompletionModule.cs", "tenant_preauth_or_proof"),
    ("GET", "/api/v1/private/cases/{resourceId}"):
        ("tenant", False, "disposable_harness_only_not_in_Program_cs",
         "backend/src/Crecisol.Api/Modules/Identity/PrivateResourceReadModule.cs", "tenant_branch_resource_grant"),
}


def verify_exception_overlay(source):
    """Explicit successor inventory; the original 80/12/28 is historical."""
    marker = "private static readonly IReadOnlyDictionary<string, string[]> C06BreakglassOperations"
    overlay_path = ROOT / "docs/c06/C06_06_CATALOGO_ACTIVACION.json"
    if marker not in source:
        need(not overlay_path.exists(), "exception overlay without registered surface")
        return
    need(source.count(marker) == 1 and overlay_path.is_file(), "exception surface missing reviewed overlay")
    block = source.split(marker, 1)[1].split("public static void MapLocalOperationalIdentity", 1)[0]
    pairs = re.findall(r'\["([^"]+)"\]\s*=\s*\[([^\]]*)\]', block)
    actual = {name: re.findall(r'"([^"]+)"', fields) for name, fields in pairs}
    expected = {"platform/breakglass/open":["tenantId","branchId","resourceId","reason","code"],"platform/breakglass/approve":["exceptionId","tenantId","branchId","resourceId","reason","reviewReason","code"],"platform/breakglass/read":["exceptionId","code"],"platform/breakglass/close":["exceptionId"]}
    need(len(pairs) == len(expected) and actual == expected, "exception route contract drift")
    overlay = json.loads(overlay_path.read_text(encoding="utf-8"))
    need(overlay["authority"] == "DOCUMENTATION_ONLY_NOT_RUNTIME_AUTHORIZATION" and
         overlay["feature_gate"] == "CRECISOL_C06_BREAKGLASS_ENABLED=true_and_C05_LOCAL_IDENTITY_ENABLED=true_and_local_isolation",
         "exception feature gate changed")
    need(overlay["activated_normative"] == ["platform.breakglass.open","platform.breakglass.read","platform.breakglass.close"],
         "unapproved normative action activation")
    need(len(overlay["actions"]) == len(expected), "exception action count")
    seen = set()
    for row in overlay["actions"]:
        op = row["operation_code"]
        need(op in expected and op not in seen and row["required_fields"] == expected[op] and
             row["action_id"] == op.replace("/", ".") and row["http_method"] == "POST" and
             row["http_route"] == "/api/v1/platform-identity/" + op[9:] and row["realm"] == "platform" and
             row["access_boundary"] == "platform_temporary_exception" and row["source_file"] == SOURCE and
             row["authorization_from"] == "backend_only" and row["general_role_grantable"] is False and
             row["registered_in_local_api"] is True and row["ui_grant_source"] is False,
             "exception overlay boundary drift")
        seen.add(op)
    need(seen == set(expected), "exception routes missing")
    need('Environment.GetEnvironmentVariable("CRECISOL_C06_BREAKGLASS_ENABLED") == "true"' in source and
         "foreach (var operation in C06BreakglassOperations.Keys)" in source and
         'endpoints.MapPost("/api/v1/platform-identity/" + selected[9..]' in source and
         "expectedFields" in source and 'context.Response.Headers.CacheControl = "no-store, private"' in source,
         "exception registration or privacy drift")

def verify_delegation_overlay(source):
    """C06-11 is an explicit isolated successor, never a normative grant."""
    path=ROOT/"docs/c06/C06_11_CATALOGO_DELEGACION_V1.json"
    marker="foreach (var operation in C06DelegationHttpContract.Operations)"
    contract_path=ROOT/"backend/src/Crecisol.Api/Contracts/C06DelegationHttpContract.cs"
    if marker not in source:
        need(not path.exists() and not contract_path.exists(),"delegation overlay without route registration")
        return
    need(source.count(marker)==1 and path.is_file() and contract_path.is_file(),"delegation overlay missing")
    contract=contract_path.read_text(encoding="utf-8")
    pairs=re.findall(r'\["([^"]+)"\]\s*=\s*\[([^\]]*)\]',contract)
    actual={name:re.findall(r'"([^"]+)"',fields) for name,fields in pairs}
    expected={"delegation/prepare":["command","branchId","resourceId","membershipId","roleId","roleRevision","membershipRevision","ceilingRevision","grantRevision","expiresAtUtc","reason"],"delegation/confirm":["command","branchId","resourceId","membershipId","roleId","roleRevision","membershipRevision","ceilingRevision","grantRevision","expiresAtUtc","reason","challenge","code"],"delegation/read":["branchId","resourceId"]}
    responses={"delegation/prepare":["challenge","expiresAtUtc"],"delegation/confirm":["accepted"],"delegation/read":["reference"]}
    boundaries={"delegation/prepare":"tenant_administrator_exact_scope","delegation/confirm":"tenant_administrator_exact_scope_fresh_mfa","delegation/read":"tenant_recipient_exact_resource_live_provenance"}
    need(len(pairs)==3 and actual==expected,"delegation request inventory drift")
    data=json.loads(path.read_text(encoding="utf-8"))
    need(data["schema_version"]==1 and data["authority"]=="DOCUMENTATION_ONLY_NOT_RUNTIME_AUTHORIZATION" and
         data["base_operational_contracts"]==79 and data["activated_normative"]==[] and
         data["feature_gate"]=="CRECISOL_C06_DELEGATION_LAB_ENABLED=true_and_C05_LOCAL_IDENTITY_ENABLED=true_and_local_isolation",
         "delegation feature gate or premature normative activation")
    seen=set()
    for row in data["actions"]:
        op=row["operation_code"]
        need(op in expected and op not in seen and row["required_fields"]==expected[op] and
             row["response_fields"]==responses[op] and row["boundary"]==boundaries[op] and
             row["action_id"]=="identity."+op.replace("/",".") and row["http_method"]=="POST" and
             row["http_route"]=="/api/v1/identity/"+op and row["source_file"]==SOURCE and row["realm"]=="tenant" and
             row["authorization_from"]=="backend_only" and row["general_role_grantable"] is False and
             row["ui_grant_source"] is False and row["registered_in_local_api"] is True and
             row["denied_payload"]=="empty" and row["cache_control"]=="no-store, private",
             "delegation route/response boundary drift")
        seen.add(op)
    need(seen==set(expected),"delegation routes missing")
    need(data["negative_scenarios"]==["anonymous","forged_bearer","wrong_host","injected_tenant","extra_field","duplicate_field","wrong_origin","browser_without_csrf"],"delegation negative matrix drift")
    for token in ('if (C06DelegationHttpContract.Enabled)',marker,'endpoints.MapPost("/api/v1/identity/" + selected,',
                  'if (delegated && !C06DelegationHttpContract.Enabled) return Results.NotFound();',
                  'C06DelegationHttpContract.TryApply(operation, result, out var delegationSafe)',
                  'browserTransport.Receipt(context, operation, delegationSafe, browserSession)'):
        need(token in source,"delegation transport gate missing "+token)
    need(source.index("C06DelegationHttpContract.TryApply(operation, result, out var delegationSafe)")<
         source.index("Results.Ok(delegationSafe.Value)"),"delegation raw response bypass")
    need('Environment.GetEnvironmentVariable("CRECISOL_C06_DELEGATION_LAB_ENABLED") == "true"' in contract and
         "if(!result.Allowed) return true" in contract and "C06IdentityResponseProjection.TryApply(" in contract and
         "JsonSerializer.Serialize" not in contract,"delegation projection/privacy drift")

def verify_catalog(data, source, program):
    need(data.get("schema_version") == 1 and data.get("authority") == "DOCUMENTATION_ONLY_NOT_RUNTIME_AUTHORIZATION",
         "inventory masquerades as a runtime authorization service")
    need(data.get("grant_decisions", "").startswith("C06-02"), "early permission grant")
    need("foreach (var operation in Operations.Keys)" in source and "MapPost(selected.StartsWith" in source,
         "operation route resolver changed")
    need("if (operationalIdentityEnabled)" in program and "app.MapLocalOperationalIdentity();" in program,
         "local-only surface activation changed")
    need("app.MapMfaVerifiedPrivateResourceRead(" not in program,
         "harness private route now exposed in production composition")
    found = operations(source)
    verify_exception_overlay(source)
    verify_delegation_overlay(source)
    rows = data.get("api_actions")
    need(isinstance(rows, list), "missing catalog")
    ids, routes, codes, extras = set(), set(), set(), []
    for row in rows:
        aid, verb, path = row.get("action_id"), row.get("http_method"), row.get("http_route")
        need(isinstance(aid, str) and re.fullmatch(r"[a-z0-9_.-]+", aid), "invalid action id")
        need(aid not in ids, "duplicate action id")
        ids.add(aid)
        key = verb, path
        need(verb in ("GET", "POST") and isinstance(path, str) and path.startswith("/api/v1/"), "unknown route")
        need(key not in routes, "duplicate method/path")
        routes.add(key)
        need(row.get("authorization_from") == "backend_only" and row.get("ui_grant_source") is False and
             row.get("c06_policy_bound") is False, "UI-derived or premature permission")
        src = row.get("source_file", "")
        need(src.startswith("backend/src/Crecisol.Api/") and (ROOT / src).is_file(), "unknown API source")
        op = row.get("operation_code")
        if op is None:
            need(row.get("required_fields") == [] and row.get("code_field_present") is False,
                 "unknown standalone request contract")
            extras.append(row)
            continue
        need(op in found and op not in codes, "invented or duplicated operation")
        codes.add(op)
        is_platform = op.startswith("platform/")
        suffix = op[9:] if is_platform else op
        expected_path = ("/api/v1/platform-identity/" if is_platform else "/api/v1/identity/") + suffix
        need(key == ("POST", expected_path), "HTTP mapping drift: " + op)
        need(aid == ("platform." if is_platform else "identity.") + suffix.replace("/", "."),
             "unstable action id")
        need(src == SOURCE and row.get("required_fields") == found[op] and
             row.get("code_field_present") is ("code" in found[op]), "backend field schema mismatch")
        need(row.get("realm") == ("platform" if is_platform else "tenant") and
             row.get("access_boundary") == boundary(op), "actor/tenant/platform boundary confusion")
        need(row.get("registered_in_product_api") is True and
             row.get("enabled_when") == "C05_LOCAL_IDENTITY_ENABLED=true_and_local_isolation",
             "operational exposure misdeclared")
    need(codes == set(found), "missing or extra operational actions: " + str(sorted(set(found) - codes)))
    need(len(extras) == len(STANDALONE), "standalone routes missing")
    for row in extras:
        key = row["http_method"], row["http_route"]
        need(key in STANDALONE, "invented standalone route")
        realm, active, when, src, guard = STANDALONE[key]
        need((row["realm"], row["registered_in_product_api"], row["enabled_when"], row["source_file"],
              row["access_boundary"]) == (realm, active, when, src, guard), "standalone boundary drift")
        need(key[1] in (ROOT / src).read_text(encoding="utf-8"), "standalone route removed from code")
    for marker in ('app.MapGet("/api/v1/public/branding/platform"', "app.MapNeutralRegistrationIntent();",
                   "app.MapOperationalRegistrationProofConfirmation();",
                   "app.MapOperationalRegistrationCompletion();"):
        need(marker in program, "Program.cs route registration drift")
    # Block silent introduction of new Map* modules not covered by the inventory.
    found_files = set()
    for f in (ROOT / "backend/src/Crecisol.Api").rglob("*.cs"):
        if re.search(r"\bMap(Get|Post|Put|Patch|Delete)\s*\(", f.read_text(encoding="utf-8")):
            found_files.add(str(f.relative_to(ROOT)))
    allowed = {SOURCE, PROGRAM} | {value[3] for value in STANDALONE.values()}
    need(found_files <= allowed, "unreviewed API registration in " + repr(found_files - allowed))
    internal = data.get("non_http_contracts")
    need(isinstance(internal, list) and len(internal) == 12 and
         len({x["name"] for x in internal}) == len(internal), "C04 contract count or duplicates")
    for row in internal:
        src = row.get("source_file", "")
        need(row.get("http_exposed") is False and row.get("status") == "internal_contract_not_a_public_endpoint",
             "contract promoted to HTTP grant")
        need(src.startswith("backend/src/Crecisol.Application/Tenancy/") and (ROOT / src).is_file(),
             "invented C04 contract")

    contract = data.get("source_contract", {})
    need(contract.get("approved_word_sha256") == WORD_SHA256 and contract.get("chapter") == "C06"
         and contract.get("source_paragraph_count") == 70, "normative Word source identity/extent drift")
    need(contract.get("cross_tenant_controls") == TEN_CROSS and
         set(contract.get("universal_case_action_codes", [])) == WORD_CASE_ACTIONS, "omitted mandatory TEN/G7 controls")
    need(contract.get("canonical_action_verbs") ==
         ["view", "create", "update", "approve", "export", "refund", "soft_delete", "publish", "administer"],
         "C06 source permission verbs changed")
    normative = data.get("normative_actions")
    need(isinstance(normative, list) and
         {x["action_id"] for x in normative} == EXPECTED_NORMATIVE and
         len(normative) == len(EXPECTED_NORMATIVE), "missing, duplicated or invented normative action")
    for row in normative:
        action_id = row["action_id"]
        need(row.get("http_registered") is False and row.get("grantable") is False and
             row.get("status") == "normative_proposed_not_implemented" and
             row.get("source") == "approved_C06_Word_not_an_API_claim",
             "planned action promoted to executable grant " + action_id)
        need(row.get("realm") == ("platform" if action_id.startswith("platform.") else "tenant"),
             "planned platform/tenant scope confusion " + action_id)
        need(isinstance(row.get("required_attributes"), list) and len(row["required_attributes"]) >= 3,
             "planned action lacks explicit context dimensions " + action_id)
        source_locator = row.get("master_source_locator", "")
        need(re.fullmatch(r"C06_WORD_PARAGRAPH_\d\d", source_locator),
             "planned permission not traceable to the approved Word")
    return len(found), len(rows), sum(x["registered_in_product_api"] for x in rows), len(internal), len(normative)

def verify_word():
    need(WORD.is_file(), "canonical Word unavailable")
    digest = hashlib.sha256(WORD.read_bytes()).hexdigest()
    need(digest == WORD_SHA256, "canonical Word does not match approved SHA256")
    with zipfile.ZipFile(WORD) as z:
        root = ET.fromstring(z.read("word/document.xml"))
    ns = "{http://schemas.openxmlformats.org/wordprocessingml/2006/main}"
    paragraphs = ["".join(el.text or "" for el in p.iter(ns + "t")) for p in root.iter(ns + "p")]
    def normal(v):
        return "".join(c for c in unicodedata.normalize("NFKD", v).lower()
                       if not unicodedata.combining(c))
    start = [i for i, p in enumerate(paragraphs) if re.search(r"capitulo\s+0?6\s+de\s+37", normal(p))]
    end = [i for i, p in enumerate(paragraphs) if re.search(r"capitulo\s+0?7\s+de\s+37", normal(p))]
    need(start and end and start[0] < end[0], "C06 chapter not bounded in Word")
    chapter_paragraphs = paragraphs[start[0]:end[0]]
    chapter = " ".join(normal(p) for p in chapter_paragraphs)
    need("autorizacion" in chapter and "privacidad" in chapter, "C06 Word chapter mismatch")
    for action_id in WORD_CASE_ACTIONS:
        need(action_id in chapter, "Word C06 no longer identifies mandatory case action " + action_id)
    for keyword in ("condicion comercial", "separacion financiera", "permisos delegados",
                    "revocacion inmediata", "retencion y derechos", "break-glass",
                    "sucursal", "canales en tiempo real", "superadmin"):
        need(keyword in chapter, "C06 Word mandatory permission dimension missing: " + keyword)
    for control in TEN_CROSS:
        need(control.lower() in chapter, "Word no longer requires " + control)
    # This source is already a public repository document. Emit bounded excerpts
    # for human review of every paragraph before certifying C06-01 scope.
    for index, paragraph in enumerate(chapter_paragraphs):
        if paragraph.strip():
            print(f"C06-WORD-LINE {index:02d}: {paragraph.strip()[:350]}")
    return len(chapter_paragraphs)

def run(stage):
    data = json.loads(CATALOG.read_text(encoding="utf-8"))
    source = (ROOT / SOURCE).read_text(encoding="utf-8")
    program = (ROOT / PROGRAM).read_text(encoding="utf-8")
    stats = verify_catalog(data, source, program)
    if stage == "A":
        word_nodes = verify_word()
        print(f"PASS C06-01 A: canonical Word C06 ({word_nodes} paragraphs); mapped={stats}")
    elif stage == "B":
        mutations = []
        for mutate in (
            lambda d: d["api_actions"].pop(),
            lambda d: d["api_actions"].append(copy.deepcopy(d["api_actions"][0])),
            lambda d: next(r for r in d["api_actions"] if r["realm"] == "platform").update(realm="tenant"),
            lambda d: next(r for r in d["api_actions"] if r["operation_code"] == "private/read").update(access_boundary="tenant_own_account"),
            lambda d: d["api_actions"][0].update(ui_grant_source=True),
            lambda d: d["api_actions"][0].update(c06_policy_bound=True),
            lambda d: d["api_actions"][0].update(source_file="frontend/src/app/buttons.ts"),
            lambda d: next(r for r in d["api_actions"] if r["operation_code"] == "memberships/decide").update(required_fields=[]),
            lambda d: next(r for r in d["api_actions"] if r["http_route"].startswith("/api/v1/private/cases/")).update(registered_in_product_api=True),
            lambda d: d["normative_actions"].pop(),
            lambda d: d["normative_actions"][0].update(grantable=True),
            lambda d: d["normative_actions"][0].update(http_registered=True),
            lambda d: next(r for r in d["normative_actions"] if r["action_id"] == "cases.read").update(realm="platform"),
            lambda d: d["source_contract"]["cross_tenant_controls"].remove("TEN-032"),
        ):
            mutant = copy.deepcopy(data)
            mutate(mutant)
            try:
                verify_catalog(mutant, source, program)
            except AssertionError:
                mutations.append("REJECT")
                continue
            raise AssertionError("C06-01 NO-GO: forged permission catalog mutation accepted")
        for marker in ("StrictVerifiedHostTransport.TryReadDirectHost", "!System.Net.IPAddress.IsLoopback",
                       'h.StartsWith("X-Tenant"', 'h.StartsWith("X-Actor"'):
            need(marker in source, "transport/identity gate removed")
        print(f"PASS C06-01 B: {len(mutations)} hostile mutations rejected; no UI-derived grant")
    elif stage == "C":
        need(len({r["module"] for r in data["api_actions"]}) >= 10, "module coverage collapsed")
        need(not any(r["registered_in_product_api"] for r in data["api_actions"]
                     if r["source_file"].endswith("PrivateResourceReadModule.cs")), "test-only route exposed")
        need(all(not r["grantable"] and not r["http_registered"]
                 for r in data["normative_actions"]), "future permissions exposed without implementation")
        print("PASS C06-01 C: modular current + normative inventories; no runtime grants or implementation")
    else:
        raise SystemExit("Usage: python3 qa/c06_01_permissions_catalog_audit.py A|B|C")

if __name__ == "__main__":
    run(sys.argv[1] if len(sys.argv) == 2 else "")
