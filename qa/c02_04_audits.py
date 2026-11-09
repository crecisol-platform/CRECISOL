#!/usr/bin/env python3
"""C02-04: real read-only tests for an INACTIVE private-navigation design.

Audits A/B/C and negative mutations validate the pinned source, JSON graph,
security obligations and preservation of public-only Angular routes.
They DO NOT constitute actual private RBAC, BOLA/IDOR or MFA execution.
"""
from copy import deepcopy
import json
from pathlib import Path
import re
import sys

import c02_01_source
import c02_02_audits
import c02_03_audits

ROOT = Path(__file__).resolve().parents[1]
DATA = ROOT / "docs/c02/C02_04_NAVEGACION_PRIVADA.json"
SPEC = ROOT / "docs/c02/C02_04_ESPECIFICACION.md"
ACTA = ROOT / "docs/c02/C02_04_ACTA_MICROFASE.md"
PLAN = ROOT / "docs/C02_PREPARACION.md"
ROUTES = ROOT / "frontend/src/app/app.routes.ts"
PARENT = ROOT / "docs/c02/C02_02_MAPA_NECESIDADES.json"
PUBLIC = ROOT / "docs/c02/C02_03_SITEMAP.json"
PUBLIC_TEMPLATE = ROOT / "frontend/src/app/pages/public-information.component.html"
HOME = ROOT / "frontend/src/app/pages/platform-home.component.html"
EXPECTED_SHA = c02_01_source.EXPECTED_SHA
EXPECTED_SCOPE = {
    "E03": ("PV01", "platform-private", "/consola"),
    "E04": ("PV02", "tenant-owner-private", "/negocio"),
    "E05": ("PV03", "tenant-employee-private", "/trabajo"),
    "E06": ("PV04", "tenant-consumer-private", "/mi-cuenta"),
}
REQUIRED_GATES = {
    "E03": {"identity", "session", "platform-permission", "action"},
    "E04": {"identity", "session", "verified-tenant", "membership",
            "action", "resource-ownership", "audit"},
    "E05": {"identity", "session", "verified-tenant", "membership",
            "branch-scope", "assignment", "action"},
    "E06": {"identity", "session", "verified-tenant",
            "consumer-membership", "action", "resource-ownership", "audit"},
}
PUBLIC_PATHS = ["", "experiencias", "demostraciones", "alta-empresas", "**"]
ALLOWED_PRESENTATION = {
    "label", "description", "icon_token", "group", "order",
    "visibility", "locale", "help_text", "preferred_home",
}


def require(condition, message):
    if not condition:
        raise AssertionError(message)


def read_data():
    return json.loads(DATA.read_text(encoding="utf-8"))


def need_index():
    c02_01_source.original_parts()  # Real byte-exact Word SHA and chapter extraction.
    parent = c02_02_audits.read_data()
    c02_02_audits.valid_contract(parent)
    return {n["id"]: (e["id"], n) for e in parent["experiences"] for n in e["needs"]}


def checked_routes(source=None):
    src = ROUTES.read_text(encoding="utf-8") if source is None else source
    paths = re.findall(r"^\s*path: '([^']*)'", src, re.M)
    require(paths == PUBLIC_PATHS,
            "Private/duplicate/unknown Angular route introduced or wildcard changed")
    require("NotFoundComponent" in src and
            src.index("path: '**'") < src.index("NotFoundComponent"),
            "Unknown paths must use the existing NotFoundComponent")
    require(src.count("loadComponent:") == len(PUBLIC_PATHS),
            "Unexpected component loader/privileged frontend")
    require(not re.search(r"\bcan(?:Activate|Match|Load)\s*:", src),
            "An Angular guard cannot stand in for private backend authorization")
    return paths


def valid_contract(data, parent_needs=None):
    require(isinstance(data, dict), "JSON root not an object")
    require(data.get("artifact") == "CRECISOL-C02-04-PRIVATE-NAVIGATION-DESIGN",
            "Unexpected navigation contract artifact")
    require(data.get("schema_version") == 1, "Contract schema changed")
    require(data.get("delivery_state") ==
            "design-only-no-private-routes-no-authorization-implemented",
            "Private features cannot be claimed implemented")
    src = data.get("source", {})
    require(src.get("sha256") == EXPECTED_SHA and
            src.get("master") == "docs/CRECISOL_DOCUMENTO_MAESTRO_UNICO.docx" and
            src.get("chapter") == "02", "Canonical Word source not pinned")
    require(src.get("operative_microphase") == "C02-04" and
            src.get("normative_process_gates") == "MAE-C02-01..08",
            "Confused process gates with operative microphase")
    for name, expected in {
        "inventory": "docs/c02/C02_01_ESPECIFICACION.md",
        "needs": "docs/c02/C02_02_MAPA_NECESIDADES.json",
        "public_sitemap": "docs/c02/C02_03_SITEMAP.json",
        "previous_acta": "docs/c02/C02_03_ACTA_MICROFASE.md",
        "previous_dev_sha": "e6329e874ae44721dda0bfa2eb9a30acfaf0b990",
    }.items():
        require(src.get(name) == expected, f"Missing C02-03 gate: {name}")

    inv = data.get("security_invariants", {})
    for key, value in {
        "authorization": "backend-deny-by-default",
        "login": "automatic-context-without-tenant-picker",
        "identity": "server-validates-session-revocation-and-identity",
        "tenant": "server-verifies-host-membership-tenant-branch-action-resource",
        "database": "server-only-catalog-chooses-shared-or-dedicated-storage",
        "mismatch": "deny-and-never-fallback-to-other-tenant",
        "frontend_guard": "UX-hint-only-not-access-control",
        "platform_admin": "global-configuration-no-private-PII-by-default",
        "exceptional_access": "step-up-MFA-reason-short-expiry-audit-revocable",
        "consumer": "only-own-tenant-and-own-private-resources",
        "tenant_public": "verified-host-and-published-only",
        "demo": "synthetic-only-never-real-tenant-data",
        "audit": "per-tenant-without-cross-tenant-read",
        "cache": "tenant-branch-resource-user-identity-scoped-no-stale-permission-reuse",
        "no_real_charges": True, "no_tenant_selector": True,
        "no_active_private_routes": True,
    }.items():
        require(inv.get(key) == value, f"Security invariant downgraded: {key}")

    config = data.get("parameterization", {})
    require(config.get("state") == "future-admin-managed-server-validated-versioned",
            "No admin-managed versioned future state")
    require(config.get("scopes") == ["platform", "tenant", "branch", "user"],
            "Navigation configurability must be scoped and versioned")
    fields = config.get("editable_presentation_fields")
    require(isinstance(fields, list) and len(fields) == len(ALLOWED_PRESENTATION)
            and set(fields) == ALLOWED_PRESENTATION,
            "All presentation fields must be configurable safely without executable extras")
    require(config.get("secure_editable_permissions") ==
            "authorized-role-permission-matrix-in-backend-with-minimum-privilege-and-audit",
            "Role-permission configuration must be secure, backend and audited")
    require(config.get("invariant_fields_never_user_supplied") == [
        "route_key", "backend_policy_identifier", "tenant_scope",
        "resource_ownership", "verified_host_mapping"
    ], "A tenant/user cannot configure resource trust or arbitrary route keys")
    require(config.get("config_contract") ==
            "versioned-preview-approval-publish-audit-rollback",
            "Administrative configuration lacks safe publishing and rollback")
    require(config.get("ui_input_validation") ==
            "structured-data-allowlisted-icon-token-no-arbitrary-JS-HTML-CSS-SQL",
            "Arbitrary executable content cannot be menu configuration")
    require(config.get("effective_visibility") ==
            "server-filtered-by-session-context-and-permission-then-user-preference",
            "Menu visibility must be filtered by backend before personalization")
    require(config.get("no_client_permission_grants") is True and
            config.get("no_unversioned_hardcoded_business_content") is True,
            "Navigation configuration cannot authorize or hardcode business content")

    contexts = data.get("contexts")
    require(isinstance(contexts, list) and len(contexts) == 4,
            "Four distinct private-context designs required")
    require([c.get("experience") for c in contexts] == list(EXPECTED_SCOPE),
            "Missing or reordered E03..E06 experiences")
    ids, needs, route_keys, proposed_paths = [], [], [], []
    if parent_needs is None:
        parent_needs = need_index()
    for exp, ctx in zip(EXPECTED_SCOPE, contexts):
        identifier, scope, root = EXPECTED_SCOPE[exp]
        require(ctx.get("id") == identifier and ctx.get("scope") == scope
                and ctx.get("proposed_root") == root,
                f"Conflicting owner/employee/consumer/platform scope for {exp}")
        require(isinstance(ctx.get("actor"), str) and len(ctx["actor"]) >= 9,
                "Context actor label missing")
        require(ctx.get("activation") == "blocked-until-real-backend-auth" and
                ctx.get("delivery_state") == "design-only-not-routable",
                "A private shell was prematurely enabled")
        nav = ctx.get("navigation")
        require(isinstance(nav, list) and len(nav) == 3,
                f"Exactly three needs/tasks per private-context experience: {exp}")
        for node in nav:
            ids.append(node.get("id"))
            need_ref = node.get("need_ref")
            needs.append(need_ref)
            require(need_ref in parent_needs and parent_needs[need_ref][0] == exp,
                    f"Task not mapped to actual C02-02 need in {exp}")
            prior = parent_needs[need_ref][1]
            require(node.get("source_refs") == prior["source_refs"] and
                    node.get("dependency_chapters") == prior["dependencies"] and
                    node.get("acceptance") == prior["acceptance"] and
                    node.get("risk") == "P0",
                    f"Task {need_ref} lost parent sources/dependencies/acceptance/risk")
            require(node.get("visibility") ==
                    "server-filtered-navigation-is-not-authorization" and
                    node.get("delivery_state") == "not-implemented",
                    "Visible menu was incorrectly equated with authorization")
            negative = node.get("negative_case")
            require(isinstance(negative, str) and len(negative.strip()) >= 55,
                    f"Task {need_ref} missing negative security outcome")
            purpose = node.get("purpose_note_for_design_only")
            require(isinstance(purpose, str) and len(purpose.strip()) >= 20,
                    f"Task {need_ref} missing future navigation intent")
            key = node.get("route_key")
            path = node.get("proposed_path")
            require(isinstance(key, str) and
                    re.fullmatch(r"[a-z]+(?:\.[a-z-]+)+", key),
                    f"Unsafe route_key: {key}")
            require(isinstance(path, str) and
                    re.fullmatch(r"/[a-z-]+(?:/[a-z-]+)+", path) and
                    path.startswith(root + "/") and
                    path[1:] not in PUBLIC_PATHS,
                    "A future private route escaped its fixed context or used dynamic input")
            route_keys.append(key)
            proposed_paths.append(path)
            checks = node.get("required_backend_checks")
            require(isinstance(checks, list) and len(checks) == len(set(checks)),
                    f"Duplicate/missing backend checks for {need_ref}")
            if need_ref == "N16":
                require(node.get("boundary") == "verified-tenant-public-future",
                        "Public tenant catalog wrongly promoted to private privilege")
                require(set(checks) == {
                    "verified-host", "published-content",
                    "no-private-pii", "no-cross-tenant-redirect"
                }, "A public catalog must only expose verified published data")
            else:
                require(node.get("boundary") == "backend-private",
                        "Private resource incorrectly classified as public")
                require(REQUIRED_GATES[exp].issubset(set(checks)),
                        f"Backend membership/action/ownership missing for {need_ref}")
            if need_ref == "N09":
                require({"step-up-mfa", "reason", "short-expiry",
                         "audit", "revocable"}.issubset(checks),
                        "SuperAdmin exceptional private data step-up missing")
            if need_ref in ("N11", "N12"):
                require("branch-scope" in checks,
                        "Owner resources must be constrained to authorized branch")
            if need_ref == "N14":
                require({"immutable-history", "resource-ownership", "audit"}.issubset(checks),
                        "Cannot silently modify staff task history")
            if need_ref == "N15":
                require("revocation-check" in checks, "Staff session revocation omitted")
            if need_ref == "N17":
                require("consent-version" in checks,
                        "Consumer acceptance must be tied to contract version")
    require(ids == [f"NAV{i:02d}" for i in range(1, 13)] and
            needs == [f"N{i:02d}" for i in range(7, 19)],
            "All twelve N07..N18 must be covered exactly once, in order")
    require(len(set(route_keys)) == 12 and len(set(proposed_paths)) == 12,
            "Duplicated UI route or capability can create authorization confusion")

    denials = data.get("cross_cutting_denials")
    require(isinstance(denials, list) and len(denials) == 12 and
            [d.get("id") for d in denials] ==
            [f"DEN{i:02d}" for i in range(1, 13)],
            "Twelve cross-cutting future security denials required")
    for d in denials:
        for field in ("attack", "expected"):
            require(isinstance(d.get(field), str) and len(d[field]) >= 32,
                    f"Missing human-readable denial {d.get('id')}: {field}")
    limits = data.get("implementation_limits", {})
    for key in ("front_end_routes_activated", "backend_private_endpoints_created",
                "database_migrations"):
        require(limits.get(key) == [], f"Unexpected implementation: {key}")
    require(limits.get("payment_processing") is False,
            "No real charging may be implemented in C02-04")
    require("C05" in limits.get("planned_chapters", []) and
            "C06" in limits.get("planned_chapters", []),
            "Actual backend authentication and authorization are deferred")
    require(isinstance(limits.get("future_tests"), str) and
            "BOLA" in limits["future_tests"] and "MFA" in limits["future_tests"],
            "Future private API tests must not be falsely certified")
    return contexts


def reject_mutation(data, mutator, label, needs=None):
    changed = deepcopy(data)
    mutator(changed)
    try:
        valid_contract(changed, parent_needs=needs)
    except (AssertionError, KeyError, TypeError, ValueError):
        return
    raise AssertionError(f"Adversarial mutation wrongly accepted: {label}")


def audit_a():
    indexed = need_index()
    c02_03_audits.validate(c02_03_audits.sitemap())
    data = read_data()
    valid_contract(data, indexed)
    checked_routes()
    doc = SPEC.read_text(encoding="utf-8")
    for marker in ("C02-04", EXPECTED_SHA, "3/9", "NO CERTIFICADO", "MAE-C02-01..08",
                   "T01-R02", "T01-R03", "T01-R04", "T01-R05",
                   "P01", "P06", "P08", "P09", "P11",
                   "N07", "N18", "DEN01", "DEN12", "C02-05"):
        require(marker in doc, f"Missing document source/phase marker: {marker}")
    reject_mutation(data, lambda d: d["contexts"][2]["navigation"][0].update(
        need_ref="N07"), "wrong persona E05->E03", indexed)
    reject_mutation(data, lambda d: d["contexts"][1]["navigation"][0].update(
        source_refs=["P99"]), "invented canonical source", indexed)
    reject_mutation(data, lambda d: d["contexts"][0]["navigation"][0].update(
        id="NAV02"), "duplicate nav ID", indexed)
    reject_mutation(data, lambda d: d["contexts"][1]["navigation"][0].update(
        proposed_path="/consola/resumen"), "tenant task mounted in global console", indexed)
    reject_mutation(data, lambda d: d["contexts"][0]["navigation"].pop(),
                    "missing canonical need", indexed)
    print("AUDIT A PASS C02-04: pinned Word, E03-E06, N07-N18, 12 tasks,")
    print("  C02-02 needs and C02-03 4-route source; 5 traceability mutations rejected.")


def audit_b():
    needs = need_index()
    data = read_data()
    valid_contract(data, needs)
    paths = checked_routes()
    require(paths[-1] == "**", "Private route wildcard removed")
    src = ROUTES.read_text(encoding="utf-8")
    public_html = (PUBLIC_TEMPLATE.read_text(encoding="utf-8") +
                   HOME.read_text(encoding="utf-8"))
    for ctx in data["contexts"]:
        for item in ctx["navigation"]:
            require(item["proposed_path"] not in src and
                    item["proposed_path"] not in public_html,
                    "Design-only private route became a public entrypoint")
    for token in ("X-Tenant-Id", "canActivate:", "canMatch:", "localStorage",
                  "sessionStorage", "bypassSecurityTrust", "innerHTML"):
        require(token not in src + public_html,
                f"Browser/menu must not grant access via {token}")
    reject_mutation(data, lambda d: d["security_invariants"].update(
        authorization="angular-guard"), "frontend authorization as backend", needs)
    reject_mutation(data, lambda d: d["security_invariants"].update(
        no_tenant_selector=False), "selector during normal login", needs)
    reject_mutation(data, lambda d: d["contexts"][0]["navigation"][2].update(
        required_backend_checks=["identity", "session", "platform-permission", "action"]),
        "SuperAdmin MFA reason expiry bypass", needs)
    reject_mutation(data, lambda d: d["contexts"][1]["navigation"][0].update(
        required_backend_checks=["identity", "session", "verified-tenant", "membership",
                                 "action", "audit"]), "tenant resource ownership removed", needs)
    reject_mutation(data, lambda d: d["contexts"][3]["navigation"][0].update(
        boundary="backend-private"), "consumer public catalog private privilege", needs)
    reject_mutation(data, lambda d: d["parameterization"].update(
        no_client_permission_grants=False), "client grants backend role", needs)
    reject_mutation(data, lambda d: d["implementation_limits"].update(
        front_end_routes_activated=["/consola"]), "private shell before auth", needs)
    reject_mutation(data, lambda d: d["security_invariants"].update(
        database="client-header-picks-dsn"), "tenant-controlled database routing", needs)
    reject_mutation(data, lambda d: d["contexts"][2]["navigation"][1].update(
        required_backend_checks=["identity", "session", "verified-tenant", "membership",
                                 "branch-scope", "assignment", "action"]),
        "staff task history without immutable audit", needs)
    print("AUDIT B PASS C02-04: zero active private routes, backend-only policy,")
    print("  SuperAdmin MFA, shared/dedicated isolation and nine adversarial mutations rejected.")
    print("LIMIT: These are design and public-route checks, NOT live private BOLA/IDOR/MFA tests.")


def audit_c():
    needs = need_index()
    data = read_data()
    valid_contract(data, needs)
    checked_routes()
    plan = PLAN.read_text(encoding="utf-8")
    if ACTA.exists():
        acta = ACTA.read_text(encoding="utf-8")
        fifth_acta_path = ROOT / "docs/c02/C02_05_ACTA_MICROFASE.md"
        if fifth_acta_path.exists():
            fifth = fifth_acta_path.read_text(encoding="utf-8")
            sixth_path = ROOT / "docs/c02/C02_06_ACTA_MICROFASE.md"
            if sixth_path.exists():
                sixth = sixth_path.read_text(encoding="utf-8")
                require("6/9" in plan and
                        "C02-06 CERRADA TÉCNICAMENTE" in sixth and
                        "d82cfc0a57561f21266a1f3e2e8c328dd3c4aeb4" in sixth and
                        all(run in sixth for run in (
                            "42649577691", "42649577850", "42649577851",
                            "42649577513", "42649577478", "42649577726",
                            "42649577757", "42649577427")),
                        "C02-06 advancement requires exact technical SHA and eight postmerge runs")
            else:
                require("5/9" in plan,
                        "C02-05 must remain 5/9 until C02-06 acta")
            require("C02-05" in plan and
                    "C02-05 CERRADA TÉCNICAMENTE" in fifth and
                    "24e3b3c73133a35bdb336269517f123765734e86" in fifth and
                    all(run in fifth for run in (
                        "42639836939", "42639836403", "42639836651",
                        "42639836641", "42639836721", "42639836413")),
                    "C02-05 closure requires its six real postmerge runs")
        else:
            require("4/9" in plan,
                    "C02-04 closure must remain 4/9 until C02-05 acta")
        require("C02-04 CERRADA TÉCNICAMENTE" in acta and
                "POSTMERGE" in acta and "C02-05" in acta and
                "ecb7807e79427d5059c8b5f4ee0c5dd2195b7cb5" in acta and
                all(run in acta for run in (
                    "42630487933", "42630487964", "42630487996",
                    "42630487889", "42630487985")),
                "C02-04 provenance requires technical SHA and five postmerge runs")
    else:
        require("3/9" in plan and "C02-04" in plan,
                "Cannot advance to 4/9 without an audited acta")
    require("NO CERTIFICADO" in plan and "1/37" in plan and "C03 bloqueado" in plan,
            "Chapter 02 or 03 prematurely certified")
    for target in (SPEC, PLAN):
        for link in re.findall(r"\[[^\]]+\]\(([^)]+)\)",
                               target.read_text(encoding="utf-8")):
            if link.startswith(("http:", "https:", "mailto:", "#")):
                continue
            dest = (target.parent / link.split("#", 1)[0]).resolve()
            require(dest.is_relative_to(ROOT) and dest.is_file(),
                    f"Invalid local Markdown link: {link}")
    require("prefers-reduced-motion" in
            (ROOT / "frontend/src/styles.css").read_text(encoding="utf-8"),
            "Existing public UX reduced-motion fallback missing")
    reject_mutation(data, lambda d: d["parameterization"].update(
        editable_presentation_fields=["label", "icon_token"]), "missing admin-configurable UI fields", needs)
    reject_mutation(data, lambda d: d["parameterization"].update(
        config_contract="unversioned-user-html"), "no preview/versioned publish/rollback", needs)
    reject_mutation(data, lambda d: d["cross_cutting_denials"].pop(),
                    "lost one of 12 threat scenarios", needs)
    reject_mutation(data, lambda d: d["security_invariants"].update(
        no_active_private_routes=False), "premature private route", needs)
    print("AUDIT C PASS C02-04: 4-context navigation, versioned admin configuration,")
    print("  no private UI, public UX regression gates, four adverse config mutations rejected.")
    print("LIMIT: Angular/.NET builds and real browser 404 tests are separate CI steps.")


def main():
    require(len(sys.argv) == 2 and sys.argv[1] in ("A", "B", "C"),
            "Usage: python3 qa/c02_04_audits.py A|B|C")
    {"A": audit_a, "B": audit_b, "C": audit_c}[sys.argv[1]]()


if __name__ == "__main__":
    try:
        main()
    except (AssertionError, ValueError, KeyError, TypeError, OSError) as exc:
        print(f"FAIL C02-04: {exc}", file=sys.stderr)
        sys.exit(1)
