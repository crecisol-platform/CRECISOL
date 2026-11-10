#!/usr/bin/env python3
"""CRECISOL C03-04: canonical, replaceable Infrastructure SELECT-only audits A/B/C."""
from copy import deepcopy
from hashlib import sha256
from pathlib import Path
import re
import sys
import xml.etree.ElementTree as ET

import c03_01_source as original
import c03_03_application_audits as app

from master_source_guard import canonical_active_word

ROOT = Path(__file__).resolve().parents[1]
WORD = ROOT / "docs/CRECISOL_DOCUMENTO_MAESTRO_UNICO.docx"
WORD_SHA = "e45c2017e62bbbaa6853298efd5de5d785230005203d8791f791760f6f2c622e"
SPEC = ROOT / "docs/c03/C03_04_ESPECIFICACION.md"
INFRA = ROOT / "backend/src/Crecisol.Infrastructure"
INFRA_PROJECT = INFRA / "Crecisol.Infrastructure.csproj"
APP_PROJECT = ROOT / "backend/src/Crecisol.Application/Crecisol.Application.csproj"
API = ROOT / "backend/src/Crecisol.Api/Program.cs"
ADAPTER = INFRA / "Branding/MySqlPublishedPlatformBrandRevisionReader.cs"
TEST = ROOT / "backend/tests/Crecisol.Infrastructure.ContractsTests/Program.cs"
TEST_PROJECT = ROOT / "backend/tests/Crecisol.Infrastructure.ContractsTests/Crecisol.Infrastructure.ContractsTests.csproj"
SMOKE = ROOT / "database/tests/c03_04_revision_reader_smoke.sh"
PROOF = ROOT / ".c03-04-ci-proof"

CLASS = "MySqlPublishedPlatformBrandRevisionReader"
PORT = "IPublishedPlatformBrandRevisionReader"
ZERO = "X'00000000000000000000000000000000'"


def require(ok, message):
    if not ok:
        raise AssertionError(message)


def refs(project):
    return [e.attrib.get("Include", "") for e in
            ET.fromstring(project).findall(".//ProjectReference")]


def query(source):
    match = re.search(r'private const string ReadPublishedRevisionSql = """(.*?)""";',
                      source, re.S)
    require(match is not None, "SQL must be a fixed const string")
    return match.group(1)


def invariant(project, app_project, source, api, smoke):
    require(refs(project) == ["../Crecisol.Application/Crecisol.Application.csproj"],
            "Infrastructure must reference only Application")
    root = ET.fromstring(project)
    target = root.find(".//TargetFramework")
    require(target is not None and target.text == "net10.0",
            "Infrastructure must compile with .NET10")
    packages = [p.attrib.get("Include") + "@" + p.attrib.get("Version", "")
                for p in root.findall(".//PackageReference")]
    require(packages == ["MySqlConnector@2.6.2"],
            "Infrastructure may use only the reviewed pinned MySqlConnector")
    require(refs(app_project) == ["../Crecisol.Domain/Crecisol.Domain.csproj"],
            "Application cannot depend on Infrastructure")

    for text in ("sealed class " + CLASS, ": " + PORT,
                 "ReadCurrentPublishedRevisionAsync(", "new MySqlConnection(",
                 "ExecuteScalarAsync(cancellationToken)",
                 "cancellationToken.ThrowIfCancellationRequested()",
                 "string.IsNullOrWhiteSpace(connectionString)",
                 "Convert.ToInt64(", "Pooling = false"):
        require(text in source, "Missing safe adapter operation: " + text)
    sql = query(source)
    for text in ("SELECT r.revision", "FROM brand_publications AS p",
                 "INNER JOIN brand_revisions AS r",
                 "r.scope_kind = p.scope_kind", "r.scope_id = p.scope_id",
                 "r.revision = p.published_revision",
                 "r.publication_status = p.publication_status",
                 "WHERE p.scope_kind = 1", "p.scope_id = " + ZERO,
                 "p.publication_status = 2", "r.scope_kind = 1",
                 "r.scope_id = " + ZERO, "r.publication_status = 2",
                 "LIMIT 1"):
        require(text in sql, "SQL loses published PLATFORM scope: " + text)
    require(sql.count(ZERO) == 2 and
            len(re.findall(r"\bSELECT\b", sql, re.I)) == 1 and
            not re.search(r"\b(?:UPDATE|DELETE|INSERT|DROP|TRUNCATE|ALTER|CREATE)\b", sql, re.I),
            "SQL may not read tenants or include a write command")
    require(not re.search(r"\b(?:HttpContext|HttpRequest|IServiceProvider|"
                          r"Environment\.GetEnvironmentVariable|ExecuteNonQueryAsync|"
                          r"ExecuteNonQuery|ConnectionStringFromRequest|"
                          r"TenantId|ScopeId|X-Forwarded-Host)\b", source),
            "Adapter must not obtain DSN from request, write or switch tenant")
    require(CLASS not in api and PORT not in api,
            "C03-04 adapter cannot be registered or exposed in public API")
    for expected in ("docker run --rm -d --network none", "mysql:8.4@sha256:",
                     "CRECISOL_C03_04_TEST_DSN", "GRANT SELECT", "REVOKE SELECT",
                     "test_mode published", "test_mode updated",
                     "test_mode absent", "test_mode denied",
                     "read-only user obtained UPDATE", "trap cleanup EXIT"):
        require(expected in smoke, "Disposable MySQL coverage missing: " + expected)
    require(not re.search(r"docker run[^\n]*\s(?:-p|--publish|--network host)\b", smoke),
            "No exposed port/host networking allowed")


def audit_a():
    require(canonical_active_word(WORD_SHA),
            "Canonical master Word modified")
    parts, table, _, annex = original.original_parts()
    require(len(parts) == 7 and len(table) == 9 and table[3][0] == "Infrastructure",
            "Canonical T01-R04 Infrastructure row/Word source absent")
    require([len(x) for x in annex] == [9, 3, 3],
            "Canonical table boundaries changed")
    spec = SPEC.read_text(encoding="utf-8")
    for marker in ("C03-04", "NO CERTIFICADA", "3/10", "9/9",
                   "T01-R04", "T01-R03", "P03/P04", "DIF-C03-01",
                   "MAE-C03-01..08", WORD_SHA, CLASS, PORT,
                   "C03-05", "BOLA/IDOR/MFA", "C03", "C04",
                   "Rollback", "MySQL 8.4"):
        require(marker in spec, "Missing source/provenance or boundary: " + marker)
    require(ADAPTER.is_file() and TEST.is_file() and TEST_PROJECT.is_file()
            and SMOKE.is_file(), "Infrastructure contract deliverables missing")
    print("AUDIT A PASS C03-04: approved Word SHA, seven original units, "
          "nine original table rows, T01-R04 Infrastructure and prior 3/10 verified.")


def audit_b():
    project = INFRA_PROJECT.read_text(encoding="utf-8")
    application = APP_PROJECT.read_text(encoding="utf-8")
    source = ADAPTER.read_text(encoding="utf-8")
    api = API.read_text(encoding="utf-8")
    smoke = SMOKE.read_text(encoding="utf-8")
    invariant(project, application, source, api, smoke)
    app.audit_b()
    original.actual_graph()
    mutations = (
        ("tenant scope", "source", "WHERE p.scope_kind = 1", "WHERE p.scope_kind = 2"),
        ("tenant id", "source", ZERO, "X'22222222222222222222222222222222'"),
        ("unpublished status", "source", "r.publication_status = 2", "r.publication_status = 1"),
        ("write statement", "source", "SELECT r.revision", "DELETE r.revision"),
        ("missing ownership join", "source", "r.scope_id = p.scope_id", "r.scope_id = p.publication_status"),
        ("unsafe request environment", "source", "namespace Crecisol", "HttpContext;\nnamespace Crecisol"),
        ("database write API", "source", "namespace Crecisol", "ExecuteNonQueryAsync;\nnamespace Crecisol"),
        ("API exposure", "api", "app.Run();", "builder.Services.AddSingleton<" + CLASS + ">();\napp.Run();"),
        ("Application to Infrastructure reference", "application",
         "</Project>", "<ItemGroup><ProjectReference Include='../Crecisol.Infrastructure/Crecisol.Infrastructure.csproj'/></ItemGroup></Project>"),
        ("open network MySQL", "smoke", "--network none", "--network host"),
    )
    for label, kind, before, after in mutations:
        values = {"project": project, "application": application,
                  "source": source, "api": api, "smoke": smoke}
        require(before in values[kind], "Invalid mutation setup: " + label)
        values[kind] = values[kind].replace(before, after, 1)
        try:
            invariant(values["project"], values["application"], values["source"],
                      values["api"], values["smoke"])
        except (AssertionError, ValueError, ET.ParseError):
            continue
        raise AssertionError("Unsafe C03-04 mutation not rejected: " + label)
    print("AUDIT B PASS C03-04: Domain/Application/Infrastructure dependency "
          "direction, PLATFORM SELECT-only, no API/HTTP credentials, "
          "10/10 adversarial mutations rejected.")


def audit_c():
    invariant(INFRA_PROJECT.read_text(encoding="utf-8"),
              APP_PROJECT.read_text(encoding="utf-8"),
              ADAPTER.read_text(encoding="utf-8"),
              API.read_text(encoding="utf-8"),
              SMOKE.read_text(encoding="utf-8"))
    original.actual_graph()
    require(refs(TEST_PROJECT.read_text(encoding="utf-8")) ==
            ["../../src/Crecisol.Infrastructure/Crecisol.Infrastructure.csproj"],
            "Contract tests may reference only Infrastructure project")
    for name in ("build", "contract", "mysql", "c03-app", "c01-app",
                 "c03-domain", "c01-domain"):
        marker = PROOF / name
        require(marker.is_file() and
                marker.read_text(encoding="utf-8").strip() == "PASS: " + name,
                "Missing real successful build/test marker: " + name)
    tests = TEST.read_text(encoding="utf-8")
    for mode in ('"contracts"', '"published"', '"updated"', '"absent"', '"denied"',
                 "OperationCanceledException", "MySqlException",
                 "IPublishedPlatformBrandRevisionReader", "ReadCurrentPublishedRevisionAsync"):
        require(mode in tests, "Positive/negative typed test absent: " + mode)
    print("AUDIT C PASS C03-04: .NET10 Release production API and Infrastructure "
          "contract executable, MySQL8.4 real isolated SELECT-only user "
          "positive/negative test, C03/C01 regressions and graph.")


if __name__ == "__main__":
    try:
        require(len(sys.argv) == 2 and sys.argv[1] in ("A", "B", "C"),
                "usage: c03_04_infrastructure_audits.py [A|B|C]")
        {"A": audit_a, "B": audit_b, "C": audit_c}[sys.argv[1]]()
    except (AssertionError, OSError, KeyError, ValueError, ET.ParseError) as ex:
        print("C03-04 AUDIT FAILED: " + str(ex), file=sys.stderr)
        sys.exit(1)
