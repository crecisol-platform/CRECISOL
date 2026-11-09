#!/usr/bin/env python3
"""C01-03 three independent, non-mutating audit gates. No DB or network access."""
from pathlib import Path
import re
import sys
import c01_verify_source

ROOT = Path(__file__).resolve().parents[1]
DDL = ROOT / "database/c01_03_brand_schema.sql"
SMOKE = ROOT / "database/tests/c01_03_mysql_smoke.sh"
DOMAIN = ROOT / "backend/src/Crecisol.Domain/Branding"
SPEC = ROOT / "docs/c01/C01_03_MODELO_MYSQL.md"
MATRIX = ROOT / "docs/c01/C01_01_MATRIZ_REQUISITOS.md"
PLAN = ROOT / "docs/C01_PREPARACION.md"

def read(path):
    return path.read_text(encoding="utf-8")

def require(condition, message):
    if not condition:
        raise AssertionError(message)

def audit_a():
    c01_verify_source.main()
    sql, spec, matrix, script = map(read, (DDL, SPEC, MATRIX, SMOKE))
    require(DDL.is_file() and SMOKE.is_file(), "Missing schema or runnable SQL integration test")
    for key in ("P04", "P06", "P07", "P14", "P25", "P31",
                "T01-R03", "T01-R04", "T02-R01", "T02-R02",
                "T02-R03", "T02-R07", "T03-R02"):
        require(key in spec and key in matrix, "Master source not traced: " + key)
    for table in ("brand_scopes", "brand_revisions", "brand_publications"):
        require("CREATE TABLE " + table in sql, "Missing mapped table: " + table)
    for field in ("scope_kind", "scope_id", "revision", "publication_status", "name",
                  "slogan", "descriptor", "primary_color", "accent_color",
                  "logo_url", "website_url", "published_at_utc", "favicon_url",
                  "typography_token", "icon_set_token", "seo_title", "seo_description",
                  "copyright_notice"):
        require(re.search(r"\b" + field + r"\b", sql), "Missing Domain field: " + field)
    require(script.count('expect_denied "') >= 20, "Too few actual negative integrity cases")
    require(script.count('pass_equal "') >= 8, "Too few positive SQL assertions")
    require("0/37" in spec and "C01-04" in spec, "Missing pending chapter boundary")
    print("C01-03 AUDIT A PASS: canonical source, source IDs, schema map, runtime test coverage.")

def audit_b():
    sql, script, spec = map(read, (DDL, SMOKE, SPEC))
    for check in ("chk_brand_scope_kind", "chk_brand_scope_identity",
                  "chk_brand_revision_positive", "chk_brand_publication_status",
                  "chk_brand_publication_time", "chk_brand_colors",
                  "chk_brand_asset_paths", "chk_brand_presentation_tokens",
                  "chk_brand_presentation_fields", "chk_publication_status_fixed"):
        require(check in sql, "Missing fail-closed CHECK: " + check)
    for fk in ("fk_brand_revision_scope", "fk_brand_publication_revision"):
        require(fk in sql, "Missing composite FK: " + fk)
    require("FOREIGN KEY (scope_kind, scope_id)" in sql, "Revision must be scope-bound")
    require("(scope_kind, scope_id, published_revision, publication_status)" in sql,
            "Published pointer must verify scope, revision and Published status")
    for label in ("tenant with empty id", "platform with tenant id",
                  "draft cannot be publication pointer", "cross-tenant revision reference",
                  "nonexistent revision", "nonlocal brand asset", "invalid presentation token",
                  "alter referenced published status"):
        require(label in script, "Absent security negative: " + label)
    require("--network none" in script and "docker exec" in script,
            "DB test must be isolated and use local socket")
    require("MYSQL_ALLOW_EMPTY_PASSWORD=yes" in script and "--network none" in script,
            "Ephemeral empty password may be used only with network disabled")
    for dangerous in ("--publish ", "-p 3306", "hostinger.com", "crecisol.com:",
                      "mysql://", "DROP DATABASE", "curl ", "wget ", "docker push",
                      "solqaryn/Solqaryn"):
        require(dangerous not in sql and dangerous not in script,
                "Prohibited external secret or execution indicator: " + dangerous)
    require("NO GO" in spec and "no" in spec.lower(), "Unimplemented auth boundary not documented")
    print("C01-03 AUDIT B PASS: scope/status integrity, negative cases and no external DB.")

def audit_c():
    sql, spec, script = map(read, (DDL, SPEC, SMOKE))
    snapshot = read(DOMAIN / "BrandSnapshot.cs")
    presentation = read(DOMAIN / "BrandPresentation.cs")
    validation = read(DOMAIN / "BrandValidation.cs")
    project = read(ROOT / "backend/src/Crecisol.Domain/Crecisol.Domain.csproj")
    require("<TargetFramework>net10.0</TargetFramework>" in project, "Domain must use .NET 10")
    require("enum BrandScope { Platform = 1, Tenant = 2 }" in snapshot,
            "Persistence scope enum diverged")
    require("enum BrandPublicationStatus { Draft = 1, Published = 2 }" in snapshot,
            "Persistence publication enum diverged")
    for marker in ("Revision < 1", "RequirePublicPlatform", "RequireRgb"):
        require(marker in snapshot, "Domain contract changed: " + marker)
    for field, size in (("name", 100), ("slogan", 160), ("descriptor", 180),
                        ("website_url", 255), ("seo_title", 120),
                        ("seo_description", 260), ("copyright_notice", 220)):
        require(re.search(r"\b" + field + r" VARCHAR\(" + str(size) + r"\)", sql),
                "Domain/data length mismatch: " + field)
    for marker in ("system", "humanist", "editorial", "outline", "solid"):
        require(marker in presentation and marker in sql,
                "Presentation token mismatch: " + marker)
    require("RequireLocalAsset" in validation and "RequireHttpsOrigin" in validation,
            "Missing Domain input checks")
    require(sql.count("ENGINE=InnoDB") == 3, "All three tables must be InnoDB")
    require(sql.count("PRIMARY KEY (scope_kind, scope_id") == 3, "Scope-first composite PK missing")
    require("UTC_TIMESTAMP(6)" in sql and "DATETIME(6)" in sql,
            "UTC microsecond convention absent")
    require("DROP TABLE brand_publications" in script and
            "DROP TABLE brand_revisions" in script and
            "DROP TABLE brand_scopes" in script, "No reversible disposable DDL test")
    for word in ("Reversión", "C01-04", "0/37", "inmutable", "autorización"):
        require(word.lower() in spec.lower(), "Risk or rollback not documented: " + word)
    for path in (ROOT / "docs/MASTER_REFERENCE.md", MATRIX, PLAN):
        require(path.is_file(), "Missing canonical policy: " + str(path))
    require(not (ROOT / "backend/src/Crecisol.Api").exists(),
            "Premature C01-04 API in C01-03")
    print("C01-03 AUDIT C PASS: .NET contracts, composite indexes, UTC, rollback, phase split.")

if __name__ == "__main__":
    try:
        require(len(sys.argv) == 2 and sys.argv[1] in ("A", "B", "C"),
                "Usage: audit A|B|C")
        {"A": audit_a, "B": audit_b, "C": audit_c}[sys.argv[1]]()
    except (AssertionError, OSError, ValueError) as error:
        print(f"C01-03 AUDIT FAIL: {error}", file=sys.stderr)
        sys.exit(1)
