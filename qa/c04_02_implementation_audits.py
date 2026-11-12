#!/usr/bin/env python3
"""C04-02 implementation gates A/B/C: SHA-bound Word, fail-closed SQL and .NET10.

A: Verify canonical C04 authority and cross-layer scope.
B: Reject independent mutations of tenant/sucursal/SQL/read-only gates.
C: Require real MySQL8.4 and dotnet runs from this exact workflow SHA/run.
These gates DO NOT claim real actor authentication in C04-05/06.
"""
from __future__ import annotations

import os
from pathlib import Path
import re
import sys

from c04_01_normative_inventory import EXPECTED, chapter4
from c04_01_preparation_audits import ROOT, canonical_active_word

PATHS = {
    "ddl": ROOT / "database/c04_02_shared_schema.sql",
    "app": ROOT / "backend/src/Crecisol.Application/Tenancy/ReadSharedTenantResource.cs",
    "reader": ROOT / "backend/src/Crecisol.Infrastructure/Tenancy/MySqlSharedTenantResourceReader.cs",
    "codec": ROOT / "backend/src/Crecisol.Infrastructure/Tenancy/MySqlRfc4122Guid.cs",
    "tests": ROOT / "backend/tests/Crecisol.SharedTenancy.ContractsTests/Program.cs",
    "runner": ROOT / "database/tests/c04_02_backend_reader_smoke.sh",
    "smoke": ROOT / "database/tests/c04_02_application_schema_smoke.sh",
    "workflow": ROOT / ".github/workflows/c04-02-application-schema.yml",
    "spec": ROOT / "docs/c04/C04_02_IMPLEMENTACION_ESQUEMA_SHARED.md",
}
PROOF = ROOT / ".c04-02-impl-proof"

MARKERS = {
    "ddl": (
        "PRIMARY KEY (tenant_id)",
        "PRIMARY KEY (tenant_id, branch_id)",
        "PRIMARY KEY (tenant_id, branch_id, resource_id)",
        "PRIMARY KEY (tenant_id, branch_id, resource_id, event_id)",
        "UNIQUE KEY uq_c04_resource_scope_key (tenant_id, branch_id, external_key)",
        "CONSTRAINT fk_c04_branch_tenant FOREIGN KEY (tenant_id)",
        "CONSTRAINT fk_c04_resource_branch FOREIGN KEY (tenant_id, branch_id)",
        "CONSTRAINT fk_c04_event_resource FOREIGN KEY (tenant_id, branch_id, resource_id)",
        "ON DELETE RESTRICT ON UPDATE RESTRICT",
        "ENGINE=InnoDB",
        "CREATE TRIGGER trg_c04_tenant_identity_immutable",
        "CREATE TRIGGER trg_c04_branch_ownership_immutable",
        "CREATE TRIGGER trg_c04_resource_ownership_immutable",
        "CREATE TRIGGER trg_c04_event_append_only_update",
        "CREATE TRIGGER trg_c04_event_append_only_delete",
        "SIGNAL SQLSTATE '45000'",
    ),
    "app": (
        "ResolveForCurrentActorAsync(cancellationToken);",
        "if (scope is null)",
        "throw new UnauthorizedAccessException",
        "result.TenantId != scope.TenantId",
        "result.BranchId != scope.BranchId",
        "result.ResourceId != resourceId",
        "cancellationToken.ThrowIfCancellationRequested();",
    ),
    "reader": (
        "WHERE tenant_id = @tenant_id",
        "AND branch_id = @branch_id",
        "AND resource_id = @resource_id",
        "MySqlRfc4122Guid.Encode(scope.TenantId.Value)",
        "MySqlRfc4122Guid.Encode(scope.BranchId)",
        "MySqlRfc4122Guid.Encode(resourceId)",
        "actualTenant != scope.TenantId",
        "actualBranch != scope.BranchId",
        "actualResource != resourceId",
        "string.IsNullOrWhiteSpace(trustedConnectionString)",
        "CommandBehavior.SingleRow",
    ),
    "codec": (
        "ToByteArray(bigEndian: true)",
        "new Guid(bytes, bigEndian: true)",
        "bytes.Length != 16",
        "guid == Guid.Empty",
    ),
    "tests": (
        "00112233445566778899AABBCCDDEEFF",
        "Unidentified/unapproved actor is denied",
        "Forged foreign tenant row rejected after storage",
        "Wrong branch row rejected after storage",
        "B cannot see A-only resource even with guessed UUID",
        "A cannot access another branch with guessed UUID",
        "No trusted DB configuration fails closed",
        "Failed authorization cannot fail open",
        "No API, real identity resolver or provisioning certified",
    ),
    "runner": (
        "--network bridge -p 127.0.0.1::3306",
        "MYSQL_ROOT_PASSWORD",
        "GRANT SELECT ON",
        "expect_privilege_denial \"UPDATE\"",
        "expect_privilege_denial \"DELETE\"",
        "expect_privilege_denial \"tenant catalogue enumeration\"",
        "expect_privilege_denial \"DROP\"",
        "CRECISOL_C04_02_TEST_DSN",
        "trap cleanup EXIT",
        "no API, router or deployment",
    ),
    "smoke": (
        'expect_immutable "resource A to EXISTING B tenant and branch with no colliding ID"',
        'expect_immutable "resource within tenant A to another VALID branch"',
        'expect_immutable "empty A branch to EXISTING B tenant"',
        'expect_immutable "tenant ID rekey without any dependent rows"',
        'expect_immutable "event to existing foreign tenant/resource"',
        'expect_immutable "append-only event content update"',
        'expect_immutable "append-only event deletion"',
        "ERROR 1644",
        "A remains owner after denied valid-target reparent",
    ),
    "workflow": (
        "dotnet-version: '10.0.x'",
        "backend/tests/Crecisol.SharedTenancy.ContractsTests/Crecisol.SharedTenancy.ContractsTests.csproj",
        "bash database/tests/c04_02_backend_reader_smoke.sh",
        "bash database/tests/c04_02_application_schema_smoke.sh",
        "python3 qa/c04_02_implementation_audits.py A",
        "python3 qa/c04_02_implementation_audits.py B",
        "python3 qa/c04_02_implementation_audits.py C",
    ),
    "spec": (
        "NO CERTIFICADO",
        "C04-03 BLOQUEADA",
        EXPECTED,
    ),
}

def need(condition: bool, why: str) -> None:
    if not condition:
        raise AssertionError(why)

def load() -> dict[str, str]:
    return {k: p.read_text(encoding="utf-8") for k, p in PATHS.items()}

def guard(files: dict[str, str]) -> None:
    for part, markers in MARKERS.items():
        for marker in markers:
            need(marker in files[part], f"{part}: missing trust boundary {marker}")
    ddl = files["ddl"]
    need(ddl.count("CREATE TABLE c04_") == 4, "Only four C04 SHARED tables allowed")
    need(ddl.count("FOREIGN KEY") == 3, "Three scoped FKs required")
    need(ddl.count("CREATE TRIGGER") == 5,
         "Five immutable identity/history triggers required")
    need(ddl.count("SIGNAL SQLSTATE '45000'") == 5,
         "Every ownership/history mutation must fail with SQLSTATE 45000")
    need(ddl.count("ON DELETE RESTRICT ON UPDATE RESTRICT") == 3,
         "No cascade or cross-tenant reparenting")
    need("FOREIGN_KEY_CHECKS=0" not in ddl, "FK enforcement must remain active")
    need("GRANT ALL" not in ddl and "CREATE USER" not in ddl,
         "Application DDL may not provision SQL users")
    reader = files["reader"]
    need(reader.count("command.Parameters.Add(") == 3,
         "Tenant, branch and item must be bound as parameters")
    need("SELECT *" not in reader.upper() and "string.Format" not in reader,
         "No overbroad projection or formatted SQL")
    need("FROM c04_resources" in reader, "Query target must be C04 resources")
    need("Parameter" in reader and "MySqlDbType.Binary" in reader,
         "Missing safely bound UUID binary parameters")
    need("ConnectionProtocol=UnixSocket" not in reader,
         "Adapter must not override connection type from browser")
    need("--privileged" not in files["runner"] and
         "-p 0.0.0.0:" not in files["runner"],
         "No privileged container or externally listening SQL port")
    need("persist-credentials: false" in files["workflow"] and
         "contents: read" in files["workflow"],
         "CI must retain read-only repo token")

def audit_a() -> None:
    need(canonical_active_word(), "Canonical Word/PDF digest mismatch")
    rows = chapter4()
    need(len(rows) == 72, "Original C04 paragraphs altered")
    source = "\n".join(v for _, v in rows)
    for term in ("FK compuestas con tenant",
                 "nunca confiar en tenant_id aportado por navegador",
                 "SHARED A/B y DEDICATED C"):
        need(term in source, "Canonical C04 requirement missing: " + term)
    plan = (ROOT / "docs/planificacion-futura/C04_MICROFASES_PROPUESTAS.md").read_text(encoding="utf-8")
    need("| C04-02 | Modelo relacional shared tenant-aware |" in plan,
         "Original scope of C04-02 changed")
    guard(load())
    print("PASS C04-02 implementation A: pinned Word/PDF, C04 requirements, "
          "DB/Application/Infrastructure scope and no premature tenant endpoint")

def audit_b() -> None:
    files = load()
    guard(files)
    for key, markers in MARKERS.items():
        for marker in markers:
            mutant = dict(files)
            mutant[key] = mutant[key].replace(marker, "[REMOVED TRUST BOUNDARY]")
            try:
                guard(mutant)
            except AssertionError:
                pass
            else:
                raise AssertionError("Mutation survived: " + key + " " + marker)
    api = (ROOT / "backend/src/Crecisol.Api/Program.cs").read_text(encoding="utf-8")
    need("ReadSharedTenantResource" not in api and
         "MySqlSharedTenantResourceReader" not in api and
         "/api/v1/tenants" not in api,
         "Private tenant endpoint/DI enabled without production auth")
    infra_project = (ROOT / "backend/src/Crecisol.Infrastructure/Crecisol.Infrastructure.csproj").read_text(encoding="utf-8")
    need('<PackageReference Include="MySqlConnector" Version="2.6.2" />' in infra_project,
         "Existing tested provider unexpectedly changed")
    need("Microsoft.AspNetCore" not in files["app"] and "MySql" not in files["app"],
         "Application must not depend on transport or SQL framework")
    print(f"PASS C04-02 implementation B: {sum(map(len, MARKERS.values()))} "
          "independent guard mutations rejected; no API/DI; exact scoped SQL")

def audit_c() -> None:
    need(os.environ.get("GITHUB_ACTIONS") == "true",
         "C requires a real CI runner")
    sha = os.environ.get("GITHUB_SHA", "")
    run = os.environ.get("GITHUB_RUN_ID", "")
    need(bool(re.fullmatch(r"[0-9a-f]{40}", sha) and re.fullmatch(r"[0-9]+", run)),
         "Invalid run fingerprint")
    for part in ("A", "B", "dotnet", "mysql"):
        p = PROOF / part
        need(p.is_file(), "Missing real proof: " + part)
        need(p.read_text(encoding="utf-8").strip() ==
             f"PASS {part} sha={sha} run={run}",
             "Evidence from another SHA/run: " + part)
    app_project = (ROOT / "backend/src/Crecisol.Application/Crecisol.Application.csproj").read_text(encoding="utf-8")
    infra_project = (ROOT / "backend/src/Crecisol.Infrastructure/Crecisol.Infrastructure.csproj").read_text(encoding="utf-8")
    test_project = (ROOT / "backend/tests/Crecisol.SharedTenancy.ContractsTests/Crecisol.SharedTenancy.ContractsTests.csproj").read_text(encoding="utf-8")
    need("<TargetFramework>net10.0</TargetFramework>" in app_project and
         "<TargetFramework>net10.0</TargetFramework>" in infra_project and
         "<TargetFramework>net10.0</TargetFramework>" in test_project,
         ".NET 10 project graph changed")
    need("Crecisol.Application.csproj" in infra_project and
         "Crecisol.Infrastructure.csproj" not in app_project,
         "Dependency arrow must remain Application -> Infrastructure")
    guard(load())
    print("PASS C04-02 implementation C: A/B, .NET10 and MySQL8.4 on SAME SHA/run; "
          "still NO real identity resolver, private HTTP, dedicated routing or production")

def main() -> None:
    need(len(sys.argv) == 2 and sys.argv[1] in ("A","B","C"),
         "Usage: qa/c04_02_implementation_audits.py A|B|C")
    {"A":audit_a, "B":audit_b, "C":audit_c}[sys.argv[1]]()

if __name__ == "__main__":
    try:
        main()
    except (OSError, AssertionError, ValueError) as error:
        print("FAIL C04-02 implementation audit:",error,file=sys.stderr)
        sys.exit(1)
