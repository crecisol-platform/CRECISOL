#!/usr/bin/env python3
"""CRECISOL C03-03 A/B/C: Word source, pure Application and real regressions."""
from copy import deepcopy
from hashlib import sha256
from pathlib import Path
import re
import sys
import xml.etree.ElementTree as ET

import c03_01_source as original

from master_source_guard import canonical_active_word

ROOT = Path(__file__).resolve().parents[1]
APP = ROOT / "backend/src/Crecisol.Application"
APP_PROJECT = APP / "Crecisol.Application.csproj"
API = ROOT / "backend/src/Crecisol.Api/Program.cs"
CASE_NAME = "AssessPlatformBrandPublicationProposal"
CASE_PATH = "Branding/" + CASE_NAME + ".cs"
PORT = "IPublishedPlatformBrandRevisionReader"
TEST = ROOT / "backend/tests/Crecisol.Application.ContractsTests/Program.cs"
TEST_PROJECT = ROOT / "backend/tests/Crecisol.Application.ContractsTests/Crecisol.Application.ContractsTests.csproj"
SPEC = ROOT / "docs/c03/C03_03_ESPECIFICACION.md"
WORD = ROOT / "docs/CRECISOL_DOCUMENTO_MAESTRO_UNICO.docx"
WORD_SHA = "e45c2017e62bbbaa6853298efd5de5d785230005203d8791f791760f6f2c622e"
PROOF = ROOT / ".c03-03-ci-proof"
FORBIDDEN = re.compile(
    r"\busing\s+(?:MySqlConnector|Microsoft\.EntityFrameworkCore|"
    r"Microsoft\.AspNetCore|Dapper|System\.Data|System\.Net\.Http)\b|"
    r"\b(?:MySqlConnection|SqlConnection|DbContext|IServiceProvider|"
    r"HttpContext|HttpClient|IConfiguration|ConnectionString)\b|"
    r"Environment\.GetEnvironmentVariable\s*\("
)


def require(value, message):
    if not value:
        raise AssertionError(message)


def authored_sources():
    return {p.relative_to(APP).as_posix(): p.read_text(encoding="utf-8")
            for p in APP.rglob("*.cs")
            if not ({"obj", "bin"} & set(p.relative_to(APP).parts))}


def pure(project, sources, api):
    root = ET.fromstring(project)
    tfm = root.find(".//TargetFramework")
    require(tfm is not None and tfm.text == "net10.0", "Application TFM must be net10.0")
    require(not root.findall(".//PackageReference"),
            "Application cannot take direct NuGet SDK packages")
    refs = [e.attrib.get("Include", "") for e in root.findall(".//ProjectReference")]
    require(refs == ["../Crecisol.Domain/Crecisol.Domain.csproj"],
            "Application must depend only on Domain, never Infrastructure")
    for name, source in sources.items():
        require(not FORBIDDEN.search(source),
                "Application source imports a forbidden HTTP/SQL/ORM/DI dependency: " + name)
    use_case = sources[CASE_PATH]
    for expected in ("sealed class " + CASE_NAME,
                     "interface " + PORT,
                     "ReadCurrentPublishedRevisionAsync(CancellationToken cancellationToken)",
                     "proposal.Validate()", "BrandScope.Platform",
                     "DomainRevision.RequireNewerThan",
                     "PlatformBrandPublicationPreflight"):
        require(expected in use_case, "Missing Application rule/port: " + expected)
    require(use_case.count("cancellationToken.ThrowIfCancellationRequested()") >= 2,
            "Application must check cancellation before and after port read")
    require(CASE_NAME not in api and PORT not in api,
            "Unapproved private publication preflight registered in public API")


def audit_a():
    require(canonical_active_word(WORD_SHA),
            "Canonical master Word changed")
    parts, table, _, _ = original.original_parts()
    require(len(parts) == 7 and len(table) == 9 and table[2][0] == "Application",
            "Canonical T01-R03 Application row is absent")
    doc = SPEC.read_text(encoding="utf-8")
    for key in ("C03-03", "T01-R03", "P03", "P04", "DIF-C03-01",
                "C03-02", "CERTIFICADAS (2/10)", "8/8", WORD_SHA,
                "MAE-C03-01..08", "NO CERTIFICADA", "C03 NO CERTIFICADO",
                "C04 BLOQUEADO", "C03-04", "BOLA/IDOR/MFA", PORT,
                CASE_NAME, "Rollback"):
        require(key in doc, "Missing canonical traceability/negative scope: " + key)
    require(TEST.is_file() and TEST_PROJECT.is_file(),
            "Typed Application executable contract tests missing")
    print("AUDIT A PASS C03-03: canonical Word SHA, 7 original units, 9 table rows, "
          "T01-R03 Application, prior 2/10 certification and scope verified.")


def audit_b():
    project, src, api = APP_PROJECT.read_text(encoding="utf-8"), authored_sources(), API.read_text(encoding="utf-8")
    pure(project, src, api)
    original.actual_graph()
    cases = (
        ("MySQL SDK package", "csproj",
         "<ItemGroup><PackageReference Include='MySqlConnector' Version='2.4.0'/></ItemGroup>"),
        ("Infrastructure ProjectReference", "csproj",
         "<ItemGroup><ProjectReference Include='../Crecisol.Infrastructure/Crecisol.Infrastructure.csproj'/></ItemGroup>"),
        ("MySQL import", "source", "using MySqlConnector;\n"),
        ("HTTP import", "source", "using Microsoft.AspNetCore.Http;\n"),
        ("ORM import", "source", "using Microsoft.EntityFrameworkCore;\n"),
        ("Service locator", "source", "internal class Bad { IServiceProvider? services; }\n"),
        ("public API DI registration", "api", "builder.Services.AddScoped<" + CASE_NAME + ">();\n"),
    )
    for label, place, injection in cases:
        sample_project, sample_src, sample_api = project, deepcopy(src), api
        if place == "csproj":
            sample_project = project.replace("</Project>", injection + "</Project>")
        elif place == "source":
            sample_src[CASE_PATH] = injection + sample_src[CASE_PATH]
        else:
            sample_api = api + "\n" + injection
        try:
            pure(sample_project, sample_src, sample_api)
        except (AssertionError, ET.ParseError, KeyError):
            continue
        raise AssertionError("Unsafe Application dependency exposed: " + label)
    print("AUDIT B PASS C03-03: net10.0 Application references only Domain, "
          "no HTTP/SQL/ORM/DI, real acyclic graph, API has no private route; "
          "7/7 dependency/exposure mutations rejected.")


def audit_c():
    pure(APP_PROJECT.read_text(encoding="utf-8"), authored_sources(), API.read_text(encoding="utf-8"))
    original.actual_graph()
    for name in ("build", "c03-app", "c01-app", "c03-domain", "c01-domain"):
        marker = PROOF / name
        require(marker.is_file() and marker.read_text(encoding="utf-8").strip() ==
                "PASS: " + name, "Missing real CI compile/test marker: " + name)
    t = TEST.read_text(encoding="utf-8")
    for key in ("BrandScope.Tenant", "ExpectedCurrentPublishedRevision = -1",
                "Guid.Empty", "ThrowingReader", "CancellationTokenSource",
                "ReadCurrentPublishedRevisionAsync", "InvalidOperationException",
                "ScopeId = foreignTenantId", "OperationId = Guid.Empty"):
        require(key in t, "Missing positive/negative Application scenario: " + key)
    test_xml = ET.parse(TEST_PROJECT).getroot()
    require([x.attrib.get("Include", "") for x in test_xml.findall(".//ProjectReference")] ==
            ["../../src/Crecisol.Application/Crecisol.Application.csproj"],
            "New Application tests may reference only Application project")
    print("AUDIT C PASS C03-03: real .NET10 Release API + new App + C01 App + "
          "C03/C01 Domain executables, negative input/cancellation contract coverage.")


if __name__ == "__main__":
    try:
        require(len(sys.argv) == 2 and sys.argv[1] in ("A", "B", "C"),
                "usage: c03_03_application_audits.py [A|B|C]")
        {"A": audit_a, "B": audit_b, "C": audit_c}[sys.argv[1]]()
    except (AssertionError, OSError, KeyError, ValueError, TypeError, ET.ParseError) as exc:
        print("C03-03 AUDIT FAILED: " + str(exc), file=sys.stderr)
        sys.exit(1)
