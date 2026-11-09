#!/usr/bin/env python3
"""Independent gates A/B/C for C01-04; runtime checks run separately in Actions."""
from pathlib import Path
import re
import sys
import c01_verify_source

ROOT = Path(__file__).resolve().parents[1]
APP = ROOT / "backend/src/Crecisol.Application"
INF = ROOT / "backend/src/Crecisol.Infrastructure"
API = ROOT / "backend/src/Crecisol.Api"
DOMAIN = ROOT / "backend/src/Crecisol.Domain"
TESTS = ROOT / "backend/tests/Crecisol.Branding.ApplicationTests/Program.cs"
HTTP = ROOT / "qa/c01_04_http_smoke.py"
MYSQL = ROOT / "database/tests/c01_04_api_mysql_smoke.sh"
SPEC = ROOT / "docs/c01/C01_04_API_PUBLICA.md"
PLAN = ROOT / "docs/C01_PREPARACION.md"
MATRIX = ROOT / "docs/c01/C01_01_MATRIZ_REQUISITOS.md"
WORKFLOW = ROOT / ".github/workflows/c01-04-check.yml"

def read(path):
    return path.read_text(encoding="utf-8")

def require(condition, message):
    if not condition:
        raise AssertionError(message)

def audit_a():
    c01_verify_source.main()
    spec, matrix = read(SPEC), read(MATRIX)
    for item in ("P04", "P06", "P07", "P14", "P24", "P25",
                 "T01-R03", "T01-R04", "T02-R07", "T03-R02", "T03-R04"):
        require(item in spec and item in matrix, "Missing canonical trace ID " + item)
    for source in (APP/"Branding/ReadPublishedPlatformBrand.cs",
                   INF/"Branding/MySqlPublishedPlatformBrandReader.cs",
                   API/"Program.cs", TESTS, HTTP, MYSQL, WORKFLOW):
        require(source.is_file(), "Required source/test missing: " + str(source))
    app, api, sql = read(APP/"Branding/ReadPublishedPlatformBrand.cs"), \
                    read(API/"Program.cs"), \
                    read(INF/"Branding/MySqlPublishedPlatformBrandReader.cs")
    for requirement in ("IPublishedPlatformBrandReader", "RequirePublicPlatform",
                        "PublicPlatformBrand", "PublicBrandPresentation"):
        require(requirement in app, "Public use case/DTO missing: " + requirement)
    for required in ("MapGet", "/api/v1/public/branding/platform",
                     "ReadPublishedPlatformBrand"):
        require(required in api, "Missing public API: " + required)
    require("FROM brand_publications AS p" in sql and
            "INNER JOIN brand_revisions AS r" in sql,
            "No link to C01-03 published pointer")
    require(read(TESTS).count("Check(") >= 12,
            "Insufficient pure Application assertions")
    require(read(HTTP).count("expect(") >= 15,
            "Insufficient actual HTTP positive/negative assertions")
    print("C01-04 AUDIT A PASS: canonical Word, scope requirements, use case, API and tests.")

def audit_b():
    api = read(API/"Program.cs")
    reader = read(INF/"Branding/MySqlPublishedPlatformBrandReader.cs")
    app = read(APP/"Branding/ReadPublishedPlatformBrand.cs")
    http, mysql = read(HTTP), read(MYSQL)
    require("p.scope_kind = 1" in reader and
            "p.scope_id = X'00000000000000000000000000000000'" in reader and
            "p.publication_status = 2" in reader and "r.publication_status = 2" in reader,
            "Query must pin published PLATFORM and deny all tenant revisions")
    for unsafe in ("MapPost", "MapPut", "MapDelete", "MapPatch",
                   "UseCors(", "AllowAnyOrigin", "UseForwardedHeaders",
                   "AddAuthentication", "MapGroup(\"/api/v1/admin"):
        require(unsafe not in api, "Private/writable/CORS route introduced: " + unsafe)
    require("AllowAnonymous()" in api and api.count("MapGet(")==1,
            "Exactly one anonymous GET endpoint must exist")
    for marker in ("CRECISOL_C01_ALLOWED_HOSTS", "X-Forwarded-Host", "Forwarded",
                   "X-Tenant-Id", "Authorization", "QueryString.HasValue",
                   "CacheControl = \"no-store\"", "Results.StatusCode"):
        require(marker in api, "Missing request security gate: " + marker)
    for marker in ("UseExceptionHandler", "Catch", "catch (Exception)", "RequirePublicPlatform",
                   "PublicPlatformBrand"):
        require(marker in api + app, "Missing safe response gate: " + marker)
    for marker in ("unapproved Host", "forwarded Host spoof", "scope injection",
                   "fake browser identity", "tenant branding route", "admin branding route",
                   "write verb denied", "invalid platform content", "no server-side database"):
        require(marker in http, "Missing negative HTTP test: " + marker)
    require("--network none" in mysql and "Connection Protocol=Unix" in mysql,
            "MySQL must be network-disabled and accessed through a local socket")
    require("GRANT SELECT" in mysql and "denied UPDATE" in mysql,
            "Required read-only DB role/negative missing")
    for forbidden in ("hostinger.com", "solqaryn/Solqaryn", "docker push", "DROP DATABASE",
                      "ClientSecret", "ConnectionStrings.json", "http://crecisol.com"):
        require(forbidden not in api + reader + mysql, "Forbidden live external action/secret")
    print("C01-04 AUDIT B PASS: fixed published platform only, HTTP fail closed, no private routes.")

def audit_c():
    domain, app, infra, api = map(read, (
        DOMAIN/"Crecisol.Domain.csproj",
        APP/"Crecisol.Application.csproj",
        INF/"Crecisol.Infrastructure.csproj",
        API/"Crecisol.Api.csproj"))
    require("<TargetFramework>net10.0</TargetFramework>" in domain + app + infra + api,
            "net10 must be configured")
    require("<ProjectReference" not in domain and "<PackageReference" not in domain,
            "Domain must remain framework/dependency-free")
    require("Crecisol.Domain.csproj" in app and "Crecisol.Infrastructure" not in app,
            "Application must depend only on Domain")
    require("Crecisol.Application.csproj" in infra and
            'PackageReference Include="MySqlConnector" Version="2.6.2"' in infra,
            "Infrastructure must own pinned DB provider")
    require("Crecisol.Application.csproj" in api and
            "Crecisol.Infrastructure.csproj" in api, "API must consume ports/adapters")
    require("DateTime.SpecifyKind" in read(INF/"Branding/MySqlPublishedPlatformBrandReader.cs"),
            "MySQL UTC mapping required")
    require("CRECISOL_C01_READONLY_DSN" in read(API/"Program.cs"),
            "Server-only DSN key absent")
    require("mysql:8.4@sha256:" in read(MYSQL),
            "Reproducible MySQL 8.4 digest missing")
    workflow = read(WORKFLOW)
    for marker in ("contents: read", "ubuntu-24.04", "c01-04-audits",
                   "c01-04-build", "c01-04-integration"):
        require(marker in workflow, "Required CI job/scope missing: " + marker)
    require("0/37" in read(SPEC) and "C01-05" in read(SPEC),
            "Non-certification / next-phase boundary missing")
    require("| C01-04 |" in read(PLAN) and "0/37" in read(PLAN),
            "Broken sequential chapter gate")
    require("Reversión" in read(SPEC), "Reversibility must be documented")
    print("C01-04 AUDIT C PASS: Clean Architecture, version-pinned driver, UTC, CI and rollback.")

if __name__ == "__main__":
    try:
        require(len(sys.argv) == 2 and sys.argv[1] in ("A", "B", "C"),
                "Choose audit A/B/C")
        {"A": audit_a, "B": audit_b, "C": audit_c}[sys.argv[1]]()
    except (AssertionError, OSError, ValueError) as error:
        print(f"C01-04 AUDIT FAIL: {error}", file=sys.stderr)
        sys.exit(1)
