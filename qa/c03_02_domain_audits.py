#!/usr/bin/env python3
"""CRECISOL C03-02: A source, B security, C architecture & real CI evidence."""
from copy import deepcopy
from hashlib import sha256
from pathlib import Path
import re
import sys
import xml.etree.ElementTree as ET

import c03_01_source as previous

ROOT = Path(__file__).resolve().parents[1]
DOMAIN = ROOT / "backend/src/Crecisol.Domain"
PROJECT = DOMAIN / "Crecisol.Domain.csproj"
SPEC = ROOT / "docs/c03/C03_02_ESPECIFICACION.md"
WORD = ROOT / "docs/CRECISOL_DOCUMENTO_MAESTRO_UNICO.docx"
PROOF = ROOT / ".c03-02-ci-proof"
WORD_SHA = "e45c2017e62bbbaa6853298efd5de5d785230005203d8791f791760f6f2c622e"
FORBIDDEN = re.compile(
    r"\busing\s+(?:Microsoft\.AspNetCore|Microsoft\.EntityFrameworkCore|"
    r"MySqlConnector|Dapper|MediatR|System\.Net\.Http|System\.Data)\b|"
    r"\b(?:HttpContext|DbContext|MySqlConnection|SqlConnection|HttpClient|"
    r"IServiceProvider|ServiceCollection)\b"
)


def require(value, message):
    if not value:
        raise AssertionError(message)


def sources():
    # Only authored C# source belongs to the Domain dependency boundary.
    # dotnet build creates obj/ generated GlobalUsings and assembly metadata,
    # which must never be mistaken for repository-authored Domain files.
    return {path.relative_to(DOMAIN).as_posix(): path.read_text(encoding="utf-8")
            for path in DOMAIN.rglob("*.cs")
            if not ({"bin", "obj"} & set(path.relative_to(DOMAIN).parts))}


def pure(project_text, code):
    root = ET.fromstring(project_text)
    target = root.find(".//TargetFramework")
    require(target is not None and target.text == "net10.0", "Domain TFM changed")
    require(not root.findall(".//PackageReference"), "Domain imported a NuGet/SDK package")
    require(not root.findall(".//ProjectReference"), "Domain imported another project")
    for path, body in code.items():
        require(not FORBIDDEN.search(body), "Forbidden Domain coupling in " + path)
    require("sealed record DomainRevision" in code["Common/DomainRevision.cs"] and
            "RequireNewerThan" in code["Common/DomainRevision.cs"] and
            "Successor" in code["Common/DomainRevision.cs"],
            "Missing immutable version contract")
    require("sealed record BrandScopeKey" in code["Branding/BrandScopeKey.cs"] and
            "Guid.Empty" in code["Branding/BrandScopeKey.cs"],
            "Missing pure scoped brand identity")


def audit_a():
    require(sha256(WORD.read_bytes()).hexdigest() == WORD_SHA,
            "Original Word does not match the approved version")
    parts, table, _, _ = previous.original_parts()
    require(len(parts) == 7 and len(table) == 9 and table[1][0] == "Domain",
            "Original Domain table row changed")
    spec = SPEC.read_text(encoding="utf-8")
    for key in ("C03-02", "T01-R02", "P03", "P04", "DIF-C03-01", WORD_SHA,
                "MAE-C03-01..08", "CERRADA TÉCNICAMENTE", "2/10",
                "FORMALIZACIÓN DOCUMENTAL PENDIENTE",
                "C03-03 NO INICIADA", "C03 NO CERTIFICADO", "C04 BLOQUEADO",
                "BOLA/IDOR/MFA", "DomainRevision", "BrandScopeKey", "Rollback"):
        require(key in spec, "Missing C03-02 traceability: " + key)
    require((ROOT / "backend/tests/Crecisol.Domain.ContractsTests/Program.cs").is_file(),
            "New Domain contract test project not found")
    print("AUDIT A PASS C03-02: exact canonical Word, seven original units, "
          "nine source rows, T01-R02 Domain, scope and status checked.")


def audit_b():
    project, code = PROJECT.read_text(encoding="utf-8"), sources()
    pure(project, code)
    previous.actual_graph()
    mutations = [
        ("NuGet ORM", "project", "<ItemGroup><PackageReference "
         "Include='Microsoft.EntityFrameworkCore' Version='10.0.0'/></ItemGroup>"),
        ("ProjectReference Infrastructure", "project", "<ItemGroup><ProjectReference "
         "Include='../Crecisol.Infrastructure/Crecisol.Infrastructure.csproj'/></ItemGroup>"),
        ("HTTP type import", "code", "using Microsoft.AspNetCore.Http;\n"),
        ("MySQL SDK import", "code", "using MySqlConnector;\n"),
        ("Service locator", "code", "using System;\ninternal class Bad { IServiceProvider? Services; }\n"),
    ]
    for label, kind, injection in mutations:
        varied_project, varied_code = project, deepcopy(code)
        if kind == "project":
            varied_project = project.replace("</Project>", injection + "</Project>")
        else:
            varied_code["Common/DomainRevision.cs"] = injection + varied_code["Common/DomainRevision.cs"]
        try:
            pure(varied_project, varied_code)
        except (AssertionError, ET.ParseError):
            continue
        raise AssertionError("Unsafe dependency mutation accepted: " + label)
    print("AUDIT B PASS C03-02: Domain has no project/SDK/HTTP/SQL coupling, "
          "physical graph acyclic; 5/5 adversarial dependency cases rejected.")


def audit_c():
    pure(PROJECT.read_text(encoding="utf-8"), sources())
    previous.actual_graph()
    for name in ("build", "c03-domain", "c01-domain", "application"):
        file = PROOF / name
        require(file.is_file() and file.read_text(encoding="utf-8").strip() ==
                "PASS: " + name, "Missing real execution marker: " + name)
    tests = (ROOT / "backend/tests/Crecisol.Domain.ContractsTests/Program.cs"
             ).read_text(encoding="utf-8")
    for marker in ("DomainRevision.From(0)", "DomainRevision.From(-1)",
                   "DomainRevision.From(long.MaxValue).Successor()",
                   "BrandScopeKey.Create(BrandScope.Tenant, Guid.Empty)",
                   "ExpectedCurrentPublishedRevision = 2",
                   "BrandPreviewReference"):
        require(marker in tests, "Negative test missing: " + marker)
    print("AUDIT C PASS C03-02: real .NET10 Release, C03 Domain, C01 Domain, "
          "Application executable tests; private auth/MySQL not claimed.")


if __name__ == "__main__":
    try:
        require(len(sys.argv) == 2 and sys.argv[1] in {"A", "B", "C"},
                "usage: c03_02_domain_audits.py [A|B|C]")
        {"A": audit_a, "B": audit_b, "C": audit_c}[sys.argv[1]]()
    except (AssertionError, OSError, ET.ParseError, KeyError, ValueError) as ex:
        print("C03-02 AUDIT FAILED: " + str(ex), file=sys.stderr)
        sys.exit(1)
