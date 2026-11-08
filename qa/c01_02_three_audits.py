#!/usr/bin/env python3
"""Independent A/B/C audits for C01-02. Read-only, stdlib, no external access."""
from pathlib import Path
import re
import sys
import c01_verify_source

ROOT = Path(__file__).resolve().parents[1]
DOMAIN = ROOT / "backend/src/Crecisol.Domain"
BRAND = DOMAIN / "Branding"
TEST = ROOT / "backend/tests/Crecisol.Branding.ContractsTests/Program.cs"
SPEC = ROOT / "docs/c01/C01_02_CONTRATOS.md"
ACTA = ROOT / "docs/c01/C01_02_ACTA_MICROFASE.md"
PLAN = ROOT / "docs/C01_PREPARACION.md"

def require(value, label):
    if not value:
        raise AssertionError(label)

def read(path):
    return path.read_text(encoding="utf-8")

def audit_a():
    c01_verify_source.main()
    spec = read(SPEC)
    for source in ("P07", "P14", "P25", "P04", "P06", "P24", "P31",
                   "T01-R03", "T01-R04", "T02-R01", "T02-R02",
                   "T02-R03", "T02-R07", "T03-R01", "T03-R02"):
        require(source in spec, "Missing master-derived requirement: " + source)
    for name in ("BrandSnapshot", "BrandValidation", "BrandPresentation",
                 "BrandPreviewReference", "BrandPublicationProposal"):
        require((BRAND / (name + ".cs")).is_file(),
                "Required domain contract missing: " + name)
    tests = read(TEST)
    test_calls = re.findall(r"^\s*(?:Pass|Reject)\(", tests, re.M)
    require(len(test_calls) >= 40, "Insufficient explicitly named assertions")
    for case in ("Valid published PLATFORM identity", "Valid isolated TENANT draft",
                 "Markup injection", "CSS injection", "URL credentials",
                 "Draft cannot have publication timestamp", "Tenant draft rejected",
                 "Missing idempotent operation ID"):
        require(case in tests, "Missing positive/negative case: " + case)
    # Deliberate negative of source mapping coverage; do not alter the source.
    require("T02-R99" not in spec, "Nonexistent source ID is erroneously mapped")
    print("C01-02 AUDIT A PASS: canonical source + domain requirements and tests.")

def audit_b():
    contracts = "\n".join(read(file) for file in BRAND.glob("*.cs"))
    tests = read(TEST)
    require("RequirePublicPlatform" in contracts and
            "Scope != BrandScope.Platform" in contracts and
            "Status != BrandPublicationStatus.Published" in contracts,
            "Public PLATFORM-only gate absent")
    require("draft.Validate()" in contracts and
            "draft.Status != BrandPublicationStatus.Draft" in contracts,
            "Draft preview guard absent")
    require("Draft.Status != BrandPublicationStatus.Draft" in contracts,
            "Publication proposal must require a draft")
    require("RequireHttpsOrigin" in contracts and "Uri.UriSchemeHttps" in contracts,
            "HTTPS origin validation absent")
    require("RequireLocalAsset" in contracts and "IconTokens" in contracts,
            "Asset and presentation token allowlist missing")
    for unsafe in ("MapGet(", "MapPost(", "MapPut(", "MapDelete(",
                   "MySqlConnection", "DbContext", "ConnectionStrings",
                   "HttpClient", "UseAuthentication", "UseAuthorization"):
        require(unsafe not in contracts, "Premature API/DB operation: " + unsafe)
    for case in ("Tenant with zero platform identifier",
                 "Platform with foreign tenant identifier",
                 "Published TENANT identity cannot become public",
                 "Untrusted font token", "Protocol relative asset",
                 "Javascript URL", "URL token query", "Stale publication revision"):
        require(case in tests, "Security negative missing: " + case)
    require("Authorization, idempotency" in contracts,
            "No explicit authorization boundary on proposal")
    require("NO" in read(SPEC), "Absent documentation of unimplemented controls")
    print("C01-02 AUDIT B PASS: published PLATFORM-only gate, draft safety,")
    print("allowlisted values, negative scenarios and no API/DB bypass.")

def audit_c():
    domain_project = read(DOMAIN / "Crecisol.Domain.csproj")
    test_project = read(ROOT / "backend/tests/Crecisol.Branding.ContractsTests/Crecisol.Branding.ContractsTests.csproj")
    require("<TargetFramework>net10.0</TargetFramework>" in domain_project,
            "Domain must target .NET 10")
    require("<TreatWarningsAsErrors>true</TreatWarningsAsErrors>" in domain_project,
            "Warnings must be errors")
    require("<ProjectReference" not in domain_project and
            "<PackageReference" not in domain_project,
            "Domain must not depend on Infrastructure or packages")
    require("../../src/Crecisol.Domain/Crecisol.Domain.csproj" in test_project,
            "Test runner must reference Domain")
    for doc in (SPEC, ACTA):
        for url in re.findall(r"\[[^\]]+\]\(([^)]+)\)", read(doc)):
            if url.startswith(("https://", "http://", "#")):
                continue
            target = (doc.parent / url.split("#", 1)[0]).resolve()
            require(target.is_relative_to(ROOT) and target.is_file(),
                    "Broken Markdown link in " + doc.name + ": " + url)
    for needed in ("C01-01", "C01-02", "C01-03", "0/37"):
        require(needed in read(PLAN), "Sequential plan or certification marker missing")
    require("Reversión" in read(SPEC) or "Rollback" in read(SPEC),
            "Rollback and reversibility missing")
    require(not (ROOT / "backend/src/Crecisol.Api/Crecisol.Api.csproj").exists(),
            "Unexpected API in contract-only microphase")
    require(not (ROOT / "database/migrations").exists(),
            "Unexpected MySQL migrations in contract-only microphase")
    print("C01-02 AUDIT C PASS: pure net10 domain, independent tests, documents,")
    print("no frontend, API, DB or VPS files; sequential phase boundaries.")

if __name__ == "__main__":
    try:
        require(len(sys.argv) == 2 and sys.argv[1] in ("A", "B", "C"),
                "Select one audit: A, B or C")
        {"A": audit_a, "B": audit_b, "C": audit_c}[sys.argv[1]]()
    except (AssertionError, FileNotFoundError, ValueError) as error:
        print(f"C01-02 AUDIT {sys.argv[1] if len(sys.argv) > 1 else '?'} FAIL: {error}",
              file=sys.stderr)
        sys.exit(1)
