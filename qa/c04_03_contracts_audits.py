#!/usr/bin/env python3
"""C04-03 .NET10 typed storage contract audit A/B/C (NO dedicated DB router).

A: canonical Word + C04-03 normative map + typed scope.
B: adversarial single-boundary mutation rejection and no API/DSN.
C: genuine .NET10 test proof bound to one GitHub SHA/run.
"""
from __future__ import annotations

import os
from pathlib import Path
import re
import sys

from c04_01_normative_inventory import EXPECTED, chapter4
from c04_01_preparation_audits import ROOT, canonical_active_word

PATHS = {
    "domain": ROOT / "backend/src/Crecisol.Domain/Tenancy/TenantStoragePlacement.cs",
    "app": ROOT / "backend/src/Crecisol.Application/Tenancy/ResolveCurrentTenantStorage.cs",
    "test": ROOT / "backend/tests/Crecisol.TenantStorage.ContractsTests/Program.cs",
    "spec": ROOT / "docs/c04/C04_03_ESPECIFICACION.md",
    "workflow": ROOT / ".github/workflows/c04-03-storage-contracts.yml",
}
PROOF = ROOT / ".c04-03-contract-proof"

MARKERS = {
    "domain": (
        "public enum TenantPlacementMode",
        "Shared = 1",
        "Dedicated = 2",
        "Guid.Empty",
        "TenantPlacementMode.Shared && dedicatedSlot is not null",
        "TenantPlacementMode.Dedicated && dedicatedSlot is null",
        "if (!Enum.IsDefined(mode))",
        "if (revision <= 0)",
        "updatedAtUtc.Offset != TimeSpan.Zero",
        "DedicatedStorageSlotId[redacted]",
        "TenantStoragePlacementSnapshot[redacted]",
    ),
    "app": (
        "ICurrentTenantPlacementAuthorization",
        "ITrustedTenantPlacementReader",
        "ResolveAuthorizedCurrentTenantAsync(cancellationToken);",
        "if (tenant is null)",
        "throw new UnauthorizedAccessException",
        "var placement = await reader.FindPlacementAsync(tenant, cancellationToken);",
        "if (placement is null)",
        "if (placement.TenantId != tenant)",
        "cancellationToken.ThrowIfCancellationRequested();",
        "public async Task<TenantStoragePlacementSnapshot> ExecuteAsync(",
    ),
    "test": (
        "Only Shared and Dedicated placement modes exist",
        "Unknown enum rejected",
        "Shared cannot contain dedicated slot",
        "Dedicated requires slot",
        "Reader for tenant A returning tenant B rejected (no cross tenant)",
        "Denial happens before catalog I/O",
        "Unknown placement fails closed without shared fallback",
        "Authorization exception cannot call catalog",
        "Cancellation during authorization rejects access",
        "Cancellation during placement read rejects return",
        "Use case accepts NO tenant, host, cookie, header or DSN argument",
        "five read-only typed properties and NO string DSN/host",
    ),
    "spec": (
        EXPECTED,
        "C04-03 PREPARADA PARA IMPLEMENTAR CONTRATOS INTERNOS",
        "C04-03 **NO CERTIFICADA**",
        "C04-04 **BLOQUEADA**",
        "No DSN desde URL, cookie o cabecera",
    ),
    "workflow": (
        "dotnet-version: '10.0.x'",
        "qa/c04_03_contracts_audits.py A",
        "qa/c04_03_contracts_audits.py B",
        "qa/c04_03_contracts_audits.py C",
        "Crecisol.TenantStorage.ContractsTests.csproj",
        "dotnet run --no-build",
        "persist-credentials: false",
        "contents: read",
    ),
}


def require(condition: bool, issue: str) -> None:
    if not condition:
        raise AssertionError(issue)


def load() -> dict[str, str]:
    return {key: path.read_text(encoding="utf-8") for key, path in PATHS.items()}


def guard(files: dict[str, str]) -> None:
    for kind, markers in MARKERS.items():
        for marker in markers:
            require(marker in files[kind], f"{kind}: missing control: {marker}")
    domain = files["domain"]
    app = files["app"]
    test = files["test"]
    workflow = files["workflow"]
    require(domain.count("public enum TenantPlacementMode") == 1,
            "Exactly one shared/dedicated enum is required")
    require(domain.count("public sealed record") == 2,
            "Only opaque slot and immutable snapshot are allowed")
    require("using MySql" not in domain and "using Microsoft" not in domain
            and "using MySql" not in app and "using Microsoft" not in app,
            "Domain/Application cannot own provider or HTTP frameworks")
    require("public async Task<TenantStoragePlacementSnapshot> ExecuteAsync(\n"
            "        CancellationToken cancellationToken)" in app,
            "No caller-controlled placement routing input permitted")
    require("return placement;" in app and "return null" not in app,
            "Missing/denied placement cannot fall back to shared")
    require(app.count("reader.FindPlacementAsync(tenant, cancellationToken)") == 1,
            "Exactly one trusted placement catalogue read allowed")
    require(app.count("ResolveAuthorizedCurrentTenantAsync(cancellationToken)") == 1,
            "One authorization must precede one storage read")
    require(app.index("ResolveAuthorizedCurrentTenantAsync") <
            app.index("reader.FindPlacementAsync(tenant, cancellationToken)"),
            "Storage read must never precede authorization")
    require("new MySqlConnection" not in app and "new MySqlConnection" not in domain,
            "No premature live DSN/provider connections")
    require("ConnectionStringBuilder" not in domain and
            "ConnectionStringBuilder" not in app and "ConnectionStringBuilder" not in test,
            "No credentials, DSN or connection builders in contracts")
    require("StringBuilder" not in domain and "StringBuilder" not in app,
            "Typed placement must not construct a connection string")
    require("TestAuthorization" in test and "TestPlacementReader" in test,
            "Tests must disclose fake actor/catalog providers")
    require("actions/checkout@" in workflow and
            "actions/setup-dotnet@" in workflow,
            "CI dependencies must be pinned actions")
    require("create-pull-request" not in workflow and "docker run" not in workflow
            and "deploy" not in workflow.lower(),
            "Contract-only workflow must not provision, deploy or connect")


def audit_a() -> None:
    require(canonical_active_word(), "Canonical Word/PDF digest mismatch")
    rows = chapter4()
    require(len(rows) == 72, "Original chapter C04 count diverged")
    source = "\n".join(s for _, s in rows)
    for token in ("SHARED A/B y DEDICATED C",
                  "nunca confiar en tenant_id aportado por navegador",
                  "FK compuestas con tenant"):
        require(token in source, "Master C04 requirement missing: " + token)
    planned = (ROOT / "docs/planificacion-futura/C04_MICROFASES_PROPUESTAS.md").read_text(encoding="utf-8")
    require("| C04-03 | Contratos de almacenamiento dedicado |" in planned,
            "C04-03 original scope changed")
    require("No DSN desde URL, cookie o cabecera" in planned,
            "C04-03 original negative gate missing")
    guard(load())
    print("PASS C04-03 A: canonical 72 C04 Word paragraphs, shared/dedicated "
          "placement contracts, typed Domain/Application, no routing or API")


def audit_b() -> None:
    files = load()
    guard(files)
    count = 0
    for kind, markers in MARKERS.items():
        for token in markers:
            mutant = dict(files)
            mutant[kind] = mutant[kind].replace(token, "[REMOVED TRUST BOUNDARY]")
            try:
                guard(mutant)
            except AssertionError:
                count += 1
            else:
                raise AssertionError("Unsafe mutation accepted: " + kind + "/" + token)
    api = (ROOT / "backend/src/Crecisol.Api/Program.cs").read_text(encoding="utf-8")
    require("ResolveCurrentTenantStorage" not in api and
            "ITrustedTenantPlacementReader" not in api and
            "DedicatedStorageSlotId" not in api,
            "New dedicated placement API/DI enabled before identity/membership")
    infra = ROOT / "backend/src/Crecisol.Infrastructure/Tenancy"
    for path in infra.glob("*.cs"):
        text = path.read_text(encoding="utf-8")
        require("ITrustedTenantPlacementReader" not in text and
                "ResolveCurrentTenantStorage" not in text,
                "Dedicated routing provider was enabled prematurely")
    print(f"PASS C04-03 B: {count} adversarial guard mutations rejected; "
          "authorization before catalog; no HTTP/DSN/provider/DI")


def audit_c() -> None:
    require(os.environ.get("GITHUB_ACTIONS") == "true",
            "Audit C requires real GitHub Actions execution")
    sha = os.environ.get("GITHUB_SHA", "")
    run_id = os.environ.get("GITHUB_RUN_ID", "")
    require(bool(re.fullmatch("[0-9a-f]{40}", sha) and
                 re.fullmatch("[0-9]+", run_id)),
            "Missing valid SHA and run ID")
    for part in ("A", "B", "dotnet"):
        file = PROOF / part
        require(file.is_file(), "Missing same-run evidence: " + part)
        require(file.read_text(encoding="utf-8").strip() ==
                f"PASS {part} sha={sha} run={run_id}",
                "Stale or foreign SHA evidence: " + part)
    projects = (
        ROOT / "backend/src/Crecisol.Domain/Crecisol.Domain.csproj",
        ROOT / "backend/src/Crecisol.Application/Crecisol.Application.csproj",
        ROOT / "backend/tests/Crecisol.TenantStorage.ContractsTests/Crecisol.TenantStorage.ContractsTests.csproj",
    )
    for project in projects:
        require("<TargetFramework>net10.0</TargetFramework>" in
                project.read_text(encoding="utf-8"),
                "Project graph not .NET10: " + str(project))
    app_project = projects[1].read_text(encoding="utf-8")
    require("Crecisol.Domain.csproj" in app_project,
            "Application must depend on Domain")
    require("Crecisol.Infrastructure.csproj" not in app_project and
            "Crecisol.Api.csproj" not in app_project,
            "No upward dependency from Application to infrastructure")
    guard(load())
    print("PASS C04-03 C: same SHA/run A/B/.NET10 build+tests and clean "
          "Domain->Application dependency; no real actor auth, storage "
          "routing, dedicated database or production enabled")


def main() -> None:
    require(len(sys.argv) == 2 and sys.argv[1] in ("A", "B", "C"),
            "Usage: qa/c04_03_contracts_audits.py A|B|C")
    {"A": audit_a, "B": audit_b, "C": audit_c}[sys.argv[1]]()


if __name__ == "__main__":
    try:
        main()
    except (AssertionError, OSError, ValueError) as error:
        print("FAIL C04-03:", error, file=sys.stderr)
        sys.exit(1)
