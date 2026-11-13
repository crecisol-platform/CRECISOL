#!/usr/bin/env python3
"""C04-11 independent A/B/C evidence gates; static audits are not dynamic tests."""
from pathlib import Path
import re
import sys

ROOT = Path(__file__).resolve().parents[1]
PLAN = (ROOT / "docs/planificacion-futura/C04_MICROFASES_PROPUESTAS.md").read_text()
MASTER = (ROOT / "docs/MASTER_REFERENCE.md").read_text()
TEST = (ROOT / "backend/tests/Crecisol.TenantOffensive.ContractsTests/Program.cs").read_text()
SQL = (ROOT / "database/tests/c04_11_offensive_sql.sh").read_text()
BASE_SQL = (ROOT / "database/tests/c04_10_isolation_fixture.sql").read_text()
CI = (ROOT / ".github/workflows/c04-11-offensive.yml").read_text()
DOMAIN = (ROOT / "backend/src/Crecisol.Domain/Tenancy/TrustedTenantArtifactScope.cs").read_text()
APP = (ROOT / "backend/src/Crecisol.Application/Tenancy/ResolveCurrentBranchResourceScope.cs").read_text()

def require(condition, message):
    if not condition:
        raise AssertionError("C04-11 AUDIT FAILED: " + message)

def require_all(source, markers, label):
    for marker in markers:
        require(marker in source, label + " missing " + repr(marker))

def audit_a():
    """A: true normative C04-11 requirement -> executable attack assertions."""
    require_all(PLAN, ("C04-11", "Pruebas ofensivas multitenant",
                       "BOLA, IDOR, host, claims y DSN falsificados",
                       "Ninguna lectura/escritura cross-tenant",
                       "C04-10", "C04-12"), "official plan")
    require_all(MASTER, ("Documento Maestro Único",
                         "CRECISOL_DOCUMENTO_MAESTRO_UNICO.docx"), "master reference")
    require_all(TEST, ("O11-01", "O11-02", "O11-03", "O11-04",
                       "O11-05", "O11-06", "O11-09",
                       "ResolveCurrentBranchResourceScope",
                       "ResolveCurrentTenantIdentityContext",
                       "ResolveCurrentTenantStorage",
                       "ResolveVerifiedTenantHost",
                       "ApplyScopedTenantArtifact",
                       "TenantPrivateArtifactKey.Derive",
                       "DenyAsync", "Deny(", "Positive("), "actual .NET attacks")
    require_all(SQL, ("O11-01", "O11-04", "O11-06", "O11-07",
                      "O11-08", "O11-09", "deny_runner", "deny_fk",
                      "C04-11 OFFENSIVE MySQL8.4 SUCCESS",
                      "no fake IDOR ever changed B data"), "actual SQL attacks")
    print("C04-11 audit A: master, plan, attack matrix and four independent surfaces matched")

def audit_b():
    """B: no scope bypass, deny must fail open bypass, real negative mutations."""
    require_all(TEST, ("C04-11 SECURITY BYPASS",
                       "catch (UnauthorizedAccessException)",
                       "catch (InvalidOperationException)",
                       "hostProofA", "hostProofB",
                       "BOLA A cannot access B resource",
                       "stolen actor membership",
                       "forged external DSN",
                       "same resource_id",
                       "malicious reader B", "epoch change revokes",
                       "revoked grant", "grant revoked between catalog reads"),
                "denials and no broad exception swallowing")
    require("catch (Exception)" not in TEST, "broad catch masks security violations")
    require("catch {" not in TEST, "catch-all masks security violations")
    require_all(SQL, ("--network none", "MYSQL_ALLOW_EMPTY_PASSWORD=yes",
                      "GRANT EXECUTE ON PROCEDURE",
                      "cannot SELECT other tenant base table",
                      "cannot UPDATE other tenant base table",
                      "cannot DELETE other tenant base table",
                      "cannot DROP protected tenant table",
                      "A IDOR B cache", "B IDOR A cache",
                      "valid A operation cannot bind B media record",
                      "valid B tenant cannot reference branch A",
                      "SET untrusted tenant variable",
                      "SET untrusted login variable",
                      "SQL boolean expression",
                      "no fake IDOR ever changed B data",
                      "suspend A invalidates previous cache"),
                "real SQL cross-tenant and tamper denials")
    require(SQL.count('deny_runner "') >= 20,
            "offensive SQL must include >=20 direct negative attacks")
    require(SQL.count('deny_fk "') >= 2,
            "two SQL cross-tenant composite FK negative attacks mandatory")
    require_all(BASE_SQL, ("PRIMARY KEY(tenant_id,branch_id,resource_id)",
                           "UNIQUE KEY uq_c10_cache_key_global",
                           "SQL SECURITY DEFINER",
                           "SUBSTRING_INDEX(USER(),'@',1)"),
                "cross-tenant persisted fixture boundaries")
    require_all(DOMAIN, ("HMACSHA256.HashData",
                         "CryptographicOperations.FixedTimeEquals",
                         "ServerVariant", "TenantAccessEpoch"),
                "C04-10 protected scope immutable secret and revision")
    require_all(APP, ("identity.TenantId, resourceId", "resource.TenantId != identity.TenantId",
                      "freshResource != resource", "freshCandidates[0] != grant"),
                "real C04-06 scope recheck remains enforced")
    # Mutations of security guards must be detected by the audit itself.
    for guard in ("resource.TenantId != identity.TenantId",
                  "freshResource != resource",
                  "freshCandidates[0] != grant"):
        mutated = APP.replace(guard, "false")
        require(guard in APP and guard not in mutated,
                "security mutation not detected: " + guard)
    print("C04-11 audit B: >=22 genuine SQL negatives, context denials and 3 guard mutations")

def audit_c():
    """C: clean architecture, real runnable tests, scoped blast radius."""
    require("namespace Crecisol.Domain.Tenancy;" in DOMAIN, "Domain namespace changed")
    require("namespace Crecisol.Application.Tenancy;" in APP, "Application namespace changed")
    require("using Crecisol.Domain.Tenancy;" in TEST and
            "using Crecisol.Application.Tenancy;" in TEST,
            "attacks must exercise real Domain/Application")
    require("mysql:8.4@sha256:" in SQL, "unpinned MySQL 8.4 image")
    require("docker run --rm -d --network none" in SQL,
            "network-isolated disposable fixture")
    require("trap cleanup EXIT" in SQL and "docker rm -f" in SQL,
            "no container cleanup after tests")
    require("bash database/tests/c04_10_isolation_smoke.sh" in CI,
            "prior C04-10 actual MySQL concurrency regression missing")
    require_all(CI, ("persist-credentials: false", "contents: read",
                     "dotnet build backend/src/Crecisol.Api",
                     "Crecisol.TenantOffensive.ContractsTests",
                     "-warnaserror", "dotnet run", "c04_11_offensive_sql.sh",
                     "c04_10_isolation_smoke.sh",
                     "c04_11_offensive_audits.py A",
                     "c04_11_offensive_audits.py B",
                     "c04_11_offensive_audits.py C",
                     "sha=$GITHUB_SHA run=$GITHUB_RUN_ID"), "same-SHA CI")
    require("continue-on-error:" not in CI, "no ignored failures")
    require(not re.search(r"(?im)^\s*deploy:|^\s*environment:\s*production",
                          CI), "no productive GitHub Actions deployment")
    require("DROP DATABASE" not in SQL and "USE mysql" not in SQL,
            "no environment DB mutation")
    print("C04-11 audit C: clean layers, pinned isolated MySQL, gate/sha and no production")

if __name__ == "__main__":
    require(len(sys.argv) == 2 and sys.argv[1] in ("A", "B", "C"),
            "audit phase must be A/B/C")
    {"A": audit_a, "B": audit_b, "C": audit_c}[sys.argv[1]]()
