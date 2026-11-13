#!/usr/bin/env python3
"""C04-09 static A/B/C gates; real .NET/MySQL evidence required separately."""
import hashlib
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
DOMAIN = (ROOT / "backend/src/Crecisol.Domain/Tenancy/TrustedTenantMigrationPlan.cs").read_text()
APP = (ROOT / "backend/src/Crecisol.Application/Tenancy/PlanTenantMigration.cs").read_text()
TEST = (ROOT / "backend/tests/Crecisol.TenantMigrations.ContractsTests/Program.cs").read_text()
SMOKE = (ROOT / "database/tests/c04_09_migration_smoke.sh").read_text()

def required(condition, label):
    if not condition:
        raise AssertionError("C04-09 " + label)

def contains_all(src, expected, area):
    for token in expected:
        required(token in src, area + ": " + token)

def audit_a():
    master = ROOT / "docs/CRECISOL_DOCUMENTO_MAESTRO_UNICO.docx"
    expected = "ea5afc89e74915a9b18b9753ddb832f4be8e5d00f5e66b0f008e02e03476444c"
    required(hashlib.sha256(master.read_bytes()).hexdigest() == expected, "canonical master hash mismatch")
    plan = (ROOT / "docs/planificacion-futura/C04_MICROFASES_PROPUESTAS.md").read_text()
    contains_all(plan, ("C04-09", "Migraciones shared y dedicated",
                        "No migrar tenant equivocado"), "normative plan")
    contains_all(DOMAIN, ("TenantPlacementMode", "DedicatedStorageSlotId",
                          "PlacementRevision", "MigrationVersion", "Checksum"),
                 "domain migration scope")
    shared = (ROOT / "backend/src/Crecisol.Domain/Tenancy/TrustedSharedSchemaMigrationPlan.cs").read_text()
    contains_all(shared, ("PlatformShared", "ProtectedSharedSchemaFamily",
                          "ExpectedSchemaRevision", "SchemaFamily"), "platform-wide schema authority")
    required("TenantId" not in shared.replace("TenantId, DedicatedStorageSlotId", ""),
             "shared scope cannot carry a per-tenant identifier")
    print("C04-09 AUDIT A: master and scope traced (not runtime proof)")

def audit_b():
    contains_all(DOMAIN, ("operationId == Guid.Empty", "placement.Revision <= 0",
                          "placement.DedicatedSlot", "checksum.Length != 64",
                          "receipt.TenantId != plan.TenantId",
                          "receipt.Slot != plan.Slot", "receipt.Checksum",
                          "receipt.PlacementRevision != plan.PlacementRevision",
                          "SHA256.HashData"), "domain trust boundary")
    contains_all(APP, ("ReadCurrentApprovedPlanAsync", "ExecuteOrReplayAsync",
                       "confirmed != plan", "RequireExactMatch",
                       "cancellationToken.ThrowIfCancellationRequested()"),
                 "application auth gate")
    contains_all(TEST, ("tenant-B-receipt-never-valid-for-A", "wrong-dedicated-slot",
                        "dedicated-checksum-mismatch", "stale-placement-revision",
                        "dedicated-approval-revoked-during-execution",
                        "shared-ddl-cannot-use-tenant-plan",
                        "global-approval-revoked-during-execution"), "negative tests")
    guards = ("receipt.TenantId != plan.TenantId",
              "receipt.Slot != plan.Slot",
              "receipt.PlacementRevision != plan.PlacementRevision",
              "!string.Equals(receipt.Checksum, plan.Checksum, StringComparison.Ordinal)")
    for guard in guards:
        required(guard in DOMAIN and guard not in DOMAIN.replace(guard, "false"),
                 "security mutation must be rejected: " + guard)
    print("C04-09 AUDIT B: guard coverage, four static mutations (not security penetration tests)")

def audit_c():
    contains_all(SMOKE, ("--network none", "mysql:8.4@", "c04_09_shared",
                         "c04_09_ded_a", "c04_09_ded_b",
                         "compensating DDL", "fk_c0409_tenant"), "synthetic SQL isolation")
    required("namespace Crecisol.Domain.Tenancy;" in DOMAIN, "domain separation")
    required("namespace Crecisol.Application.Tenancy;" in APP, "application separation")
    shared_d = (ROOT / "backend/src/Crecisol.Domain/Tenancy/TrustedSharedSchemaMigrationPlan.cs").read_text()
    shared_a = (ROOT / "backend/src/Crecisol.Application/Tenancy/PlanSharedSchemaMigration.cs").read_text()
    for prohibited in ("Microsoft.AspNetCore", "MySqlConnector", "DbContext",
                       "ConnectionString", "http://", "https://"):
        required(prohibited not in DOMAIN + APP + shared_d + shared_a,
                 "direct coupling: " + prohibited)
    print("C04-09 AUDIT C: clean domain and isolated DDL harness, no prod runner")

if __name__ == "__main__":
    required(len(sys.argv) == 2 and sys.argv[1] in ("A", "B", "C"), "mode A/B/C required")
    {"A": audit_a, "B": audit_b, "C": audit_c}[sys.argv[1]]()
