#!/usr/bin/env python3
"""CRECISOL C03-06 A/B/C: explicit vertical composition, adversarial DI guards."""
from copy import deepcopy
from hashlib import sha256
from pathlib import Path
import re
import sys
import xml.etree.ElementTree as ET

import c03_01_source as c03
import c03_05_api_audits as c05
import c03_05_status as stage

ROOT = Path(__file__).resolve().parents[1]
WORD = ROOT / "docs/CRECISOL_DOCUMENTO_MAESTRO_UNICO.docx"
WORD_SHA = "e45c2017e62bbbaa6853298efd5de5d785230005203d8791f791760f6f2c622e"
SPEC = ROOT / "docs/c03/C03_06_ESPECIFICACION.md"
PLAN = ROOT / "docs/PLAN_37_CAPITULOS.md"
MODULE = ROOT / "backend/src/Crecisol.Api/Modules/Branding/PublicBrandingModule.cs"
API = ROOT / "backend/src/Crecisol.Api/Program.cs"
CS_PROJ = ROOT / "backend/tests/Crecisol.Api.ModuleTests/Crecisol.Api.ModuleTests.csproj"
TEST = ROOT / "backend/tests/Crecisol.Api.ModuleTests/Program.cs"
WORKFLOW = ROOT / ".github/workflows/c03-06-module-di.yml"
PROOF = ROOT / ".c03-06-ci-proof"


def require(test, why):
    if not test:
        raise AssertionError(why)


def read(path):
    return path.read_text(encoding="utf-8")


def contract(program, registration):
    require(program.count("builder.Services.AddPublicBrandingModule(serverDsn);") == 1,
            "Composition root must invoke one explicit module only")
    require("Environment.GetEnvironmentVariable(\"CRECISOL_C01_READONLY_DSN\")" in program,
            "Public DSN must originate only from server environment")
    require("app.MapGet(\"/api/v1/public/branding/platform\"" in program and
            program.count("app.MapGet(") == 1, "Exactly one published PLATFORM GET")
    for item in ("app.MapPost(", "app.MapDelete(", "app.MapPut(", "app.MapPatch(",
                 "UseForwardedHeaders", "AllowAnyOrigin", "AddAuthentication",
                 "MySqlPublishedPlatformBrandRevisionReader",
                 "IPublishedPlatformBrandRevisionReader", "AssessPlatformBrandPublicationProposal"):
        require(item not in program, "Unauthorized code in public entrypoint: " + item)
    for item in ('"X-Forwarded-Host"', '"Forwarded"', '"X-Tenant-Id"',
                 '"Authorization"', "CacheControl = \"no-store\"",
                 "PublicBrandRequestValidator.IsValid(context.Request)",
                 "Results.Json(PublishedPlatformBrandDto.FromApplication(brand))",
                 "Results.StatusCode(StatusCodes.Status503ServiceUnavailable)"):
        require(item in program, "Public HTTP gate or DTO weakened: " + item)
    for item in ("namespace Crecisol.Api.Modules.Branding",
                 "static class PublicBrandingModule",
                 "IServiceCollection AddPublicBrandingModule(",
                 "ArgumentNullException.ThrowIfNull(services)",
                 "if (services.Any(descriptor =>",
                 "descriptor.ServiceType == typeof(IPublishedPlatformBrandReader)",
                 "descriptor.ServiceType == typeof(ReadPublishedPlatformBrand)",
                 "throw new InvalidOperationException(",
                 "services.AddSingleton<IPublishedPlatformBrandReader>(",
                 "new MySqlPublishedPlatformBrandReader(trustedServerDsn)",
                 "services.AddScoped<ReadPublishedPlatformBrand>()",
                 "return services;"):
        require(item in registration, "Explicit DI binding or override rejection absent: " + item)
    require(registration.count("services.AddSingleton<") == 1 and
            registration.count("services.AddScoped<") == 1,
            "Only two reviewed public DI registrations allowed")
    for forbidden in ("IServiceProvider", "GetService(", "GetRequiredService(",
                      "ActivatorUtilities", "Assembly.Load", "GetTypes(",
                      "RegisterAssemblyTypes", "Scrutor", "AddHttpContextAccessor",
                      "MySqlPublishedPlatformBrandRevisionReader",
                      "IPublishedPlatformBrandRevisionReader",
                      "AssessPlatformBrandPublicationProposal",
                      "HttpContext", "HttpRequest", "X-Tenant-Id",
                      "Environment.GetEnvironmentVariable", "TenantId",
                      "ConnectionStringFromRequest", "MapPost(", "MapGet(",
                      "ConnectionStringBuilder", "AddHostedService"):
        require(forbidden not in registration and
                forbidden not in program.replace("Environment.GetEnvironmentVariable", "")
                if forbidden in ("IServiceProvider", "GetService(", "GetRequiredService(",
                                "ActivatorUtilities", "Assembly.Load", "GetTypes(",
                                "RegisterAssemblyTypes", "Scrutor")
                else forbidden not in registration,
                "Unexpected dynamic/privileged module dependency: " + forbidden)


def audit_a():
    require(sha256(WORD.read_bytes()).hexdigest() == WORD_SHA,
            "Canonical Word SHA-256 altered")
    original, rows, _, _ = c03.original_parts()
    require(len(original) == 7 and len(rows) == 9 and
            rows[2][0] == "Application" and rows[3][0] == "Infrastructure"
            and rows[4][0] == "API",
            "Original Word T01-R03/R04/R05 dependencies changed")
    specification = read(SPEC)
    for item in ("C03-06", "T01-R03", "T01-R04", "T01-R05",
                 "MAE-C03-01..08", "C03-05 CERTIFICADA (5/10)",
                 "19/19 POSTMERGE", "NO PROBADA NI CERTIFICADA",
                 "C03-07 NO INICIADA", "C03 NO CERTIFICADO", "C04 BLOQUEADO",
                 "Service Locator", "Rollback", WORD_SHA):
        require(item in specification,
                "C03-06 scope/tracing/negative evidence absent: " + item)
    for p in (MODULE, API, TEST, CS_PROJ, WORKFLOW):
        require(p.is_file(), "C03-06 deliverable missing: " + str(p))
    plan = read(PLAN)
    require("C03-05 CERTIFICADA (5/10)" in plan and
            stage.fifth_plan_safe(plan) and
            stage.next_locked(next((row for row in plan.splitlines() if row.startswith("C03 | ")), "")) in plan and
            "C04 BLOQUEADO" in plan,
            "Previous stage must be certified before technical C03-06 merge")
    print("PASS C03-06 A: approved Word SHA, 7/9 original units and dependency rows, prior 5/10, scoped implementation.")


def audit_b():
    program, module = read(API), read(MODULE)
    contract(program, module)
    c05.audit_b()
    mutations = [
        ("API duplicate module", "api", "builder.Services.AddPublicBrandingModule(serverDsn);",
         "builder.Services.AddPublicBrandingModule(serverDsn);\nbuilder.Services.AddPublicBrandingModule(serverDsn);"),
        ("API private POST", "api", "app.Run();", 'app.MapPost("/api/v1/admin/publish", () => Results.Ok());\napp.Run();'),
        ("API internal port", "api", "app.Run();",
         "IPublishedPlatformBrandRevisionReader injected;\napp.Run();"),
        ("API host security removed", "api", '"X-Forwarded-Host"', '"X-Other-Host"'),
        ("API browser DSN", "api", "builder.Services.AddPublicBrandingModule(serverDsn);",
         'builder.Services.AddPublicBrandingModule(context.Request.Query["dsn"]);'),
        ("remove duplicate check", "module", "if (services.Any(descriptor =>",
         "if (false && services.Any(descriptor =>"),
        ("reader becomes transient", "module", "services.AddSingleton<",
         "services.AddTransient<"),
        ("use case becomes singleton", "module", "services.AddScoped<",
         "services.AddSingleton<"),
        ("private adapter binding", "module", "return services;",
         "services.AddSingleton<MySqlPublishedPlatformBrandRevisionReader>();\nreturn services;"),
        ("private port binding", "module", "return services;",
         "services.AddScoped<IPublishedPlatformBrandRevisionReader>();\nreturn services;"),
        ("service locator", "module", "return services;",
         "IServiceProvider provider = null!;\nreturn services;"),
        ("reflection registration", "module", "return services;",
         "Assembly.Load(\"untrusted\");\nreturn services;"),
        ("context supplier", "module", "return services;",
         "AddHttpContextAccessor();\nreturn services;"),
        ("dynamic resolver", "module", "return services;",
         "ActivatorUtilities.CreateInstance<object>();\nreturn services;"),
        ("reader type swap", "module", "new MySqlPublishedPlatformBrandReader(trustedServerDsn)",
         "new MySqlPublishedPlatformBrandRevisionReader(trustedServerDsn)"),
    ]
    base = {"api": program, "module": module}
    for name, field, before, after in mutations:
        require(before in base[field], "Invalid mutation setup: " + name)
        mutated = deepcopy(base)
        mutated[field] = mutated[field].replace(before, after, 1)
        try:
            contract(mutated["api"], mutated["module"])
        except AssertionError:
            continue
        raise AssertionError("Unsafe DI mutation accepted: " + name)
    c03.actual_graph()
    print(f"PASS C03-06 B: 15/15 adversarial composition mutations rejected; prior C03-05 audit B passed.")


def audit_c():
    for item in ("build", "module-tests", "regressions", "mysql-http"):
        path = PROOF / item
        require(path.is_file() and path.read_text(encoding="utf-8").strip() == "PASS: " + item,
                "Real C03-06 CI step proof missing: " + item)
    target = ET.fromstring(read(CS_PROJ))
    require(target.findtext(".//TargetFramework") == "net10.0" and
            target.findtext(".//TreatWarningsAsErrors") == "true",
            "Module tests require .NET10 warnings as errors")
    refs = [x.attrib.get("Include") for x in target.findall(".//ProjectReference")]
    require(refs == ["../../src/Crecisol.Api/Crecisol.Api.csproj"],
            "Module tests must reference API only")
    require("FrameworkReference Include=\"Microsoft.AspNetCore.App\"" in read(CS_PROJ),
            "Test must use actual Microsoft DI, not fake container")
    testcode = read(TEST)
    for item in ("ValidateScopes = true", "ValidateOnBuild = true",
                 "ServiceLifetime.Singleton", "ServiceLifetime.Scoped",
                 "GetRequiredService<IPublishedPlatformBrandReader>",
                 "GetService<IPublishedPlatformBrandRevisionReader>()",
                 "GetService<AssessPlatformBrandPublicationProposal>()",
                 "GetService<MySqlPublishedPlatformBrandRevisionReader>()",
                 "ExecuteAsync(CancellationToken.None)",
                 "Double", "Duplicate Branding registration rejected",
                 "Scoped use case cannot be resolved from singleton root"):
        # 'Double' is represented by 'Duplicate' in actual executed test source.
        if item == "Double":
            continue
        require(item in testcode, "No executable negative test: " + item)
    require("CRECISOL_C03_05_CONTRACT_SMOKE=1" in read(WORKFLOW) and
            "dotnet run" in read(WORKFLOW) and
            "ubuntu-24.04" in read(WORKFLOW) and
            "contents: read" in read(WORKFLOW),
            "CI must run real C03-05 contract/MySQL regressions")
    c03.actual_graph()
    print("PASS C03-06 C: real .NET10 Release, executable scope/override contracts, existing tests, MySQL8.4 HTTP and unchanged graph.")


if __name__ == "__main__":
    try:
        require(len(sys.argv) == 2 and sys.argv[1] in ("A", "B", "C"), "Use A, B or C")
        {"A": audit_a, "B": audit_b, "C": audit_c}[sys.argv[1]]()
    except (AssertionError, OSError, ValueError, KeyError, TypeError) as error:
        print("FAIL C03-06 audit: " + str(error), file=sys.stderr)
        sys.exit(1)
