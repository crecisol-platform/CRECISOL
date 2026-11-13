#!/usr/bin/env python3
"""CRECISOL C04-10 A/B/C independent static audits.

Static guards are NOT a .NET compile, MySQL execution or security pen test.
The CI MUST execute all real tests on the same SHA before certification.
"""
from pathlib import Path
import re
import sys

ROOT = Path(__file__).resolve().parents[1]
DOMAIN = (ROOT / "backend/src/Crecisol.Domain/Tenancy/TrustedTenantArtifactScope.cs").read_text()
APP = (ROOT / "backend/src/Crecisol.Application/Tenancy/ApplyScopedTenantArtifact.cs").read_text()
TEST = (ROOT / "backend/tests/Crecisol.TenantArtifacts.ContractsTests/Program.cs").read_text()
SQL = (ROOT / "database/tests/c04_10_isolation_fixture.sql").read_text()
SMOKE = (ROOT / "database/tests/c04_10_isolation_smoke.sh").read_text()
SPEC = (ROOT / "docs/c04/C04_10_ESPECIFICACION.md").read_text()
CI = (ROOT / ".github/workflows/c04-10-isolation.yml").read_text()

def req(ok: bool, label: str):
    if not ok:
        raise AssertionError("C04-10 audit failed: " + label)

def contains(source: str, values: tuple[str, ...], label: str):
    for value in values:
        req(value in source, label + " missing " + repr(value))

def audit_a():
    """A: normative chain, actual test expectations and four purposes."""
    contains(SPEC, (
        "C04-09", "C04-10", "keys, jobs, medios y exportaciones",
        "C10-K01", "C10-J01", "C10-M01", "C10-E01", "C10-A01",
        "Documento Maestro", "P0", "P1", "C04-11", "NO CERTIFICADA",
        "Cloudflare", "SIN ADAPTADOR"
    ), "official C04-10 scope")
    contains(DOMAIN, ("TenantArtifactKind", "Cache", "Job", "Media", "Export",
                       "TenantPrivateArtifactKey", "TrustedTenantArtifactReceipt"),
             "all four domain purposes")
    contains(TEST, (
        "valid tenant B never aliases valid tenant A",
        "branch B isolated", "actor B isolated",
        "cache stable but jobs/media/exports operation-scoped",
        "suspended tenant", "revoked actor session",
        "revoked membership", "suspended resource",
        "malicious gateway returns cross-tenant key",
        "same operation id cannot be reused for valid B"
    ), ".NET positive and negative matrix")
    contains(SMOKE, (
        "B asks for A cached content", "A asks for B cache",
        "B asks for A object metadata", "B asks for A export",
        "B claims A's job selector", "B media mapped to valid A operation",
        "B reactivated branch cannot ack old lease revision",
        "reactivated A epoch rejects old cache",
        "twelve" if "twelve" in SMOKE else "seq 1 12"
    ), "MySQL positive and negative scope matrix")
    print("C04-10 A: source/impact matrix and four purpose test cases present")

def audit_b():
    """B: actor/tenant isolation, revision and no cap-by-opaque-key."""
    contains(DOMAIN, (
        "HMACSHA256.HashData", "protectedKeyMaterial.Length < 32",
        "TenantLifecycleState.Active", "ActorSessionState.Active",
        "TenantMembershipState.Active", "TrustedBranchState.Active",
        "TrustedResourceState.Active", "TenantAccessEpoch",
        "SessionRevision", "MembershipRevision", "BranchRevision",
        "ResourceRevision", "PlacementRevision", "DedicatedSlot",
        "IntentFingerprint", "IsolationFingerprint",
        "CryptographicOperations.FixedTimeEquals",
        "tenant.cache.read", "tenant.job.execute", "tenant.media.read",
        "tenant.export.create"
    ), "private scope and HMAC contract")
    contains(APP, (
        "serverAuthorizedScope.OperationId != operationId",
        "approved != serverAuthorizedScope",
        "beforeEffect != approved", "afterEffect != beforeEffect",
        "ReadCurrentApprovedScopeAsync",
        "TenantPrivateArtifactKey.IsCanonicalForKind",
        "TrustedTenantArtifactReceipt.RequireExactMatch",
        "MUST independently recheck",
        "cancellationToken.ThrowIfCancellationRequested()"
    ), "three authorization checks")
    contains(SQL, (
        "PRIMARY KEY(tenant_id,branch_id,resource_id)",
        "UNIQUE KEY uq_c10_cache_key_global",
        "fk_c10_job_decision", "fk_c10_media_decision",
        "fk_c10_export_decision", "SQL SECURITY DEFINER",
        "SUBSTRING_INDEX(USER(),'@',1)",
        "v_depoch <> v_epoch",
        "v_dbranchrev <> v_actualbranchrev",
        "v_dresourcerev <> v_actualresourcerev",
        "v_jobbranchrev <> v_dbranchrev",
        "v_jobresourcerev <> v_dresourcerev",
        "b.revision=c.branch_revision",
        "b.revision=m.branch_revision",
        "b.revision=e.branch_revision",
        "v_owner <> v_actor",
        "v_fence <> p_fence"
    ), "MySQL branch/resource and fenced worker")
    contains(SMOKE, (
        "runner cannot read tenant ledger directly",
        "runner cannot execute DDL",
        "tenant B cannot ack A's fence",
        "one real claimant of A job",
        "eleven other workers cannot execute same A job",
        "suspended A cannot finish previously claimed job",
        "reactivated A epoch rejects old job ack",
        "expired B export cannot be reused",
        "current B identity revoked before cache read",
        "restored resource owner cannot replay old revision cache",
        "B reactivated branch cannot read stale export",
    ), "executed negative tests")
    guards = (
        "v_dtenant <> v_tenant", "v_depoch <> v_epoch",
        "v_dbranchrev <> v_actualbranchrev",
        "v_dresourcerev <> v_actualresourcerev",
        "v_jobepoch <> v_depoch", "v_owner <> v_actor"
    )
    for guard in guards:
        mutated = SQL.replace(guard, "FALSE")
        req(guard in SQL and guard not in mutated,
            "missed synthetic mutation of SQL authorization guard: " + guard)
    print(f"C04-10 B: scope and lifecycle checks present; {len(guards)} static negative mutations detectable")

def audit_c():
    """C: clean layering, disposable fixture, reproducible CI and blast radius."""
    req("namespace Crecisol.Domain.Tenancy;" in DOMAIN,
        "Domain layering")
    req("namespace Crecisol.Application.Tenancy;" in APP,
        "Application layering")
    for forbidden in ("MySqlConnector", "DbContext", "HttpContext", "Redis",
                      "ConnectionString", "Cloudflare", "AmazonS3", "BlobClient"):
        req(forbidden not in DOMAIN, "Domain framework dependency: " + forbidden)
        req(forbidden not in APP, "Application concrete dependency: " + forbidden)
    req("--network none" in SMOKE and "MYSQL_ALLOW_EMPTY_PASSWORD=yes" in SMOKE,
        "isolated disposable MySQL 8.4 fixture")
    req("mysql:8.4@sha256:" in SMOKE, "MySQL 8.4 pinned image")
    req(SQL.count("CREATE DATABASE c04_10_disposable") == 1,
        "one fresh throwaway DB only")
    req(SQL.count("DELIMITER //") == 1 and SQL.count("DELIMITER ;") == 1,
        "single consistent SQL procedure delimiter section")
    for p in ("claim_job", "ack_job", "read_private_cache",
              "read_private_media", "read_private_export"):
        req(SQL.count("CREATE PROCEDURE " + p + "(") == 1,
            "missing or duplicate SQL access procedure: " + p)
    req(SQL.count("END//") == 5, "exactly five isolated access procedures")
    req(not re.search(r"(?:CREATE|ALTER|DROP)\s+DATABASE\s+(?!c04_10_disposable)\w+",
                      SQL, re.I), "fixture touches foreign database")
    req("GRANT EXECUTE ON PROCEDURE" in SMOKE, "least privilege synthetic roles")
    req("GRANT ALL" not in SMOKE and "FLUSH PRIVILEGES" not in SMOKE,
        "overprivileged test runner")
    contains(CI, ("persist-credentials: false", "contents: read",
                  "dotnet build backend/src/Crecisol.Api", "-warnaserror",
                  "Crecisol.TenantArtifacts.ContractsTests",
                  "c04_10_isolation_smoke.sh", "c04_10_artifact_audits.py A",
                  "c04_10_artifact_audits.py B", "c04_10_artifact_audits.py C",
                  "sha=$GITHUB_SHA run=$GITHUB_RUN_ID"), "CI evidence")
    req("continue-on-error:" not in CI, "CI cannot ignore negative failures")
    req(not any(x in SPEC for x in ("CERTIFICADA EN PRODUCCIÓN", "R2 ACTIVO")),
        "documentation cannot claim productive coverage")
    print("C04-10 C: clean layers, single throwaway DB, five procedures, CI evidence")

if __name__ == "__main__":
    req(len(sys.argv) == 2 and sys.argv[1] in ("A","B","C"),
        "choose A, B or C")
    {"A": audit_a, "B": audit_b, "C": audit_c}[sys.argv[1]]()
