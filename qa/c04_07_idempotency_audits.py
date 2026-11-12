#!/usr/bin/env python3
"""CRECISOL C04-07: normative A, concurrency/security B, same-SHA real-runtime C.

A: pinned Word/PDF, 72 C04 paragraphs, original plan, non-production scope.
B: independent mutation of trust guards, uniqueness and transactional SQL.
C: .NET 10 + real offline MySQL8.4 A/B proof bound to SAME CI SHA/run.
No production provisioning, auth, billing, domains, DNS, HTTP or migrations.
"""
from __future__ import annotations

import os
import re
import sys

from c04_01_normative_inventory import EXPECTED, chapter4
from c04_01_preparation_audits import ROOT, canonical_active_word

PATHS = {
    "domain": ROOT / "backend/src/Crecisol.Domain/Tenancy/TenantProvisioningIdempotency.cs",
    "app": ROOT / "backend/src/Crecisol.Application/Tenancy/ProvisionApprovedTenantDraft.cs",
    "test": ROOT / "backend/tests/Crecisol.TenantProvisioning.ContractsTests/Program.cs",
    "ddl": ROOT / "database/tests/c04_07_idempotent_fixture.sql",
    "smoke": ROOT / "database/tests/c04_07_idempotency_smoke.sh",
    "spec": ROOT / "docs/c04/C04_07_ESPECIFICACION.md",
    "workflow": ROOT / ".github/workflows/c04-07-idempotent-provisioning.yml",
}
PROOF = ROOT / ".c04-07-idempotency-proof"

MARKERS = {
    "domain": (
        "public sealed record TenantProvisioningOperationId",
        "Guid.Empty",
        "public sealed record TenantProvisioningBusinessKey",
        "raw.Length is < 3 or > 64",
        "raw[0] is < 'a' or > 'z'",
        "raw[^1] is not",
        "public enum TenantProvisioningPlanState",
        "Approved = 1",
        "Revoked = 3",
        "public sealed record TrustedTenantProvisioningPlanSnapshot",
        "RestoreFromTrustedBackoffice(",
        "if (!Enum.IsDefined(addressMode) || !Enum.IsDefined(state) ||",
        "approvalRevision <= 0",
        "approvedAtUtc >= expiresAtUtc",
        "State == TenantProvisioningPlanState.Approved",
        "ApprovedAtUtc <= nowUtc && nowUtc < ExpiresAtUtc",
        "SHA256.HashData(",
        "operationId.Value.ToString(\"N\")",
        "tenantId.Value.ToString(\"N\")",
        "actorId.Value.ToString(\"N\")",
        "businessKey.Value + \"|\" +",
        "revision.ToString(CultureInfo.InvariantCulture)",
        "public sealed record TrustedTenantProvisioningReceipt",
        "RestoreFromTrustedAtomicStore(",
        "tenantLifecycle != TenantLifecycleState.Draft",
        "fingerprint.Length != 64",
        "public sealed record TenantProvisioningResult",
        "FromAtomicStore(",
        "receipt.OperationId != plan.OperationId",
        "receipt.TenantId != plan.TenantId",
        "receipt.SponsoringActorId != plan.SponsoringActorId",
        "receipt.BusinessKey != plan.BusinessKey",
        "receipt.AddressMode != plan.AddressMode",
        "receipt.IntentFingerprint, plan.IntentFingerprint",
        "receipt.TenantLifecycle != TenantLifecycleState.Draft",
        "receipt.CommittedAtUtc < plan.ApprovedAtUtc",
        "receipt.CommittedAtUtc > nowUtc",
        "TenantProvisioningResult[redacted]",
    ),
    "app": (
        "interface ITrustedTenantProvisioningPlanReader",
        "ReadByOperationIdAsync(",
        "interface IAtomicTenantProvisioningStore",
        "CommitOrReplayAsync(",
        "public sealed class ProvisionApprovedTenantDraft",
        "public async Task<TenantProvisioningResult> ExecuteAsync(",
        "TenantProvisioningOperationId operationId,",
        "CancellationToken cancellationToken)",
        "ArgumentNullException.ThrowIfNull(operationId)",
        "var plan = await plans.ReadByOperationIdAsync(operationId, cancellationToken)",
        "plan.OperationId != operationId",
        "if (!plan.IsApprovedAtUtc(startUtc))",
        "var committed = await store.CommitOrReplayAsync(plan, cancellationToken)",
        "committed is null || committed.Receipt is null",
        "var updatedPlan = await plans.ReadByOperationIdAsync(",
        "if (updatedPlan != plan)",
        "if (finishUtc < startUtc)",
        "TenantProvisioningResult.FromAtomicStore(",
        "cancellationToken.ThrowIfCancellationRequested()",
    ),
    "ddl": (
        "CREATE TABLE c04_07_operations",
        "CREATE TABLE c04_07_tenant_drafts",
        "ENGINE=InnoDB",
        "PRIMARY KEY (operation_id)",
        "PRIMARY KEY (tenant_id)",
        "UNIQUE KEY uq_c04_07_tenant (tenant_id)",
        "UNIQUE KEY uq_c04_07_business_key (business_key)",
        "UNIQUE KEY uq_c04_07_draft_op (operation_id)",
        "UNIQUE KEY uq_c04_07_draft_business (business_key)",
        "fk_c04_07_draft_operation FOREIGN KEY (operation_id)",
        "ON DELETE RESTRICT ON UPDATE RESTRICT",
        "chk_c04_07_draft_state CHECK (lifecycle_state = 1)",
        "CREATE TRIGGER trg_c04_07_op_update",
        "CREATE TRIGGER trg_c04_07_op_delete",
        "CREATE TRIGGER trg_c04_07_draft_update",
        "CREATE TRIGGER trg_c04_07_draft_delete",
        "SIGNAL SQLSTATE '45000'",
        "CREATE PROCEDURE c04_07_create_or_replay(",
        "SQL SECURITY DEFINER",
        "DECLARE EXIT HANDLER FOR SQLEXCEPTION",
        "ROLLBACK;",
        "START TRANSACTION;",
        "DECLARE CONTINUE HANDLER FOR 1062 SET v_duplicate = TRUE",
        "INSERT INTO c04_07_operations",
        "IF v_duplicate THEN",
        "WHERE operation_id = p_operation FOR UPDATE",
        "NOT (v_tenant <=> p_tenant)",
        "NOT (v_actor <=> p_actor)",
        "NOT (v_business <=> p_business)",
        "NOT (v_mode <=> p_address_mode)",
        "NOT (v_fingerprint <=> p_fingerprint)",
        "IF v_draft_count != 1",
        "INSERT INTO c04_07_tenant_drafts",
        "COMMIT;",
        "SELECT 'REPLAY' AS outcome",
        "SELECT 'CREATED' AS outcome",
        "NOT REGEXP_LIKE(p_business, '^[a-z][a-z0-9-]{1,62}[a-z0-9]$', 'c') THEN",
    ),
    "smoke": (
        "mysql:8.4@sha256:",
        "--network none",
        "trap cleanup EXIT",
        "GRANT EXECUTE ON PROCEDURE",
        "c04_07_ci_runner",
        "SELECT COUNT(*) FROM c04_07_operations",
        "no duplicate business identity created",
        "retry after crash commits once",
        "lost response after commit returns replay",
        "same-key race one committed draft",
        "same-key race one committed operation",
        "different keys same business: one winner",
        "different keys same business: 11 conflicts",
        "same operation with changed fingerprint",
        "same operation with another tenant",
        "same operation with different sponsor",
        "same operation with different address mode",
        "new operation may not clone business identity",
        "new operation may not reassign tenant id",
        "synthetic mid-transaction crash forces rollback",
        "crashed transaction left no operation",
        "crashed transaction left no tenant",
        "operation tenant rekey",
        "tenant draft business rekey",
        "MySQL8.4",
    ),
    "test": (
        "48 actual concurrent same-operation attempts: 1 CREATED, 47 REPLAY",
        "24 concurrent DIFFERENT operations requesting same global business key",
        "Same operation key with altered payload cannot replay",
        "Different operation same internal business identity denied",
        "Different operation same tenant ID with different business identity denied",
        "Lost response AFTER durable commit bubbles up safely",
        "After crash, retry commits Draft exactly once",
        "Approval revoked after DB write denies response",
        "Receipt from another tenant rejected",
        "Receipt from different sponsoring actor rejected",
        "Replayed receipt from different operation rejected",
        "Receipt for a different address modality rejected",
        "Forged valid-format but wrong fingerprint rejected",
        "Plan expires during operation",
        "Null operation selector denied",
        "Use case accepts ONLY operation selector + cancellation",
        "No writes after cancellation of plan read",
        "Retry recovers committed Draft after caller cancellation",
        "NO real production writer, approved admin identity, HTTP",
    ),
    "spec": (
        EXPECTED,
        "C04-07 NO CERTIFICADA",
        "C04-08 BLOQUEADA",
        "No duplicar tenants por carreras",
        "12 solicitudes concurrentes",
        "48 reintentos simultáneos",
        "NO implementados",
    ),
    "workflow": (
        "dotnet-version: '10.0.x'",
        "qa/c04_07_idempotency_audits.py A",
        "qa/c04_07_idempotency_audits.py B",
        "qa/c04_07_idempotency_audits.py C",
        "Crecisol.TenantProvisioning.ContractsTests.csproj",
        "database/tests/c04_07_idempotency_smoke.sh",
        "dotnet run --no-build",
        "persist-credentials: false",
        "contents: read",
    ),
}


def require(ok: bool, message: str) -> None:
    if not ok:
        raise AssertionError(message)


def load() -> dict[str, str]:
    return {key: path.read_text(encoding="utf-8") for key, path in PATHS.items()}


def guard(data: dict[str, str]) -> None:
    for key, markers in MARKERS.items():
        for marker in markers:
            require(marker in data[key], f"{key}: missing security guard: {marker}")

    domain, app, ddl, test, smoke, workflow = (
        data["domain"], data["app"], data["ddl"], data["test"],
        data["smoke"], data["workflow"])
    require(domain.count("public sealed record") == 5,
            "Only 5 opaque and trusted domain types are in C04-07")
    require(app.count("plans.ReadByOperationIdAsync(") == 2,
            "Approved plan MUST be re-read after commit")
    require(app.count("store.CommitOrReplayAsync(plan, cancellationToken)") == 1,
            "No retries/inserts outside one trusted atomic storage operation")
    require(app.index("var plan = await plans.ReadByOperationIdAsync") <
            app.index("var committed = await store.CommitOrReplayAsync") <
            app.index("var updatedPlan = await plans.ReadByOperationIdAsync") <
            app.index("TenantProvisioningResult.FromAtomicStore("),
            "Read approval -> atomic commit -> verify approval -> receipt sequence required")
    require(ddl.count("CREATE TRIGGER trg_c04_07_") == 4,
            "All four table write/erase triggers must exist")
    require(ddl.count("UNIQUE KEY uq_c04_07_") == 4,
            "Four additional unique indexes required beyond the two PKs")
    executable_sql = "\n".join(line.split("--", 1)[0] for line in ddl.splitlines())
    require(not re.search(r"\bINSERT\s+IGNORE\b|\bON\s+DUPLICATE\s+KEY\s+UPDATE\b",
                          executable_sql, re.IGNORECASE),
            "No unsafe duplicate key mutation permitted")
    require(ddl.index("START TRANSACTION;") <
            ddl.index("INSERT INTO c04_07_operations") <
            ddl.index("IF v_duplicate THEN") <
            ddl.index("INSERT INTO c04_07_tenant_drafts"),
            "Atomic row insert + replay inspection ordering required")
    require("DROP TABLE" not in ddl and "CREATE USER" not in ddl,
            "Only isolated creation fixture: no schema modification in existing DB")
    require("--network none" in smoke and "-p 127." not in smoke and
            "docker run" in smoke and "trap cleanup EXIT" in smoke,
            "MySQL integration only in disposable no-network container")
    require("https://" not in smoke and "curl " not in smoke and
            "deploy" not in workflow.lower() and "docker run" not in workflow,
            "No external provider, CI deploy, remote networking or production changes")
    require("SyntheticAtomicStore" in test and "FakePlanReader" in test,
            "Tests must clearly declare fake storage/approval catalog")
    require("actions/checkout@" in workflow and "actions/setup-dotnet@" in workflow,
            "CI must use pinned Actions, without deployment")
    for source in (ROOT / "backend/src/Crecisol.Infrastructure").rglob("*.cs"):
        body = source.read_text(encoding="utf-8")
        require("IAtomicTenantProvisioningStore" not in body and
                "ITrustedTenantProvisioningPlanReader" not in body,
                "Unexpected production provisioning adapter introduced")
    api = (ROOT / "backend/src/Crecisol.Api/Program.cs").read_text(encoding="utf-8")
    require("ProvisionApprovedTenantDraft" not in api and
            "IAtomicTenantProvisioningStore" not in api and
            "c04_07" not in api,
            "Private provisioning endpoint/DI introduced without authorization")
    require("using Microsoft" not in domain and "using Microsoft" not in app and
            "HttpContext" not in app and "MySqlConnection" not in app and
            "MySqlConnection" not in domain,
            "No framework/DB dependency in Domain/Application")


def audit_a() -> None:
    require(canonical_active_word(), "Word/PDF SHA changed")
    contents = chapter4()
    require(len(contents) == 72, "C04 original paragraph count changed")
    source = "\n".join(t for _, t in contents)
    for phrase in ("SHARED A/B y DEDICATED C",
                   "nunca confiar en tenant_id aportado por navegador",
                   "FK compuestas con tenant"):
        require(phrase in source, "Canonical Word C04 missing: " + phrase)
    plan = (ROOT / "docs/planificacion-futura/C04_MICROFASES_PROPUESTAS.md").read_text(
        encoding="utf-8")
    require("| C04-07 | Aprovisionamiento idempotente |" in plan,
            "C04-07 original contract changed")
    require("Creación sintética y recuperación ante reintento" in plan and
            "No duplicar tenants por carreras" in plan,
            "Original positive/negative C04-07 contract changed")
    predecessor = (ROOT / "docs/c04/C04_06_ACTA_CERTIFICACION.md").read_text(
        encoding="utf-8")
    require("PR técnico #94" in predecessor and "C04-06" in predecessor,
            "Required predecessor C04-06 evidence unavailable")
    guard(load())
    print("PASS C04-07 A: original Word/PDF SHA and C04 72 paragraphs, "
          "strict C04-07 provisioning scope and no product/API/production changes")


def audit_b() -> None:
    data = load()
    guard(data)
    rejected = 0
    for key, markers in MARKERS.items():
        for marker in markers:
            mutated = dict(data)
            mutated[key] = mutated[key].replace(
                marker, "[REMOVED_IDEMPOTENCY_SECURITY_GUARD]")
            try:
                guard(mutated)
            except AssertionError:
                rejected += 1
            else:
                raise AssertionError("Undetected idempotency mutation: " +
                                     key + ": " + marker)
    print(f"PASS C04-07 B: {rejected} independently removed trust guards "
          "were all rejected; unique operations+tenants+business names, "
          "exact full-intent replay and no production API")


def audit_c() -> None:
    require(os.environ.get("GITHUB_ACTIONS") == "true",
            "C04-07 C needs actual GitHub Actions runner")
    sha = os.environ.get("GITHUB_SHA", "")
    run = os.environ.get("GITHUB_RUN_ID", "")
    require(bool(re.fullmatch(r"[0-9a-f]{40}", sha) and
                 re.fullmatch(r"[0-9]+", run)), "Malformed SHA/run identifier")
    for phase in ("A", "B", "dotnet", "mysql"):
        path = PROOF / phase
        require(path.is_file() and path.read_text(encoding="utf-8").strip() ==
                f"PASS {phase} sha={sha} run={run}",
                "Missing/stale SAME CI SHA/run evidence: " + phase)
    for path in (
        ROOT / "backend/src/Crecisol.Domain/Crecisol.Domain.csproj",
        ROOT / "backend/src/Crecisol.Application/Crecisol.Application.csproj",
        ROOT / "backend/tests/Crecisol.TenantProvisioning.ContractsTests/Crecisol.TenantProvisioning.ContractsTests.csproj",
    ):
        require("<TargetFramework>net10.0</TargetFramework>" in
                path.read_text(encoding="utf-8"), "Not real .NET10: " + str(path))
    app_project = (ROOT / "backend/src/Crecisol.Application/Crecisol.Application.csproj").read_text(
        encoding="utf-8")
    require("Crecisol.Domain.csproj" in app_project and
            "Crecisol.Infrastructure.csproj" not in app_project,
            "Clean Domain/Application dependency bypass")
    guard(load())
    print("PASS C04-07 C: SAME-SHA real .NET10 Release and MySQL8.4 "
          "InnoDB concurrency+rollback+replay, no production implementation")


def main() -> None:
    require(len(sys.argv) == 2 and sys.argv[1] in ("A", "B", "C"),
            "Usage: qa/c04_07_idempotency_audits.py A|B|C")
    {"A": audit_a, "B": audit_b, "C": audit_c}[sys.argv[1]]()


if __name__ == "__main__":
    try:
        main()
    except (AssertionError, OSError, ValueError) as exc:
        print("FAIL C04-07:", exc, file=sys.stderr)
        sys.exit(1)
