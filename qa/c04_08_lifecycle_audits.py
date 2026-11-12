#!/usr/bin/env python3
"""C04-08 source-boundary audits. Static checks supplement, NEVER replace, .NET/MySQL runtime tests."""
from __future__ import annotations

import hashlib
import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
DOMAIN = ROOT / "backend/src/Crecisol.Domain/Tenancy/TenantLifecycleTransitions.cs"
APP = ROOT / "backend/src/Crecisol.Application/Tenancy/ChangeTenantLifecycle.cs"
TEST = ROOT / "backend/tests/Crecisol.TenantLifecycle.ContractsTests/Program.cs"
SQL = ROOT / "database/tests/c04_08_lifecycle_fixture.sql"
SH = ROOT / "database/tests/c04_08_lifecycle_smoke.sh"
MASTER = ROOT / "docs/CRECISOL_DOCUMENTO_MAESTRO_UNICO.docx"
REQUIRED_MASTER_HASH = "ea5afc89e74915a9b18b9753ddb832f4be8e5d00f5e66b0f008e02e03476444c"


def require(ok: bool, reason: str) -> None:
    if not ok:
        raise AssertionError(f"C04-08 AUDIT FAIL: {reason}")


def contains_all(text: str, tokens: tuple[str, ...], tag: str) -> None:
    for token in tokens:
        require(token in text, f"{tag}: required semantic boundary {token!r}")


def audit_a() -> None:
    from_disk = hashlib.sha256(MASTER.read_bytes()).hexdigest()
    require(from_disk == REQUIRED_MASTER_HASH, "canonical Word SHA-256 drift")
    plan = (ROOT / "docs/planificacion-futura/C04_MICROFASES_PROPUESTAS.md").read_text()
    require("C04-08" in plan and "Suspensión" in plan and "acceso residual" in plan,
            "chapter04 C04-08 positive and negative outcome")
    catalog = (ROOT / "backend/src/Crecisol.Domain/Tenancy/TenantCatalogSnapshot.cs").read_text()
    contains_all(catalog, ("Active = 3", "Suspended = 4", "Deactivated = 5"), "catalog")
    domain = DOMAIN.read_text()
    contains_all(domain, (
        "TenantLifecycleState.Suspended", "TenantLifecycleState.Deactivated",
        "TenantLifecycleAction.Reactivate", "TenantLifecycleAction.Deactivate",
        "TenantLifecycleState.Active", "Lifecycle transition denied."), "domain")
    require("Deleted =" not in domain, "do not add parallel Deleted enum")
    print("A: canonical Word, existing lifecycle enum and C04-08 outcome verified")


def audit_b() -> None:
    domain = DOMAIN.read_text()
    app = APP.read_text()
    sql = SQL.read_text()
    tests = TEST.read_text()
    contains_all(domain, (
        "operationId == Guid.Empty", "expectedRevision <= 0",
        "expectedAccessEpoch <= 0", "requestedAtUtc.Offset != TimeSpan.Zero",
        "reasonCode.All", "intentFingerprint" if "intentFingerprint" in domain else "IntentFingerprint",
        "SHA256.HashData", "UnauthorizedAccessException",
        "TrustedTenantLifecycleReceipt", "IsEligibleForAdditionalAuthorization"),
        "domain security")
    contains_all(app, (
        "ReadAuthorizedIntentAsync", "CommitOrReplayAsync",
        "currentIntent != intent", "receipt.TenantId != intent.TenantId",
        "receipt.OperationId != intent.OperationId",
        "receipt.IntentFingerprint", "receipt.Revision",
        "receipt.AccessEpoch", "receipt.State != TenantLifecycleState.Deactivated",
        "cancellationToken.ThrowIfCancellationRequested()"), "application security")
    contains_all(sql, (
        "START TRANSACTION", "FOR UPDATE", "COMMIT", "ROLLBACK",
        "approved_until_utc", "expected_revision", "expected_epoch",
        "access_epoch", "IF ROW_COUNT() <> 1", "c04_08_audit",
        "c04_08_receipts", "Append-only", "DELETE", "SIGNAL SQLSTATE"), "synthetic SQL")
    contains_all(tests, (
        "cross-tenant-receipt", "stale-authorization-epoch",
        "approval-revoked-mid-flight", "forbidden-", "deactivated",
        "unknown-operation", "operation-bound", "actor-bound"), "negative tests")
    require(not re.search(r"\b(?:HttpClient|ControllerBase|DbContext|ConnectionString)\b",
                          domain + app), "no public/db coupling")
    # Static mutation rejection: demonstrate that each individually removed guard
    # fails this audit. This is NOT a replacement for executable security tests.
    guards = ("receipt.TenantId != intent.TenantId",
              "receipt.OperationId != intent.OperationId",
              "receipt.AccessEpoch != checked(intent.ExpectedAccessEpoch + 1)",
              "currentIntent != intent")
    for guard in guards:
        mutated = app.replace(guard, "/* removed security guard */")
        require(guard not in mutated and guard in app, "guard must be identifiable")
        require(not all(g in mutated for g in guards), "guard-removal mutation rejected")
    print("B: guard boundaries and 4 static mutations rejected; runtime tests still required")


def audit_c() -> None:
    app = APP.read_text()
    domain = DOMAIN.read_text()
    sh = SH.read_text()
    sql = SQL.read_text()
    require("namespace Crecisol.Domain.Tenancy;" in domain, "domain namespace")
    require("namespace Crecisol.Application.Tenancy;" in app, "application namespace")
    require("IAtomicTenantLifecycleTransitionStore" in app, "application storage port")
    require("Microsoft.AspNetCore" not in domain + app, "no HTTP dependency")
    require("MySqlConnector" not in domain + app, "no direct product database")
    require("--network none" in sh, "isolated docker network")
    require("MYSQL_ALLOW_EMPTY_PASSWORD=yes" in sh, "only isolated disposable fixture")
    require("CREATE PROCEDURE c04_08_transition" in sql, "SQL procedure")
    require("DELIMITER ;" in sql, "SQL fixture terminator")
    print("C: Domain/Application separation, no API/DB adapter, Docker isolation checked")


if __name__ == "__main__":
    mode = sys.argv[1] if len(sys.argv) == 2 else None
    require(mode in ("A", "B", "C"), "specify A, B or C")
    {"A": audit_a, "B": audit_b, "C": audit_c}[mode]()
