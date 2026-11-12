#!/usr/bin/env python3
"""C04-02 isolated MySQL8.4 preparation gates, NOT a product tenant migration.

Audit A: actual pinned Word C04, previous C04-01 gate, explicit FK design.
Audit B: reject altered relational, startup or privilege trust boundaries.
Audit C: real MySQL8.4 synthetic run + A/B SHA/run proof, no production claims.
"""
from __future__ import annotations
import os
from pathlib import Path
import re
import sys

from c04_01_normative_inventory import chapter4, EXPECTED
from c04_01_preparation_audits import canonical_active_word, ROOT

DDL = ROOT / "database/tests/fixtures/c04_02_shared_integrity_fixture.sql"
RUNNER = ROOT / "database/tests/c04_02_shared_integrity_smoke.sh"
SPEC = ROOT / "docs/c04/C04_02_PREPARACION_MODELO_SHARED.md"
PROOF = ROOT / ".c04-02-prep-proof"
IMAGE = "mysql:8.4@sha256:a81d043930e51745522bd5c430961cf8286f0a23f0a9a2a6e0108f7ea71b0902"

MARKERS: dict[str, tuple[str, ...]] = {
    "ddl": (
        "ENGINE=InnoDB",
        "PRIMARY KEY (tenant_id)",
        "PRIMARY KEY (tenant_id, branch_id)",
        "PRIMARY KEY (tenant_id, branch_id, item_id)",
        "PRIMARY KEY (tenant_id, branch_id, item_id, note_id)",
        "UNIQUE KEY uq_c04_02_item_scope_key (tenant_id, branch_id, external_key)",
        "CONSTRAINT fk_c04_02_branch_tenant FOREIGN KEY (tenant_id)",
        "CONSTRAINT fk_c04_02_item_branch FOREIGN KEY (tenant_id, branch_id)",
        "CONSTRAINT fk_c04_02_note_item FOREIGN KEY (tenant_id, branch_id, item_id)",
        "ON UPDATE RESTRICT ON DELETE RESTRICT",
        "CHECK (tenant_id <> X'00000000000000000000000000000000')",
    ),
    "runner": (
        IMAGE,
        "--network none",
        "--rm -d",
        "MySQL init process done. Ready for start up.",
        "if [ \"$init_done\" -eq 1 ]",
        "root_sql_NEVER_MATCH_THIS_STRING",  # placeholder replaced below
        "ERROR 1452",
        "ERROR 1451",
        "ERROR 1062",
        "ERROR 3819",
        "ERROR 1048",
        "ERROR 1142",
        "READER_PASSWORD=\"$(openssl rand -hex 18)\"",
        "trap cleanup EXIT",
        "PASS C04-02 MySQL8.4 preparation:",
    ),
    "spec": (
        "C04-02 · Preparación del modelo relacional SHARED",
        EXPECTED,
        "FK compuestas con tenant",
        "NO IMPLEMENTADA NI CERTIFICADA",
        "C04-03",
        "SH-16",
        "un usuario SQL con SELECT no filtrado",
    ),
}
# Two local SQL probes after confirmed full server bootstrap, not a temporary
# mysql:8.4 initialization daemon. Source uses function sql(), never root_sql().
MARKERS["runner"] = tuple(
    "sql 'SELECT 1'" if x == "root_sql_NEVER_MATCH_THIS_STRING" else x
    for x in MARKERS["runner"]
)

def need(condition: bool, message: str) -> None:
    if not condition:
        raise AssertionError(message)

def load() -> dict[str, str]:
    return {
        "ddl": DDL.read_text(encoding="utf-8"),
        "runner": RUNNER.read_text(encoding="utf-8"),
        "spec": SPEC.read_text(encoding="utf-8"),
    }

def contract(files: dict[str, str]) -> None:
    for kind, tokens in MARKERS.items():
        for token in tokens:
            need(token in files[kind], f"{kind}: security boundary absent: {token}")
    ddl = files["ddl"]
    runner = files["runner"]
    spec = files["spec"]
    need(ddl.count("CREATE TABLE c04_02_fixture_") == 4,
         "Exactly four disposable fixture tables required")
    need(ddl.count("FOREIGN KEY") == 3,
         "Tenant hierarchy must have three composite-aware FK constraints")
    need(ddl.count("ON UPDATE RESTRICT ON DELETE RESTRICT") == 3,
         "No cascade reparent/deletion in synthetic fixture")
    need(runner.count("sql 'SELECT 1'") == 2,
         "Final MySQL daemon needs two consecutive SQL probes")
    need("if result=$(sql \"$query\" 2>&1); then" in runner,
         "Negative tests must fail on unexpectedly successful writes")
    need("if result=$(docker exec -e MYSQL_PWD=" in runner,
         "Read-only fixture user's negative grants must be tested")
    need("CASE_INSENSITIVE_FORBIDDEN_ROUTE" not in runner,
         "Unexpected route sentinel")
    need("docker rm -f \"$CONTAINER\"" in runner,
         "Disposable container cleanup required")
    need("--publish" not in runner and "--privileged" not in runner,
         "Fixture exposed a network/privileged capability")
    need("Docker" not in ddl and "CONNECTION_STRING" not in ddl,
         "Schema must remain portable and credential-free")
    need("NO IMPLEMENTADA NI CERTIFICADA" in spec and "C04-03" in spec,
         "Preparation must not claim product implementation or chapter unlock")

def audit_a() -> None:
    need(canonical_active_word(), "Canonical Word/PDF pins do not match")
    rows = chapter4()
    need(len(rows) == 72, "C04 master paragraph count changed")
    doc = "\n".join(x for _, x in rows)
    for term in (
        "FK compuestas con tenant",
        "SHARED A/B y DEDICATED C",
        "nunca confiar en tenant_id aportado por navegador",
    ):
        need(term in doc, "Original Word requirement missing: " + term)
    plan = (ROOT / "docs/planificacion-futura/C04_MICROFASES_PROPUESTAS.md").read_text(encoding="utf-8")
    need("| C04-02 | Modelo relacional shared tenant-aware |" in plan,
         "C04-02 original technical deliverable changed")
    contract(load())
    print("PASS C04-02 A: Word/PDF, 72 original C04 paragraphs, 3 tenant/branch rules, "
          "composite FK/PK fixture and 16 threat cases traced")

def audit_b() -> None:
    files = load()
    contract(files)
    # Deliberately remove whole trust-boundary tokens from the exact input,
    # and require the validation to reject EACH falsification independently.
    mutations = 0
    for kind in ("ddl", "runner", "spec"):
        for token in MARKERS[kind]:
            mutant = dict(files)
            mutant[kind] = files[kind].replace(token, "[REMOVED C04-02 TRUST BOUNDARY]")
            try:
                contract(mutant)
            except AssertionError:
                mutations += 1
            else:
                raise AssertionError("Unsafe mutation accepted: " + kind + " " + token)
    # A SQL integrity check alone is NOT backend row-level access control.
    need("unscoped SQL DOES see both tenants" in files["runner"],
         "The test must expose lack of implicit row-level security")
    need("LIMIT: synthetic disposable SQL fixture only" in files["runner"],
         "Test must not claim tenant isolation authorization")
    need("backend/" not in files["ddl"], "Fixture may not reference backend implementation")
    print(f"PASS C04-02 B: {mutations} independent document/DDL/runner mutations "
          "rejected; real SQL cross-tenant negatives must also PASS")

def audit_c() -> None:
    need(os.environ.get("GITHUB_ACTIONS") == "true",
         "Audit C must run in a hosted GitHub Actions job")
    sha, run = os.environ.get("GITHUB_SHA", ""), os.environ.get("GITHUB_RUN_ID", "")
    need(bool(re.fullmatch(r"[0-9a-f]{40}", sha) and re.fullmatch(r"[0-9]+", run)),
         "GitHub SHA or run ID invalid")
    for stage in ("A", "B", "mysql"):
        marker = PROOF / stage
        need(marker.is_file(), "Missing same-run proof: " + stage)
        need(marker.read_text(encoding="utf-8").strip() ==
             f"PASS {stage} sha={sha} run={run}",
             "Wrong SHA/run proof: " + stage)
    contract(load())
    # C04-01 remains a pure Domain/Application contract, without real tenant
    # adapters in API, Infrastructure or database product modules.
    program = (ROOT / "backend/src/Crecisol.Api/Program.cs").read_text(encoding="utf-8")
    need("/api/v1/tenants" not in program and "/api/v1/admin/tenants" not in program,
         "Unexpected tenant endpoint active in preparation")
    print("PASS C04-02 C: real isolated MySQL8.4 tenant/branch FK tests, "
          "same-run A/B/mysql SHA proof, no tenant API")
    print("LIMIT: preparation only; no production migrations, auth, "
          "cross-tenant HTTP/BOLA tests or dedicated routing performed")

def main() -> None:
    need(len(sys.argv) == 2 and sys.argv[1] in ("A", "B", "C"),
         "Usage: c04_02_preparation_audits.py A|B|C")
    {"A": audit_a, "B": audit_b, "C": audit_c}[sys.argv[1]]()

if __name__ == "__main__":
    try:
        main()
    except (AssertionError, OSError, ValueError) as exc:
        print("FAIL C04-02 preparation:", exc, file=sys.stderr)
        sys.exit(1)
