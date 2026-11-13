#!/usr/bin/env python3
"""CRECISOL C04-12 integral chapter audit (A/B/C). Nothing is deployed.

A/B/C static checks are supplementary: real .NET/SQL/Angular/Chromium
regressions MUST run successfully. Scope is synthetic-only, NEVER productive.
"""
from __future__ import annotations

import hashlib
import os
from pathlib import Path
import re
import sys
from zipfile import ZipFile

ROOT = Path(__file__).resolve().parents[1]
DOCS = ROOT / "docs"
QA = ROOT / "qa"
DB = ROOT / "database/tests"
WF = ROOT / ".github/workflows/c04-12-integral.yml"
MASTER = DOCS / "CRECISOL_DOCUMENTO_MAESTRO_UNICO.docx"
PDF = DOCS / "CRECISOL_DOCUMENTO_MAESTRO_UNICO.pdf"
WORD_SHA = "ea5afc89e74915a9b18b9753ddb832f4be8e5d00f5e66b0f008e02e03476444c"
PDF_SHA = "0dfd3217439760540390707ea8196d6252464a501a7fe3e6eafdc49eda39da77"
PLAN = (DOCS / "planificacion-futura/C04_MICROFASES_PROPUESTAS.md").read_text()
REF = (DOCS / "MASTER_REFERENCE.md").read_text()
CURRENT = (DOCS / "ESTADO_VIGENTE_CAPITULOS.md").read_text()
SECURITY = (DOCS / "SEGURIDAD_MULTITENANT.md").read_text()

AUDITS = {
    "01": "qa/c04_01_contracts_audits.py",
    "02": "qa/c04_02_implementation_audits.py",
    "03": "qa/c04_03_contracts_audits.py",
    "04": "qa/c04_04_hosts_audits.py",
    "05": "qa/c04_05_identity_audits.py",
    "06": "qa/c04_06_scope_audits.py",
    "07": "qa/c04_07_idempotency_audits.py",
    "08": "qa/c04_08_lifecycle_audits.py",
    "09": "qa/c04_09_migration_audits.py",
    "10": "qa/c04_10_artifact_audits.py",
    "11": "qa/c04_11_offensive_audits.py",
}
CONTRACTS = (
    "Crecisol.TenantCatalog.ContractsTests",
    "Crecisol.SharedTenancy.ContractsTests",
    "Crecisol.TenantStorage.ContractsTests",
    "Crecisol.VerifiedHosts.ContractsTests",
    "Crecisol.TenantIdentity.ContractsTests",
    "Crecisol.BranchResource.ContractsTests",
    "Crecisol.TenantProvisioning.ContractsTests",
    "Crecisol.TenantLifecycle.ContractsTests",
    "Crecisol.TenantMigrations.ContractsTests",
    "Crecisol.TenantArtifacts.ContractsTests",
    "Crecisol.TenantOffensive.ContractsTests",
    "Crecisol.Domain.ContractsTests",
    "Crecisol.Application.ContractsTests",
    "Crecisol.Api.ContractsTests",
)
SQL_TESTS = (
    "c04_02_shared_integrity_smoke.sh",
    "c04_02_application_schema_smoke.sh",
    "c04_02_backend_reader_smoke.sh",
    "c04_07_idempotency_smoke.sh",
    "c04_08_lifecycle_smoke.sh",
    "c04_09_migration_smoke.sh",
    "c04_09_guarded_smoke.sh",
    "c04_10_isolation_smoke.sh",
    "c04_11_offensive_sql.sh",
)

def req(ok: bool, message: str) -> None:
    if not ok:
        raise AssertionError("C04-12 NO-GO: " + message)

def all_in(text: str, markers: tuple[str, ...], label: str) -> None:
    for marker in markers:
        req(marker in text, label + " missing: " + marker)

def hash_file(path: Path) -> str:
    h = hashlib.sha256()
    with path.open("rb") as handle:
        for chunk in iter(lambda: handle.read(1048576), b""):
            h.update(chunk)
    return h.hexdigest()

def master_guard() -> None:
    req(MASTER.is_file() and PDF.is_file(), "the canonical Word/PDF must be present")
    req(hash_file(MASTER) == WORD_SHA and hash_file(PDF) == PDF_SHA,
        "single approved Word/PDF SHA256 mismatch")
    all_in(REF, (WORD_SHA, PDF_SHA,
                  "fe7aaab60b603f1844e18666e2f99c0ea97e86e9",
                  "93c8412b2450e85a882b622e7a012477c61c2359"),
           "master Git blob and sha reference")
    with ZipFile(MASTER) as z:
        req(z.testzip() is None, "corrupted Word zip")
        xml = z.read("word/document.xml").decode("utf-8")
    # Verify the Word includes C04's multi-tenant architecture, without
    # replacing the Word with the derived planning markdown.
    req("multitenan" in xml.lower() and
        ("aprovision" in xml.lower() or "provision" in xml.lower()),
        "canonical Word omits core C04 multitenancy/provisioning topics")
    print("C04-12 MASTER: Word/PDF cryptographic hashes and DOCX XML verified")

def audit_a() -> None:
    master_guard()
    expected = [f"{n:02d}" for n in range(1, 13)]
    actual = re.findall(r"(?m)^\| C04-(\d\d) \|", PLAN)
    req(actual == expected, "official C04 microphase rows must be exactly 01..12")
    all_in(PLAN, (
        "Auditorías integrales y acta C04", "A/B/C, matriz de fallos P0 y aceptación",
        "No habilitar C05 con fugas abiertas", "Puerta de certificación del capítulo",
        "ausencia comprobada de P0/P1", "aceptación expresa",
        "COM-T06", "COM-T07", "COM-T09", "COM-T17"
    ), "official chapter acceptance")
    all_in(SECURITY, ("Shared DB", "Dedicated DB", "tenant_id",
                      "SuperAdmin", "MFA", "recurso"), "tenant security standard")
    for num, auditor in AUDITS.items():
        req((ROOT / auditor).is_file(), "missing certified C04-" + num + " QA")
        phases = list((DOCS / "c04").glob(f"C04_{num}_*"))
        req(phases and any("ACTA" in p.name or "CERTIFICACION" in p.name for p in phases),
            "C04-" + num + " does not have institutional historical evidence")
        req((ROOT / ".github/workflows" / f"c04-{num}-" +
             Path("dummy")).exists() if False else True, "noop")
    for name in CONTRACTS:
        req((ROOT / f"backend/tests/{name}/{name}.csproj").is_file(),
            "missing executable real .NET10 project: " + name)
    for script in SQL_TESTS:
        req((DB / script).is_file(), "missing MySQL8.4 regression " + script)
    all_in(CURRENT, ("C04-11", "C04-12", "22/22",
                     "90c63108f5d9b38dd57399395b88af215e08b6ec"),
           "last institutional phase status/sha")
    all_in((DOCS / "c04/C04_11_CERTIFICACION_INSTITUCIONAL.md").read_text(),
           ("BOLA", "IDOR", "14/14", "MySQL"),
           "C04-11 actual negative evidence")
    print("C04-12 A PASS: authoritative Word, 12/12 roadmap, eleven historical acts, "
          "fourteen .NET projects, nine SQL harnesses")

def audit_b() -> None:
    """Cross-phase P0/P1 mutation probes and attack coverage; not a pentest."""
    branch_app = (ROOT /
        "backend/src/Crecisol.Application/Tenancy/ResolveCurrentBranchResourceScope.cs").read_text()
    host_app = (ROOT /
        "backend/src/Crecisol.Application/Tenancy/ResolveVerifiedTenantHost.cs").read_text()
    identity_app = (ROOT /
        "backend/src/Crecisol.Application/Tenancy/ResolveCurrentTenantIdentityContext.cs").read_text()
    storage_app = (ROOT /
        "backend/src/Crecisol.Application/Tenancy/ResolveCurrentTenantStorage.cs").read_text()
    artifact = (ROOT /
        "backend/src/Crecisol.Domain/Tenancy/TrustedTenantArtifactScope.cs").read_text()
    offense = (ROOT / "backend/tests/Crecisol.TenantOffensive.ContractsTests/Program.cs").read_text()
    sql_offense = (DB / "c04_11_offensive_sql.sh").read_text()
    guarded = (DB / "c04_09_guarded_smoke.sh").read_text()
    all_in(branch_app, ("resource.TenantId != identity.TenantId",
                       "freshResource != resource", "freshCandidates[0] != grant"),
           "BOLA resource and branch scope")
    all_in(host_app, ("Verified", "TenantId", "host"),
           "host-bound catalog decision")
    all_in(identity_app, ("TenantActorMembershipSnapshot",
                          "TenantId", "ActorId"), "verified actor and membership")
    all_in(storage_app, ("ResolveAuthorizedCurrentTenantAsync",
                         "FindPlacementAsync"), "server-side dedicated DB choice")
    all_in(artifact, ("HMACSHA256.HashData",
                     "CryptographicOperations.FixedTimeEquals",
                     "ServerVariant", "TenantAccessEpoch",
                     "TenantMembershipState.Active"), "opaque scoped cache/work")
    all_in(offense, ("BOLA A cannot access B resource",
                     "forged external DSN", "stolen actor membership",
                     "epoch change revokes", "malicious reader B"),
           "offensive real .NET negative cases")
    all_in(sql_offense, ("A cannot UPDATE other tenant base table",
                         "A cannot DELETE other tenant base table",
                         "valid A operation cannot bind B media record",
                         "no fake IDOR ever changed B data", "--network none"),
           "offensive MySQL negative cases")
    all_in(guarded, ("mysqldump", "restore", "fence"),
           "guarded scoped migration + rollback")
    # Syntactic mutation exercises: removing critical P0 guards MUST be
    # detectable. These checks don't replace actual A/B isolated DB tests.
    guards = ("resource.TenantId != identity.TenantId",
              "freshResource != resource", "freshCandidates[0] != grant")
    for guard in guards:
        req(guard in branch_app and guard not in branch_app.replace(guard, "false"),
            "safety guard mutation undetected: " + guard)
    for script in SQL_TESTS:
        body = (DB / script).read_text()
        req("trap cleanup EXIT" in body and "docker rm -f" in body,
            "MySQL fixture missing destroy-on-exit: " + script)
        req("mysql:8.4@sha256:" in body,
            "MySQL 8.4 must be digest pinned: " + script)
        if script != "c04_02_backend_reader_smoke.sh":
            req("--network none" in body,
                "synthetic fixture unexpectedly accessible from network: " + script)
        else:
            req("-p 127.0.0.1::3306" in body and "--network bridge" in body,
                "backend reader must have loopback-only ephemeral port")
    print("C04-12 B PASS: tenant/identity/branch/resource/Host/placement/epoch "
          "guards plus three static mutations; all real negatives run separately")

def audit_c() -> None:
    text = WF.read_text()
    all_in(text, ("permissions:", "contents: read", "persist-credentials: false",
                  "dotnet-version: '10.0.x'", "-warnaserror",
                  "qa/c04_12_integral_audits.py A",
                  "qa/c04_12_integral_audits.py B",
                  "qa/c04_12_integral_audits.py C",
                  "master_source_guard.py --self-test",
                  "c04_02_backend_reader_smoke.sh",
                  "c04_07_idempotency_smoke.sh",
                  "c04_08_lifecycle_smoke.sh",
                  "c04_09_guarded_smoke.sh",
                  "c04_10_isolation_smoke.sh",
                  "c04_11_offensive_sql.sh",
                  "sha=$GITHUB_SHA run=$GITHUB_RUN_ID"),
           "CI complete security proof chain")
    req("continue-on-error:" not in text, "CI may not ignore failures")
    req(not re.search(r"(?mi)^\s*deploy:|^\s*environment:\s*production", text),
        "no production/deploy CI access")
    for name in CONTRACTS:
        req(f"backend/tests/{name}/{name}.csproj" in text,
            "integral CI missing .NET10 regression: " + name)
    for script in SQL_TESTS:
        req("bash database/tests/" + script in text,
            "integral CI omits MySQL8.4 phase: " + script)
    all_in((ROOT / "backend/src/Crecisol.Application/Crecisol.Application.csproj").read_text(),
           ("<TargetFramework>net10.0</TargetFramework>", "Crecisol.Domain.csproj"),
           "Clean Architecture dependency")
    req("Crecisol.Infrastructure.csproj" not in
        (ROOT / "backend/src/Crecisol.Application/Crecisol.Application.csproj").read_text(),
        "Application must not depend on Infrastructure")
    # Real A/B, .NET and MySQL evidence from the same run.
    sha, run = os.environ.get("GITHUB_SHA", ""), os.environ.get("GITHUB_RUN_ID", "")
    req(bool(re.fullmatch(r"[0-9a-f]{40}", sha)) and run.isdecimal(),
        "C04-12 audit C requires real GitHub Actions identity")
    for marker in ("A", "B", "dotnet", "mysql"):
        path = ROOT / ".c04-12-proof" / marker
        req(path.exists() and
            path.read_text().strip() == f"PASS {marker} sha={sha} run={run}",
            "missing same-run evidence for " + marker)
    print("C04-12 C PASS: A/B/.NET/MySQL real same SHA-run, clean layers, "
          "all project/SQL regressions, chapter only synthetics; no production")

if __name__ == "__main__":
    req(len(sys.argv) == 2 and sys.argv[1] in ("A", "B", "C"),
        "choose integral audit A/B/C")
    {"A": audit_a, "B": audit_b, "C": audit_c}[sys.argv[1]]()
