#!/usr/bin/env python3
"""CRECISOL C03-01: canonical Word and actual architecture inventory, A/B/C.

This gate tests the existing public slice and a proposed dependency map.
It NEVER certifies future private modules or authorizes C04/production.
"""
from collections import Counter
from copy import deepcopy
from hashlib import sha256
from pathlib import Path
from zipfile import ZipFile
import json
import re
import sys
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[1]
WORD = ROOT / "docs/CRECISOL_DOCUMENTO_MAESTRO_UNICO.docx"
REFERENCE = ROOT / "docs/MASTER_REFERENCE.md"
SPEC = ROOT / "docs/c03/C03_01_ESPECIFICACION.md"
MAP = ROOT / "docs/c03/C03_01_MODULOS_DEPENDENCIAS.json"
PREP = ROOT / "docs/C03_PREPARACION.md"
PLAN = ROOT / "docs/PLAN_37_CAPITULOS.md"
PROOF = ROOT / ".c03-01-ci-proof"
EXPECTED_SHA = "e45c2017e62bbbaa6853298efd5de5d785230005203d8791f791760f6f2c622e"
W = "{http://schemas.openxmlformats.org/wordprocessingml/2006/main}"

CANONICAL_PROJECTS = {
    "Crecisol.Domain": "backend/src/Crecisol.Domain/Crecisol.Domain.csproj",
    "Crecisol.Application": "backend/src/Crecisol.Application/Crecisol.Application.csproj",
    "Crecisol.Infrastructure": "backend/src/Crecisol.Infrastructure/Crecisol.Infrastructure.csproj",
    "Crecisol.Api": "backend/src/Crecisol.Api/Crecisol.Api.csproj",
}
CURRENT_EDGES = {
    ("Crecisol.Application", "Crecisol.Domain"),
    ("Crecisol.Infrastructure", "Crecisol.Application"),
    ("Crecisol.Api", "Crecisol.Application"),
    ("Crecisol.Api", "Crecisol.Infrastructure"),
}
FUTURE_MODULES = {"Identity", "Tenants", "Catalog", "Orders", "Payments",
                  "Scheduling", "Memberships", "Media", "Analytics", "Cases"}
PUBLIC_ROUTES = ["", "experiencias", "demostraciones", "alta-empresas", "**"]
SECURITY_FLAGS = (
    "modular_monolith_before_microservices", "domain_no_http_orm_sdk",
    "application_ports_no_direct_mysql", "no_module_writes_foreign_tables",
    "workers_outbox_idempotent_when_implemented",
    "events_versioned_when_implemented", "api_back_end_authorization_required",
    "tenant_context_no_bypass", "server_resolves_tenant_and_data_placement",
    "login_without_company_selector",
    "superadmin_exceptional_access_with_mfa_reason_expiry_audit",
    "full_admin_parametrization_future_only", "no_external_services_or_deploy",
    "no_implemented_private_endpoints_in_this_microphase",
)


def require(ok, message):
    if not ok:
        raise AssertionError(message)


def text_of(node):
    return "".join(el.text or "" for el in node.iter(W + "t")).strip()


def style_of(node):
    props = node.find(W + "pPr")
    st = props.find(W + "pStyle") if props is not None else None
    return st.get(W + "val") if st is not None else "Normal"


def chapter_items():
    raw = WORD.read_bytes()
    require(sha256(raw).hexdigest() == EXPECTED_SHA,
            "Canonical Word sha256 mismatch")
    require(EXPECTED_SHA in REFERENCE.read_text(encoding="utf-8"),
            "MASTER_REFERENCE SHA does not match Word")
    with ZipFile(WORD) as doc:
        body = ET.fromstring(doc.read("word/document.xml")).find(W + "body")
    require(body is not None, "Word document body missing")
    beginning = ending = ""
    started = False
    paras, tables = [], []
    for node in body:
        if node.tag == W + "p":
            text, sty = text_of(node), style_of(node)
            if sty == "Heading1" and re.match(r"^03\.\s", text):
                require(not beginning, "Duplicate chapter 03 heading")
                beginning, started = text, True
                continue
            if started and sty == "Heading1" and re.match(r"^04\.\s", text):
                ending = text
                break
            if started and text:
                paras.append((sty, text))
        elif node.tag == W + "tbl" and started:
            tables.append([[text_of(c) for c in r.findall(W + "tc")]
                           for r in node.findall(W + "tr")])
    require(beginning == "03. Arquitectura técnica limpia y dependencias",
            "C03 canonical chapter title drift")
    require(ending == "04. Multitenancy, dominios y aprovisionamiento",
            "C03/C04 boundary drift")
    require(len(paras) == 31 and len(tables) == 3 and
            [len(t) for t in tables] == [9, 3, 3],
            "C03 appendix or original row count shifted")
    return beginning, ending, paras, tables


def original_parts():
    _, _, paras, tables = chapter_items()
    boundary = next((i for i, (_, p) in enumerate(paras)
                     if p == "Contrato de implementación y certificación del capítulo"), -1)
    require(boundary == 8, "Original chapter vs appendix boundary changed")
    original = [(s, t) for s, t in paras[:boundary]
                if not (s == "OriginalNote" and t.startswith("FUENTE ORIGINAL"))]
    require(len(original) == 7, "Canonical C03 must have seven original text units")
    anchors = (
        "Se propone un monolito modular", "3.1. Árbol recomendado",
        "frontend/apps/", "3.2. Dependencias y límites",
        "Identity emite identidad verificada", "3.3. Decisiones abiertas",
        "El repositorio ya está creado",
    )
    styles = ("Normal", "Heading2", "OriginalNote", "Heading3",
              "Normal", "Heading3", "Normal")
    for i, ((style, txt), anchor) in enumerate(zip(original, anchors)):
        require(style == styles[i] and anchor.casefold() in txt.casefold(),
                "Original C03 paragraph drift P%02d" % (i + 1))
    components = ("Capacidad / regla", "Domain", "Application", "Infrastructure",
                  "API", "Workers", "Frontend público", "Frontend privado",
                  "Shared contracts")
    require(all(len(row) == 3 and all(row) for row in tables[0]),
            "Original C03 three-column table changed")
    for i, item in enumerate(components):
        require(tables[0][i][0] == item, f"Wrong original row T01-R{i+1:02d}")
    annex = " ".join(t for _, t in paras[boundary:])
    for marker in ("C03-01 Extraer", "C03-05 Auditar A", "C03-06 Auditar B",
                   "C03-07 Auditar C", "C03-08 Acta", "TEN-001", "TEN-032",
                   "UC-029", "UC-034", "Cases", "Contrato de implementación"):
        require(marker.casefold() in annex.casefold(),
                "C03 canonical appendix marker missing: " + marker)
    return original, tables[0], paras, tables


def matrix_ids(document):
    return re.findall(r"^\|\s*(P\d{2}|T01-R\d{2})\s*\|", document, re.M)


def valid_ids(ids):
    expected = {f"P{i:02d}" for i in range(1, 8)}
    expected |= {f"T01-R{i:02d}" for i in range(1, 10)}
    return len(ids) == 16 and Counter(ids) == Counter(expected)


def canonical_graph(graph):
    names = set(graph["current_backend"]["projects"])
    edges = [(edge["from"], edge["to"])
             for edge in graph["current_backend"]["project_references"]]
    require(names == set(CANONICAL_PROJECTS), "C03 graph actual project set changed")
    require(graph["current_backend"]["projects"] == CANONICAL_PROJECTS,
            "C03 graph physical project paths changed")
    require(len(edges) == 4 and set(edges) == CURRENT_EDGES,
            "C03 graph added, removed or inverted current physical references")
    adjacency = {name: [] for name in names}
    for src, target in edges:
        require(src in names and target in names and src != target,
                "Invalid project reference")
        adjacency[src].append(target)
    seen, stack = set(), set()

    def visit(node):
        require(node not in stack, "Circular project dependency")
        if node in seen:
            return
        stack.add(node)
        for target in adjacency[node]:
            visit(target)
        stack.remove(node)
        seen.add(node)

    for name in names:
        visit(name)
    require(graph["current_backend"]["dependencies_acyclic"] is True and
            graph["current_backend"]["domain_external_framework_packages_forbidden"] is True
            and graph["current_backend"]["application_direct_infrastructure_forbidden"] is True
            and graph["current_backend"]["application_direct_mysql_forbidden"] is True
            and graph["current_backend"]["api_direct_sql_forbidden"] is True
            and graph["current_backend"]["infrastructure_only_adapters"] is True,
            "Clean Architecture flags weakened")


def validate_contract(graph):
    src = graph["source"]
    require(graph["schema"] == "crecisol.c03-01.architecture-inventory.v1",
            "Wrong C03 graph schema version")
    require(src["repository"] == "solqaryn-platform/CRECISOL" and
            src["sha256"] == EXPECTED_SHA and
            src["start"] == "03. Arquitectura técnica limpia y dependencias" and
            src["end"] == "04. Multitenancy, dominios y aprovisionamiento",
            "Canonical C03 provenance changed")
    require(src["original_paragraph_ids"] == [f"P{i:02d}" for i in range(1, 8)]
            and src["original_table_ids"] == [f"T01-R{i:02d}" for i in range(1, 10)]
            and src["original_table_rows"] == 9
            and src["supplemental_table_rows"] == [3, 3],
            "Missing original C03 source coverage")
    require(src["normative_gates"] ==
            [f"MAE-C03-{i:02d}" for i in range(1, 9)]
            and src["technical_microphases"] ==
            [f"C03-{i:02d}" for i in range(1, 11)],
            "Normative steps and ten technical microphases were conflated")
    require(graph["approved_previous_gate"] == {
        "certified_chapters": 2,
        "previous_chapter": "C02",
        "accepted_at": "2026-11-09",
        "postmerge_sha": "342085ad323c9f24da5b2a197c41574376ac30f0",
        "postmerge_successful_workflows": 12,
    }, "Cannot open C03 without verified certified C02")
    require("not certified" in graph["status"].casefold(),
            "False C03 chapter certification in inventory")
    canonical_graph(graph)
    frontend = graph["current_frontend"]
    require(frontend["project"] == "frontend/angular.json" and
            frontend["production_entry"] == "src/main.ts" and
            frontend["pattern_harness_entry"] == "tests/pattern-harness/main.ts" and
            frontend["public_routes"] == PUBLIC_ROUTES and
            frontend["private_routes_implemented"] is False and
            frontend["private_portal_shells_implemented"] is False and
            frontend["shared_design_system_restructuring_implemented"] is False,
            "Frontend implementation falsely expanded beyond public baseline")
    db = graph["current_database"]
    require(db["schema"] == "database/c01_03_brand_schema.sql" and
            db["client_selected_dsn_allowed"] is False and
            db["cross_module_table_writes_allowed"] is False and
            db["shared_and_dedicated_tenant_placement_implemented"] is False and
            db["migration_required_in_c03_01"] is False,
            "Database placement/migration scope misrepresented")
    api = graph["current_api"]
    require(api["entry"] == "backend/src/Crecisol.Api/Program.cs" and
            api["public_endpoints"] == ["GET /api/v1/public/branding/platform"]
            and api["private_business_endpoints_implemented"] is False
            and api["read_only_public_model"] is True,
            "Invented or privileged API endpoints")
    future = graph["future_modules"]
    require({m["module"] for m in future} == FUTURE_MODULES and len(future) == 10
            and all(m["state"] == "planned" and m["purpose"] for m in future),
            "Future modules are not implemented; scope escalation forbidden")
    require("separado de Orders/Scheduling/Billing" in
            next(m["purpose"] for m in future if m["module"] == "Cases"),
            "Cases must remain independently owned, as G.7 requires")
    require(graph["future_surfaces"] ==
            ["Workers", "Frontend público", "Frontend privado",
             "Shared contracts/OpenAPI"], "Future non-implemented surfaces changed")
    require(all(graph["future_invariants"].get(k) is True for k in SECURITY_FLAGS),
            "Future security invariant downgraded")
    negative = graph["negative_contracts"]
    require(len(negative) == 10 and
            all(negative[i].startswith(f"NEG-{i+1:02d} ") for i in range(10)),
            "C03 first ten negative contracts missing or reordered")


def actual_graph():
    actual = set()
    for name, path in CANONICAL_PROJECTS.items():
        project = ROOT / path
        require(project.is_file(), "Project missing: " + path)
        node = ET.fromstring(project.read_text(encoding="utf-8"))
        refs = node.findall(".//ProjectReference")
        for ref in refs:
            relative = ref.attrib.get("Include", "")
            dest = (project.parent / relative).resolve()
            require(dest.is_relative_to(ROOT) and dest.is_file(),
                    "ProjectReference points outside repo")
            target = next((label for label, p in CANONICAL_PROJECTS.items()
                           if (ROOT / p).resolve() == dest), None)
            require(target is not None, "Unexpected project linked: " + relative)
            actual.add((name, target))
        if name in ("Crecisol.Domain", "Crecisol.Application"):
            require(not node.findall(".//PackageReference"),
                    name + " cannot depend on infrastructure packages")
            cs_dir = project.parent
            for cs in cs_dir.rglob("*.cs"):
                txt = cs.read_text(encoding="utf-8")
                require(not re.search(r"\b(?:using\s+(?:MySqlConnector|Microsoft\.EntityFrameworkCore)"
                                      r"|MySqlConnection\b|DbContext\b|HttpContext\b)", txt),
                        f"Forbidden SQL/HTTP/ORM coupling in {cs.relative_to(ROOT)}")
    require(actual == CURRENT_EDGES,
            "Actual C# ProjectReference graph differs from canonical inventory")
    # This is static source verification; real compiler and API tests are CI steps.
    angular = json.loads((ROOT / "frontend/angular.json").read_text(encoding="utf-8"))
    build = angular["projects"]["crecisol-web"]["architect"]["build"]
    require(build["options"]["browser"] == "src/main.ts" and
            build["configurations"]["pattern-harness"]["browser"] ==
            "tests/pattern-harness/main.ts" and
            build["defaultConfiguration"] == "production",
            "Synthetic harness entered production bootstrap")
    routes = (ROOT / "frontend/src/app/app.routes.ts").read_text(encoding="utf-8")
    observed = re.findall(r"^\s*path:\s*'([^']*)'", routes, re.M)
    require(observed == PUBLIC_ROUTES,
            "Unexpected private Angular route activated")
    code = (ROOT / "backend/src/Crecisol.Api/Program.cs").read_text(encoding="utf-8")
    endpoints = re.findall(r"app\.Map(Get|Post|Put|Patch|Delete)\(\s*\"([^\"]+)\"",
                           code)
    require(endpoints == [("Get", "/api/v1/public/branding/platform")],
            "Unexpected public/private .NET endpoint activated")
    require((ROOT / "database/c01_03_brand_schema.sql").is_file(),
            "Existing C01 read model schema missing")


def proof(name):
    file = PROOF / name
    require(file.is_file() and file.read_text(encoding="utf-8").strip() ==
            "PASS: " + name, "Missing real CI command evidence: " + name)


def audit_a():
    paragraphs, table, _, tables = original_parts()
    graph = json.loads(MAP.read_text(encoding="utf-8"))
    validate_contract(graph)
    require(len(paragraphs) == 7 and len(table) == 9 and
            [len(t) for t in tables] == [9, 3, 3],
            "C03 original source/annex coverage incomplete")
    spec = SPEC.read_text(encoding="utf-8")
    ids = matrix_ids(spec)
    require(valid_ids(ids), "Incomplete or duplicated 16 source IDs")
    require(not valid_ids(ids[:-1]) and
            not valid_ids(ids + [ids[0]]) and
            not valid_ids(["P01"] * 7 + ["T01-R01"] * 9),
            "Missing, duplicated or collapsed C03 ID mutation accepted")
    for marker in ("DIF-C03-01", "MAE-C03-01", "MAE-C03-08",
                   "C03-10", EXPECTED_SHA, "G.7", "Cases", "C04 BLOQUEADO"):
        require(marker in spec, "Missing source/gate in C03 spec: " + marker)
    require((ROOT / "docs/c02/C02_09_AUDITORIA_Y_ACTA.md").is_file(),
            "Previous chapter accepted acta missing")
    previous = (ROOT / "docs/c02/C02_09_AUDITORIA_Y_ACTA.md").read_text(encoding="utf-8")
    require("**«APROBADO»**" in previous and
            "342085ad323c9f24da5b2a197c41574376ac30f0" in
            MAP.read_text(encoding="utf-8"), "C02 approval/gate provenance lost")
    print("AUDIT A PASS C03-01: verified canonical Word, 7 original paragraphs, "
          "9 source table rows, 2 supplementary tables, 16 IDs; "
          "3 missing/duplicate/collapsed matrix mutations rejected.")


def audit_b():
    original_parts()
    graph = json.loads(MAP.read_text(encoding="utf-8"))
    validate_contract(graph)
    observed = []
    mutations = (
        ("cycle", lambda g: g["current_backend"]["project_references"].append(
            {"from":"Crecisol.Domain", "to":"Crecisol.Api"})),
        ("domain-SDK", lambda g: g["current_backend"].update(
            domain_external_framework_packages_forbidden=False)),
        ("Application-MySQL", lambda g: g["current_backend"].update(
            application_direct_mysql_forbidden=False)),
        ("cross-module-write", lambda g: g["current_database"].update(
            cross_module_table_writes_allowed=True)),
        ("browser-DSN", lambda g: g["current_database"].update(
            client_selected_dsn_allowed=True)),
        ("private-route", lambda g: g["current_frontend"].update(
            private_routes_implemented=True)),
        ("fake-workers", lambda g: g["future_modules"][0].update(state="implemented")),
        ("Cases-merged", lambda g: next(m for m in g["future_modules"]
            if m["module"]=="Cases").update(purpose="Orders/Scheduling/Billing")),
        ("C04-premature", lambda g: g["approved_previous_gate"].update(
            certified_chapters=3)),
        ("future-as-current", lambda g: g["current_backend"].update(
            project_status="all future modules active")),
    )
    # A documentary mutation MUST be checked against a meaningful invariant,
    # not merely a convenient test branch that accepts every input.
    for label, transform in mutations:
        sample = deepcopy(graph)
        transform(sample)
        try:
            validate_contract(sample)
        except (AssertionError, KeyError, TypeError, ValueError):
            observed.append(label)
            continue
        raise AssertionError("Unsafe C03 mutation accepted: " + label)
    require(len(observed)==10, "Not all C03 security/architecture mutations rejected")
    actual_graph()
    for marker in ("TEN-001", "TEN-002", "TEN-008", "TEN-009",
                   "TEN-010", "TEN-021", "TEN-032", "BOLA/IDOR",
                   "NO APLICA", "G.7", "DSN"):
        require(marker.casefold() in SPEC.read_text(encoding="utf-8").casefold(),
                "C03 risk not documented: " + marker)
    print("AUDIT B PASS C03-01: 10 adversarial graph/security mutations "
          "rejected; current public paths only, no private backend asserted.")
    print("LIMIT C03-01: No private BOLA/IDOR, MFA or shared/dedicated tenant "
          "runtime security proven. Future modules remain planned.")


def audit_c():
    original_parts()
    graph = json.loads(MAP.read_text(encoding="utf-8"))
    validate_contract(graph)
    actual_graph()
    plan = PLAN.read_text(encoding="utf-8")
    prep = PREP.read_text(encoding="utf-8")
    row_c03 = next((line for line in plan.splitlines() if line.startswith("C03 | ")), "")
    row_c04 = next((line for line in plan.splitlines() if line.startswith("C04 | ")), "")
    require("2/37 capítulos certificados (C01 y C02)" in plan
            and "C03-01" in row_c03 and "EN DESARROLLO" in row_c03
            and "NO CERTIFICADO" in row_c03
            and "PENDIENTE" in row_c04,
            "C03/C04 chapter sequence gate violated")
    require("0/10" in prep and "C03-01" in prep and
            "C03 NO CERTIFICADO" in prep and
            "C04 BLOQUEADO" in prep, "C03-01 is not just an open microphase")
    for name in ("angular", "dotnet", "domain", "application", "live-stack"):
        proof(name)
    for path in (SPEC, PREP, PLAN):
        for link in re.findall(r"\[[^\]]+\]\(([^)]+)\)", path.read_text(encoding="utf-8")):
            if link.startswith(("http:", "https:", "mailto:", "#")):
                continue
            file = (path.parent / link.split("#", 1)[0]).resolve()
            require(file.is_relative_to(ROOT) and file.is_file(),
                    "Broken/escaping local link: " + link)
    print("AUDIT C PASS C03-01: actual acyclic .NET10 refs, Angular22 lazy "
          "public allowlist, separate synthetic harness, executable build and "
          "public-only disposable MySQL8.4/API smoke evidence.")
    print("LIMIT: no new product code, no workers, no private tenancy, "
          "no performance benchmark or C03 chapter certification.")


def preview():
    begin, end, ps, tables = chapter_items()
    print("CANONICAL_C03_START=" + begin)
    print("CANONICAL_C03_END=" + end)
    print("C03_SOURCE_COUNTS paragraphs=%d tables=%d rows=%d" %
          (len(ps), len(tables), sum(len(tab) for tab in tables)))
    for i, (style, line) in enumerate(ps, 1):
        print(f"C03-P{i:03d} [{style}] " + line[:1400])
    for i, table in enumerate(tables, 1):
        for j, row in enumerate(table, 1):
            print(f"C03-T{i:02d}-R{j:02d} " +
                  " | ".join(c[:600] for c in row))
    print("C03_CANONICAL_PREVIEW_SUCCESS: original Word pinned and bounded; "
          "not a chapter certification.")


def main():
    require(len(sys.argv)==2 and sys.argv[1] in ("--preview", "A", "B", "C"),
            "Use --preview|A|B|C")
    {"--preview":preview, "A":audit_a, "B":audit_b,
     "C":audit_c}[sys.argv[1]]()


if __name__ == "__main__":
    try:
        main()
    except (AssertionError, KeyError, OSError, TypeError, ValueError, IndexError) as ex:
        print("C03-01 AUDIT FAILED: " + str(ex), file=sys.stderr)
        sys.exit(1)
