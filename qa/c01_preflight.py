#!/usr/bin/env python3
"""C01 static preflight. Does NOT compile Angular/.NET, run MySQL or certify a chapter."""
import hashlib
import json
import re
import sys
from pathlib import Path
from zipfile import ZipFile

ROOT = Path(__file__).resolve().parents[1]
problems: list[str] = []
checks = 0

def check(condition: bool, label: str) -> None:
    global checks
    checks += 1
    if not condition:
        problems.append(label)

def get(path: str) -> str:
    return (ROOT / path).read_text(encoding="utf-8")

word = ROOT / "docs/CRECISOL_DOCUMENTO_MAESTRO_UNICO.docx"
pdf = ROOT / "docs/CRECISOL_DOCUMENTO_MAESTRO_UNICO.pdf"
check(word.exists() and pdf.exists(), "Canonical documents must both exist")
if word.exists():
    check(hashlib.sha256(word.read_bytes()).hexdigest() ==
          "e45c2017e62bbbaa6853298efd5de5d785230005203d8791f791760f6f2c622e",
          "Wrong canonical Word SHA-256")
    with ZipFile(word) as source:
        image = source.read("word/media/image1.png")
    check(image.startswith(b"\x89PNG\r\n\x1a\n") and
          hashlib.sha256(image).hexdigest() ==
          "9e83d1d0a891565cf80a21a7e90d7afcaa24c9bb63e7d2a589790127dadd549f",
          "Logo image differs from approved master")

seed = json.loads(get("backend/src/Crecisol.Api/Configuration/platform-brand.published.json"))
check(seed["scope"] == "Platform" and seed["scopeId"] ==
      "00000000-0000-0000-0000-000000000000", "Brand seed must be platform-scoped")
check(seed["status"] == "Published" and seed["revision"] >= 1,
      "Public brand must have published revision")
check(seed["name"] == "CRECISOL" and seed["slogan"] == "Tu negocio, a tu manera.",
      "Canonical identity mismatches master")
check(seed["descriptor"] == "Comercio · Servicios · Membresías · Crecimiento",
      "Descriptor mismatches master")
check(bool(re.fullmatch(r"/assets/[A-Za-z0-9-]+[.]png", seed["logoUrl"])),
      "Logo URL is not approved local asset")
check(all(re.fullmatch(r"#[0-9a-fA-F]{6}", seed[name])
          for name in ("primaryColor", "accentColor")), "Unvalidated brand colors")
check(seed["websiteUrl"] is None, "Do not claim domain ownership without evidence")

api = get("backend/src/Crecisol.Api/Program.cs")
service = get("frontend/src/app/branding/public-brand.service.ts")
check('MapGet("/api/v1/public/branding/platform"' in api and
      '/api/v1/public/branding/platform' in service, "Frontend/API path disagreement")
check(not any(x in api for x in ("MapPost(", "MapPut(", "MapDelete(", "MapPatch(")),
      "No unauthenticated administrative writes in C01")
check("no-store" in api and "JsonPlatformBrandSource" in api,
      "Public response or trusted source misconfigured")
check("isPublicPlatformBrand(payload)" in service and
      "this.identity.set(null)" in service, "Frontend must fail closed on invalid payload")
check("innerHTML" not in get("frontend/src/app/app.component.html"),
      "Do not introduce dynamic HTML rendering")
check("CRECISOL" not in get("frontend/src/app/app.component.html"),
      "Brand name must not be hardcoded into template content or accessibility labels")
check("[alt]" in get("frontend/src/app/app.component.html") and
      "[attr.aria-label]" in get("frontend/src/app/app.component.html"),
      "Accessible identity labels must reflect the published brand snapshot")

package = json.loads(get("frontend/package.json"))
check(package["dependencies"]["@angular/core"].startswith("22."),
      "Angular major must be 22")
check(package["devDependencies"]["typescript"].startswith("6.0."),
      "Angular 22 requires compatible TypeScript 6.0")
check("24.15.0" in package["engines"]["node"], "Node 24 minimum missing")
for part in ("Crecisol.Domain", "Crecisol.Application", "Crecisol.Infrastructure", "Crecisol.Api"):
    check("net10.0" in get(f"backend/src/{part}/{part}.csproj"),
          f"{part} must target net10.0")

sql = get("database/migrations/0001_c01_brand_identity.sql")
check("FOREIGN KEY (scope_kind, scope_id, revision_number)" in sql
      and "ENGINE=InnoDB" in sql, "Brand FK/engine missing")

if problems:
    print("C01 STATIC PREFLIGHT FAILED")
    for item in problems:
        print(" -", item)
    sys.exit(1)
print(f"C01 STATIC PREFLIGHT PASSED: {checks} checks.")
print("Limits: no Angular build, .NET compilation, MySQL execution or cross-tenant runtime tests.")
