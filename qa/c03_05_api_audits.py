#!/usr/bin/env python3
"""CRECISOL C03-05 independent A/B/C audits and adversarial DTO/API mutations."""
from copy import deepcopy
from hashlib import sha256
from pathlib import Path
import sys
import xml.etree.ElementTree as ET

import c03_01_source as original

from master_source_guard import canonical_active_word

ROOT = Path(__file__).resolve().parents[1]
WORD = ROOT / "docs/CRECISOL_DOCUMENTO_MAESTRO_UNICO.docx"
WORD_SHA = "e45c2017e62bbbaa6853298efd5de5d785230005203d8791f791760f6f2c622e"
API = ROOT / "backend/src/Crecisol.Api/Program.cs"
DTO = ROOT / "backend/src/Crecisol.Api/Contracts/PublishedPlatformBrandDto.cs"
GUARD = ROOT / "backend/src/Crecisol.Api/Contracts/PublicBrandRequestValidator.cs"
TEST = ROOT / "backend/tests/Crecisol.Api.ContractsTests/Program.cs"
TEST_PROJECT = ROOT / "backend/tests/Crecisol.Api.ContractsTests/Crecisol.Api.ContractsTests.csproj"
SMOKE = ROOT / "qa/c03_05_http_smoke.py"
MYSQL = ROOT / "database/tests/c01_04_api_mysql_smoke.sh"
SPEC = ROOT / "docs/c03/C03_05_ESPECIFICACION.md"
WF = ROOT / ".github/workflows/c03-05-api-dto.yml"
PROOF = ROOT / ".c03-05-ci-proof"


def require(condition, reason):
    if not condition:
        raise AssertionError(reason)


def read(path):
    return path.read_text(encoding="utf-8")


def audit_a():
    require(canonical_active_word(WORD_SHA),
            "Canonical Word SHA-256 differs from approved reference")
    parts, table, _, annex = original.original_parts()
    require(len(parts) == 7 and len(table) == 9 and len(annex) == 3,
            "C03 canonical Word source structure changed")
    require(table[3][0] == "Infrastructure", "C03 original table sequence changed")
    spec = read(SPEC)
    for word in ("C03-05", "NO CERTIFICADA", "C03-04",
                 "C03 NO CERTIFICADO", "C04 BLOQUEADO",
                 "DTO", "Validación", "A/B/C", WORD_SHA):
        require(word in spec, "C03-05 specification lacks source, risk or gate: " + word)
    for path in (API, DTO, GUARD, TEST, TEST_PROJECT, SMOKE, MYSQL, WF):
        require(path.is_file(), "C03-05 deliverable absent: " + str(path))
    angular = read(ROOT / "frontend/src/app/branding/public-brand.ts")
    require("parsePublishedPlatformBrand" in angular and
            "platformKeys = new Set" in angular and
            "presentationKeys = new Set" in angular,
            "Existing Angular exact DTO consumer missing")
    require("C03-05" in read(TEST) and "JsonSerializerDefaults.Web" in read(TEST),
            "Typed serialization contract executable not found")
    print("PASS C03-05 A: approved Word SHA, C03 source, strict Angular contract and DTO evidence.")


def invariant(api, dto, guard, smoke):
    require(api.count("app.MapGet(") == 1 and
            '"/api/v1/public/branding/platform"' in api,
            "Exactly one public GET route is permitted")
    for marker in ("MapPost(", "MapPut(", "MapPatch(", "MapDelete(",
                   "UseForwardedHeaders", "AllowAnyOrigin", "AddAuthentication",
                   "MySqlPublishedPlatformBrandRevisionReader",
                   "IPublishedPlatformBrandRevisionReader"):
        require(marker not in api, "Unapproved API surface: " + marker)
    for marker in ('"CRECISOL_C01_ALLOWED_HOSTS"', '"CRECISOL_C01_READONLY_DSN"',
                   '"X-Forwarded-Host"', '"Forwarded"', '"X-Tenant-Id"',
                   '"Authorization"', "QueryString.HasValue",
                   "UseExceptionHandler", "CacheControl = \"no-store\"",
                   "Results.StatusCode(StatusCodes.Status503ServiceUnavailable)",
                   "PublicBrandRequestValidator.IsValid(context.Request)",
                   "Results.Json(PublishedPlatformBrandDto.FromApplication(brand))"):
        require(marker in api, "API removed a required fail-closed boundary: " + marker)
    require("using Crecisol.Application.Branding;" in dto and
            "record PublishedPlatformBrandDto" in dto and
            "record PublishedBrandPresentationDto" in dto and
            "FromApplication(PublicPlatformBrand published)" in dto and
            "ArgumentNullException.ThrowIfNull(published)" in dto and
            "presentation is null ? null" in dto,
            "DTO must explicitly map allowlisted public Application values")
    for marker in ("using MySqlConnector", "using Crecisol.Domain",
                   "TenantId", "ScopeId", "ConnectionString", "Password",
                   "public object", "dynamic ", "JsonExtensionData"):
        require(marker not in dto, "DTO introduced internal data or dynamic serialization: " + marker)
    for marker in ("HttpMethods.IsGet", "!request.QueryString.HasValue", '"Authorization"',
                   '"X-Tenant-Id"', '"Content-Length"', '"Transfer-Encoding"',
                   '"X-HTTP-Method-Override"', '"X-Original-URL"', '"X-Rewrite-URL"'):
        require(marker in guard, "Request validation missing: " + marker)
    require("HttpRequest request" in guard and
            "ArgumentNullException.ThrowIfNull(request)" in guard,
            "Validator must reject null and use typed ASP.NET request")
    require("TOP = {" in smoke and "PRESENTATION = {" in smoke and
            "set(item) != TOP" in smoke and "set(item[\"presentation\"]) != PRESENTATION" in smoke,
            "Real HTTP smoke must compare exact DTO keys")
    require("X-Tenant-Id" in smoke and "X-Forwarded-Host" in smoke and
            "Content-Length" in smoke and "X-Original-URL" in smoke and
            "unconfigured" in smoke and "invalid" in smoke,
            "Real negative HTTP cases missing")


def audit_b():
    files = {"api": read(API), "dto": read(DTO),
             "guard": read(GUARD), "smoke": read(SMOKE)}
    invariant(**files)
    mutations = [
        ("raw Application serialization", "api",
         "Results.Json(PublishedPlatformBrandDto.FromApplication(brand))", "Results.Json(brand)"),
        ("new private write route", "api", "app.Run();",
         'app.MapPost("/api/v1/admin/brand", () => Results.Ok());\napp.Run();'),
        ("disallowed revision adapter registration", "api", "app.Run();",
         "MySqlPublishedPlatformBrandRevisionReader;\napp.Run();"),
        ("CORS wildcard", "api", "app.Run();", "AllowAnyOrigin;\napp.Run();"),
        ("DTO tenant property", "dto", "long Revision,", "string TenantId,\n    long Revision,"),
        ("DTO dynamic extension", "dto", "namespace Crecisol", "dynamic value;\nnamespace Crecisol"),
        ("request drops GET restriction", "guard", "HttpMethods.IsGet", "HttpMethods.IsPost"),
        ("request drops query rejection", "guard", "!request.QueryString.HasValue",
         "request.QueryString.HasValue"),
        ("request drops auth rejection", "guard", '"Authorization"', '"A-Authorization"'),
        ("request drops tenant rejection", "guard", '"X-Tenant-Id"', '"X-Scope-Id"'),
        ("request accepts body", "guard", '"Content-Length"', '"Body-Length"'),
        ("request accepts chunking", "guard", '"Transfer-Encoding"', '"X-Transfer-Encoding"'),
    ]
    for name, kind, original_text, replacement in mutations:
        require(original_text in files[kind], "Mutation anchor missing: " + name)
        modified = deepcopy(files)
        modified[kind] = modified[kind].replace(original_text, replacement, 1)
        try:
            invariant(**modified)
        except AssertionError:
            continue
        raise AssertionError("Adversarial mutation was accepted: " + name)
    original.actual_graph()
    print(f"PASS C03-05 B: 12/12 adversarial mutations rejected, HTTP/DTO/tenant boundaries preserved.")


def audit_c():
    for label in ("build", "dto-tests", "regressions", "mysql-http"):
        path = PROOF / label
        require(path.is_file() and path.read_text(encoding="utf-8").strip() == "PASS: " + label,
                "Missing real Actions step evidence: " + label)
    project = ET.fromstring(read(TEST_PROJECT))
    require(project.findtext(".//TargetFramework") == "net10.0",
            "API contract executable must target .NET10")
    refs = [r.attrib.get("Include", "") for r in project.findall(".//ProjectReference")]
    require(refs == ["../../src/Crecisol.Api/Crecisol.Api.csproj"],
            "API contracts tests must reference API only")
    for file in ("backend/src/Crecisol.Domain/Crecisol.Domain.csproj",
                 "backend/src/Crecisol.Application/Crecisol.Application.csproj",
                 "backend/src/Crecisol.Infrastructure/Crecisol.Infrastructure.csproj"):
        require("net10.0" in read(ROOT / file), "Target framework changed")
    original.actual_graph()
    workflow = read(WF)
    for marker in ("contents: read", "ubuntu-24.04", "dotnet build",
                   "dotnet run", "CRECISOL_C03_05_CONTRACT_SMOKE=1",
                   "mysql-http", "python3 qa/c03_05_api_audits.py A",
                   "python3 qa/c03_05_api_audits.py B",
                   "python3 qa/c03_05_api_audits.py C"):
        require(marker in workflow, "CI missing real gate: " + marker)
    require("CRECISOL_C03_05_CONTRACT_SMOKE" in read(MYSQL) and
            "--network none" in read(MYSQL) and
            "GRANT SELECT" in read(MYSQL),
            "Real MySQL socket/SELECT-only integration is not wired")
    print("PASS C03-05 C: real .NET10 build, executable contracts, C01-C03 regressions, "
          "MySQL8.4 + HTTP, graph and rollback gates.")


if __name__ == "__main__":
    try:
        require(len(sys.argv) == 2 and sys.argv[1] in ("A", "B", "C"),
                "Specify A, B or C independent audit")
        {"A": audit_a, "B": audit_b, "C": audit_c}[sys.argv[1]]()
    except (AssertionError, OSError, ValueError) as error:
        print("FAIL C03-05 audit: " + str(error), file=sys.stderr)
        sys.exit(1)
