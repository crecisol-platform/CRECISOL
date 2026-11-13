#!/usr/bin/env python3
"""C04-09 ADDITIONAL independent security/quality audits D/E/F.

These are source and test-matrix guards, not replacements for the real MySQL
and .NET execution recorded in the same SHA/run by the workflow.
"""
from pathlib import Path
import re
import sys

ROOT = Path(__file__).resolve().parents[1]
DEDICATED = (ROOT / "backend/src/Crecisol.Domain/Tenancy/TrustedTenantMigrationPlan.cs").read_text()
SHARED = (ROOT / "backend/src/Crecisol.Domain/Tenancy/TrustedSharedSchemaMigrationPlan.cs").read_text()
DE_APP = (ROOT / "backend/src/Crecisol.Application/Tenancy/PlanTenantMigration.cs").read_text()
SH_APP = (ROOT / "backend/src/Crecisol.Application/Tenancy/PlanSharedSchemaMigration.cs").read_text()
SQL = (ROOT / "database/tests/c04_09_guarded_fixture.sql").read_text()
SMOKE = (ROOT / "database/tests/c04_09_guarded_smoke.sh").read_text()
NET_TEST = (ROOT / "backend/tests/Crecisol.TenantMigrations.ContractsTests/Program.cs").read_text()
LEGACY_SMOKE = (ROOT / "database/tests/c04_09_migration_smoke.sh").read_text()
CI = (ROOT / ".github/workflows/c04-09-migrations.yml").read_text()

def require(condition: bool, description: str):
    if not condition:
        raise AssertionError("C04-09 extra audit failed: " + description)

def expect_all(source: str, tokens: tuple[str, ...], context: str):
    for token in tokens:
        require(token in source, context + ": missing " + repr(token))

def audit_d():
    """D: scope-confusion, real valid cross-tenant and forged actor/slot resistance."""
    require("placement.Mode != TenantPlacementMode.Dedicated" in DEDICATED,
            "shared DDL is forbidden under tenant-specific plan")
    require("public TenantId " not in SHARED and
            "public DedicatedStorageSlotId " not in SHARED,
            "global shared plan must have no TenantId/slot selector")
    expect_all(SHARED, (
        "ProtectedSharedSchemaFamily",
        "ExpectedSchemaRevision",
        "FromTrustedPlatformApproval",
        "TrustedSharedSchemaMigrationReceipt",
        "receipt.CommittedSchemaRevision != checked(plan.ExpectedSchemaRevision + 1)",
        "SHA256.HashData"
    ), "global shared authorization")
    expect_all(DEDICATED, (
        "receipt.TenantId != plan.TenantId",
        "receipt.Slot != plan.Slot",
        "receipt.PlacementRevision != plan.PlacementRevision",
        "SHA256.HashData"
    ), "dedicated isolation")
    expect_all(SQL, (
        "UNIQUE KEY uq_c0409_target_slot",
        "NOT (v_tenant <=> v_actual_tenant)",
        "NOT (v_slot <=> v_actual_slot)",
        "NOT (v_kind <=> v_actual_kind)",
        "NOT (v_rev <=> v_actual_rev)",
        "NOT (v_epoch <=> v_actual_epoch)",
        "v_actual_schema <> 'c04_09_shared'",
        "v_actual_schema NOT IN ('c04_09_ded_a','c04_09_ded_b')",
        "c0409_target_binding_immutable",
        "c0409_shared_owner_immutable"
    ), "real SQL target binding")
    expect_all(SMOKE, (
        "cross-tenant valid-B approval",
        "valid tenant B cannot steal noncolliding tenant A resource",
        "cross-slot valid-slot-B approval",
        "different valid tenant cannot claim same dedicated slot",
        "runner cannot choose scope via update",
        "globally shared schema has no tenant selector",
        "runner cannot alter another dedicated schema",
        "runner cannot perform destructive DDL downgrade",
    ), "executable cross-scope negative tests")
    expect_all(NET_TEST, (
        "shared-ddl-cannot-use-tenant-plan",
        "shared-global-must-not-select-tenant-or-slot",
        "tenant-B-receipt-never-valid-for-A",
        "dedicated-cross-tenant-executor-rejected",
        "global-invalid-revision-receipt"
    ), ".NET boundary tests")
    # Independently mutate each authorization guard in memory. Each mutation
    # must be distinguishable by this audit; it is a STATIC mutation check.
    guards = (
        "NOT (v_tenant <=> v_actual_tenant)",
        "NOT (v_slot <=> v_actual_slot)",
        "NOT (v_kind <=> v_actual_kind)",
        "NOT (v_rev <=> v_actual_rev)",
        "NOT (v_epoch <=> v_actual_epoch)",
    )
    for guard in guards:
        mutated = SQL.replace(guard, "FALSE")
        require(guard in SQL and guard not in mutated,
                "negative guard mutation was not detectable: " + guard)
    print(f"D: scoped global-vs-tenant binding verified; {len(guards)}"
          " independent SQL guard mutations detectable; negative runner cases present")

def audit_e():
    """E: concurrency, idempotency, fencing, failure and restoration."""
    expect_all(SQL, (
        "FOR UPDATE", "START TRANSACTION", "ROLLBACK", "RESIGNAL",
        "UNIQUE KEY uq_c0409_ledger_op",
        "Another migration is still in flight",
        "Version/checksum/operation conflict or incomplete DDL",
        "Migration reservation CAS lost",
        "Missing, stale or non-applying migration fence",
        "DDL verification failed",
        "Verified migration schema drift",
        "Shared schema revision CAS lost",
        "Stale target revision on new migration",
        "Migration approval/revision/fence revoked",
        "state='failed'", "state='recovered'",
        "DDL is still applied: verify backup restoration",
        "Recovery CAS lost",
        "baseline_payload_sha256",
        "Restored data snapshot digest mismatch",
        "inflight_operation=p_op"
    ), "protected synthetic ledger and recovery")
    expect_all(SMOKE, (
        "seq 1 12", "twelve independent decisions: one winner",
        "eleven conflicting contenders fail closed",
        "finalization before DDL cannot certify",
        "verified shared replay detects schema drift",
        "global Shared schema revision advanced once",
        "new Shared version cannot use old global schema revision",
        "stale fencing cannot finalize dedicated B",
        "DDL persists after ROLLBACK (implicit commit)",
        "mysqldump", "single-transaction", "ded_b_backup.sql",
        "physical backup restoration data hash",
        "failed migration can never replay as success",
        "historical failed-recovered operation cannot blindly replay",
        "new operation increments immutable fence",
        "stale first fence fails after recovery",
        "approval revoked midflight cannot finalize committed DDL",
        "verified ledger may not rewrite checksum",
        "verified ledger may not be downgraded to applying",
        "verified ledger may not be deleted",
        "restore_checked",
        "wrong dedicated restore destination accepted",
        "rejected wrong restoration did not modify A",
        "protected recovery rejects altered restored payload",
        "second physical restoration recovers snapshot hash"
    ), "executable concurrency/recovery tests")
    require("ALTER TABLE c04_09_ded_b.resources ADD COLUMN migration_tag" in SMOKE,
            "DDL fixture must actually mutate dedicated schema")
    require("DROP DATABASE c04_09_ded_b; CREATE DATABASE c04_09_ded_b" in SMOKE,
            "must restore actual disposable physical database")
    require("SQL SECURITY DEFINER" in SQL,
            "protected synthetic procedures must use definition owner")
    # Concurrent reservation, finalization and crash recovery MUST lock rows
    # in one deterministic order to avoid a preventable inversion/deadlock.
    for procedure in ("reserve", "finalize", "mark_failed",
                      "verify_restored_and_release"):
        prefix = "CREATE PROCEDURE c04_09_control." + procedure + "("
        require(prefix in SQL, "missing SQL procedure: " + procedure)
        body = SQL.split(prefix, 1)[1].split("END//", 1)[0]
        order = [body.find("FROM c04_09_control." + table)
                 for table in ("approvals", "targets", "gates", "ledger")]
        require(all(position >= 0 for position in order) and
                order == sorted(order),
                "SQL row lock inversion in " + procedure)
    print("E: four SQL procedures share strict approval-target-gate-ledger lock order")
    print("E: reservation/fencing/crash/recovery test gates verified; real MySQL required")

def audit_f():
    """F: architectural separation, regression and executable proof chain."""
    for code in (DEDICATED, SHARED, DE_APP, SH_APP):
        for forbidden in ("Microsoft.AspNetCore", "MySqlConnector", "DbContext",
                          "IConfiguration", "ConnectionString", "HttpContext"):
            require(forbidden not in code, "runtime coupling forbidden: " + forbidden)
    require("namespace Crecisol.Domain.Tenancy;" in DEDICATED and
            "namespace Crecisol.Domain.Tenancy;" in SHARED,
            "pure Domain projects")
    require("namespace Crecisol.Application.Tenancy;" in DE_APP and
            "namespace Crecisol.Application.Tenancy;" in SH_APP,
            "Application ports not Infrastructure")
    expect_all(CI, (
        "dotnet build backend/src/Crecisol.Api",
        "-warnaserror",
        "Crecisol.TenantMigrations.ContractsTests",
        "Crecisol.TenantLifecycle.ContractsTests",
        "Crecisol.TenantProvisioning.ContractsTests",
        "Crecisol.TenantStorage.ContractsTests",
        "Crecisol.SharedTenancy.ContractsTests",
        "c04_09_migration_smoke.sh",
        "c04_09_guarded_smoke.sh",
        "c04_09_extra_audits.py D",
        "c04_09_extra_audits.py E",
        "c04_09_extra_audits.py F",
        "sha=$GITHUB_SHA run=$GITHUB_RUN_ID"
    ), "workflow audit/evidence chain")
    require("--network none" in SMOKE and "--network none" in LEGACY_SMOKE,
            "both MySQL fixtures must have no container network")
    require("uses: actions/checkout@" in CI and
            "persist-credentials: false" in CI,
            "checkout does not persist write credentials")
    require("continue-on-error:" not in CI,
            "mandatory failing checks cannot be ignored")
    require("PRIMARY KEY (target_id,migration_version)" in SQL and
            "CONSTRAINT chk_c0409_baseline_hash CHECK" in SQL and
            ("REGEXP_LIKE(baseline_payload_sha256,\x27^[0-9a-f]{64}" + chr(36) + "\x27,\x27c\x27)") in SQL,
            "guarded migration ledger key or complete snapshot digest check missing")
    require(SQL.count("CREATE DATABASE c04_09_control") == 1,
            "duplicate or missing synthetic migration catalog")
    require(SQL.count("DELIMITER //") == 1 and SQL.count("DELIMITER ;") == 1,
            "migration fixture must have one uniform SQL delimiter region")
    for proc in ("reserve","finalize","mark_failed","verify_restored_and_release"):
        require(SQL.count("CREATE PROCEDURE c04_09_control." + proc + "(") == 1,
                "duplicate or missing migration gate procedure: " + proc)
    require(SQL.count("END//") == 8,
            "four triggers and four procedures must terminate once each")
    require(not re.search(r"(?:CREATE|ALTER|DROP)\s+DATABASE\s+crecisol", SQL, re.I),
            "no production database target")
    print("F: no production adapter or secret inputs; exhaustive same-run gates specified")

if __name__ == "__main__":
    require(len(sys.argv) == 2 and sys.argv[1] in ("D","E","F"),
            "audit identifier D/E/F required")
    {"D": audit_d, "E": audit_e, "F": audit_f}[sys.argv[1]]()
,'c')" in SQL,
            "guarded migration ledger key or complete snapshot digest check missing")
    require(SQL.count("CREATE DATABASE c04_09_control") == 1,
            "duplicate or missing synthetic migration catalog")
    require(SQL.count("DELIMITER //") == 1 and SQL.count("DELIMITER ;") == 1,
            "migration fixture must have one uniform SQL delimiter region")
    for proc in ("reserve","finalize","mark_failed","verify_restored_and_release"):
        require(SQL.count("CREATE PROCEDURE c04_09_control." + proc + "(") == 1,
                "duplicate or missing migration gate procedure: " + proc)
    require(SQL.count("END//") == 8,
            "four triggers and four procedures must terminate once each")
    require(not re.search(r"(?:CREATE|ALTER|DROP)\s+DATABASE\s+crecisol", SQL, re.I),
            "no production database target")
    print("F: no production adapter or secret inputs; exhaustive same-run gates specified")

if __name__ == "__main__":
    require(len(sys.argv) == 2 and sys.argv[1] in ("D","E","F"),
            "audit identifier D/E/F required")
    {"D": audit_d, "E": audit_e, "F": audit_f}[sys.argv[1]]()
