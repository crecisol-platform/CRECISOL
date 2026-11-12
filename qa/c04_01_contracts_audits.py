#!/usr/bin/env python3
"""C04-01 limited A/B/C real .NET contracts audit; NEVER a cross-tenant DB pentest."""
from __future__ import annotations

import os
from pathlib import Path
import re
import sys

from c04_01_normative_inventory import chapter4, EXPECTED
from c04_01_preparation_audits import canonical_active_word, ROOT

SOURCES = {
    "id": ROOT / "backend/src/Crecisol.Domain/Tenancy/TenantId.cs",
    "entity": ROOT / "backend/src/Crecisol.Domain/Tenancy/TenantCatalogSnapshot.cs",
    "app": ROOT / "backend/src/Crecisol.Application/Tenancy/ReadInternalTenantCatalog.cs",
    "tests": ROOT / "backend/tests/Crecisol.TenantCatalog.ContractsTests/Program.cs",
    "spec": ROOT / "docs/c04/C04_01_IMPLEMENTACION_CONTRATOS.md",
}
PROOF = ROOT / ".c04-01-contracts-proof"
MARKERS = {
    "id": [
        "if (value == Guid.Empty)",
        "private TenantId(Guid value)",
        "TenantId[redacted]",
    ],
    "entity": [
        "if (!Enum.IsDefined(state) || !Enum.IsDefined(addressMode))",
        "DomainRevision.From(revision);",
        "updatedAtUtc.Offset != TimeSpan.Zero",
        "UnicodeCategory.Format",
        "TenantCatalogSnapshot[redacted]",
        "public enum TenantAddressMode",
    ],
    "app": [
        "if (!await authorization.AllowsInternalCatalogReadAsync(cancellationToken))",
        "if (snapshot.TenantId != id)",
        "return new InternalTenantCatalogView(",
        "ArgumentNullException.ThrowIfNull(id);",
        "cancellationToken.ThrowIfCancellationRequested();",
    ],
    "tests": [
        "No backend permission -> reject valid tenant ID",
        "Hostile reader returning another tenant must fail closed",
        "Internal projection never serializes",
        "Unavailable policy fails closed",
        "Diagnostic stringification redacts tenant identifiers and names",
    ],
    "spec": [
        "C04-01 · Contratos internos implementados",
        "95be2bb0210e108317e67bd08fb9b2899d33554e684bb2d9ae3c8eced6995058",
        "NO HAY CATÁLOGO OPERATIVO",
        "1735–1740",
        "1797–1805",
    ],
}
REQUIRED_WORD = [
    "El acceso tenant se resuelve automáticamente, sin selector",
    "host solo determina el sitio visitado",
    "nunca confiar en tenant_id aportado por navegador",
    "No publicar tenant parcialmente creado",
    "FK compuestas con tenant",
    "SHARED A/B y DEDICATED C",
    "C04-01 Extraer / C04-02 Diseñar",
]

def need(condition: bool, msg: str) -> None:
    if not condition:
        raise AssertionError(msg)

def load() -> dict[str, str]:
    return {key: path.read_text(encoding="utf-8") for key, path in SOURCES.items()}

def contract(files: dict[str, str]) -> None:
    for key, tokens in MARKERS.items():
        for marker in tokens:
            need(marker in files[key], key + ": missing safety contract " + marker[:72])

def audit_a() -> None:
    need(canonical_active_word(), "Canonical Word/PDF hash mismatch")
    rows = chapter4()
    text = "\n".join(value for _, value in rows)
    need(len(rows) == 72, "Unexpected original C04 source paragraph count")
    for value in REQUIRED_WORD:
        need(value in text, "C04 word requirement missing: " + value)
    need(EXPECTED in SOURCES["spec"].read_text(encoding="utf-8"),
         "C04 normative source fingerprint is not in implementation matrix")
    need("| C04-01 | Catálogo confiable de tenants |" in
         (ROOT / "docs/planificacion-futura/C04_MICROFASES_PROPUESTAS.md").read_text(encoding="utf-8"),
         "Technical C04-01 proposal changed")
    contract(load())
    print("PASS C04-01 A: original Word C04 72 paragraphs + 7 normative invariants; "
          "Domain/Application mapping and 12-vs-8 numbering verified")

def audit_b() -> None:
    files = load()
    contract(files)
    api = (ROOT / "backend/src/Crecisol.Api/Program.cs").read_text(encoding="utf-8")
    need("Tenancy" not in api and "/api/v1/tenants" not in api,
         "A private tenant API was enabled prematurely")
    need(not list((ROOT / "database").glob("c04*")),
         "C04-02 or C04 dedicated schema was prematurely added")
    for key in ("id","entity","app"):
        need("Microsoft.AspNetCore" not in files[key] and
             "MySql" not in files[key] and
             "HttpContext" not in files[key],
             "Pure Domain/Application unexpectedly depends on transport/DB: " + key)
    accepted = 0
    for key, tokens in MARKERS.items():
        if key == "spec":
            continue
        for token in tokens:
            mutant = dict(files)
            mutant[key] = files[key].replace(token, "[REMOVED TRUST BOUNDARY]")
            try:
                contract(mutant)
            except AssertionError:
                accepted += 1
            else:
                raise AssertionError("Unsafe mutation survived: " + key + " " + token[:45])
    print("PASS C04-01 B: " + str(accepted) +
          " adversarial contract mutations rejected; no tenant API/DDL, no auth bypass or DSN; "
          "runtime cross-tenant MySQL tests NOT PERFORMED")

def audit_c() -> None:
    need(os.environ.get("GITHUB_ACTIONS") == "true",
         "Audit C requires the actual GitHub runner")
    sha = os.environ.get("GITHUB_SHA", "")
    run = os.environ.get("GITHUB_RUN_ID", "")
    need(bool(re.fullmatch("[0-9a-f]{40}", sha) and re.fullmatch("[0-9]+", run)),
         "Invalid GitHub run identity")
    for marker in ("A", "B", "dotnet"):
        p = PROOF / marker
        need(p.is_file(), "Missing real same-run gate: " + marker)
        need(p.read_text(encoding="utf-8").strip() ==
             "PASS " + marker + " sha=" + sha + " run=" + run,
             "Same-run proof mismatch: " + marker)
    api_project = (ROOT / "backend/src/Crecisol.Api/Crecisol.Api.csproj").read_text(encoding="utf-8")
    domain_project = (ROOT / "backend/src/Crecisol.Domain/Crecisol.Domain.csproj").read_text(encoding="utf-8")
    app_project = (ROOT / "backend/src/Crecisol.Application/Crecisol.Application.csproj").read_text(encoding="utf-8")
    need(all("<TargetFramework>net10.0</TargetFramework>" in s for s in
             (api_project, domain_project, app_project)),
         "Unsupported .NET runtime in project graph")
    need("Crecisol.Domain.csproj" in app_project and
         "Crecisol.Application.csproj" not in domain_project,
         "Clean Architecture dependency direction violated")
    contract(load())
    print("PASS C04-01 C: .NET 10 build/tests and A/B audits share SHA/run; "
          "Domain->Application graph checked; no private API/DDL certified")

def main() -> None:
    need(len(sys.argv)==2 and sys.argv[1] in ("A","B","C"),
         "Usage: c04_01_contracts_audits.py A|B|C")
    {"A":audit_a,"B":audit_b,"C":audit_c}[sys.argv[1]]()

if __name__=="__main__":
    try:
        main()
    except (OSError,AssertionError,ValueError) as err:
        print("FAIL C04-01 contracts audit:",err,file=sys.stderr)
        sys.exit(1)
