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
CATALOG = ROOT / "backend/contracts/authorization/c06_01_actions.json"
WORD = ROOT / "docs/CRECISOL_DOCUMENTO_MAESTRO_UNICO.docx"
WORD_SHA256 = "ea5afc89e74915a9b18b9753ddb832f4be8e5d00f5e66b0f008e02e03476444c"
PRE_TENANT = set("login mfa/enroll mfa/verify recovery/request recovery/complete memberships/request passkeys/options passkeys/login terms invitations/accept pairings/start pairings/complete incidents/report assisted/request assisted/complete assisted/handoff-complete".split())
PRE_PLATFORM = set("platform/login platform/mfa platform/operator-recovery/request platform/operator-recovery/complete platform/operator-recovery/enroll platform/incidents/report".split())
ELEVATED_TENANT = set("memberships/pending memberships/decide invitations/issue private/read".split())
ELEVATED_PLATFORM = set("platform/case platform/cases/find platform/abuse-summary platform/review platform/release-share platform/session-policy/update".split())
TENANT_SESSIONS = set("sessions sessions/keepalive sessions/rotate sessions/revoke sessions/revoke-all logout browser/restore".split())
PLATFORM_SESSIONS = set("platform/session platform/restore platform/logout platform/sessions platform/sessions/keepalive platform/sessions/rotate platform/sessions/revoke platform/sessions/revoke-all platform/session-policy platform/session-policy/update".split())

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
    return len(found), len(rows), sum(x["registered_in_product_api"] for x in rows), len(internal)

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
    chapter = " ".join(normal(p) for p in paragraphs[start[0]:end[0]])
    need("autorizacion" in chapter and "privacidad" in chapter, "C06 Word chapter mismatch")
    return len(paragraphs[start[0]:end[0]])

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
        print("PASS C06-01 C: modular inventory only; no authorization code or permission assignments")
    else:
        raise SystemExit("Usage: python3 qa/c06_01_permissions_catalog_audit.py A|B|C")

if __name__ == "__main__":
    run(sys.argv[1] if len(sys.argv) == 2 else "")
