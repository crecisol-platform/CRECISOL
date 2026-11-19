#!/usr/bin/env python3
"""C05 UX F01: scope-bound SOURCE inventory audits, not a visual certificate."""
from collections import Counter
import csv
from pathlib import Path
import re
import sys

ROOT = Path(__file__).resolve().parents[1]
BASE = "f61a79d242288c686a88a7c3dd711f2be9a48c18"
DOCS = ROOT / "docs/c05/ux"
PAGES = ROOT / "frontend/src/app/pages"


def source(path: Path) -> str:
    return path.read_text(encoding="utf-8")


def require(value: bool, message: str) -> None:
    if not value:
        raise AssertionError("F01 NO-GO: " + message)


def records(name: str) -> list[dict[str, str]]:
    with (DOCS / name).open(encoding="utf-8", newline="") as stream:
        return list(csv.DictReader(stream))


def operations() -> dict[str, list[str]]:
    text = source(ROOT / "backend/src/Crecisol.Api/Modules/Identity/OperationalIdentityModule.cs")
    return {
        name: re.findall(r'"([^"]+)"', fields)
        for name, fields in re.findall(r'\["([^"]+)"\]\s*=\s*\[([^\]]*)\]', text)
    }


def audit_a() -> None:
    ui = records("F01_MATRIZ_INTERFACES_BASE.csv")
    api = records("F01_CONTRATOS_API_BASE.csv")
    require(len(ui) == 65, "unexpected number of visual surfaces")
    require(Counter(row["tipo"] for row in ui) ==
            {"vista": 17, "estado": 18, "complemento": 3, "modal": 27},
            "type totals differ from source coverage")
    require(len({row["id"] for row in ui}) == len(ui), "duplicate visual ID")
    require(len(api) == 78, "API contract coverage changed")
    require(len({row["endpoint"] for row in api}) == len(api), "duplicate API endpoint")
    for row in ui:
        require(row["componente_fuente"].startswith("frontend/src/app/"),
                "component out of frontend scope: " + row["id"])
        require((ROOT / row["componente_fuente"]).is_file(),
                "component missing: " + row["componente_fuente"])
        require(row["estado_F01"] in ("PENDIENTE", "VALIDADO"),
                "unknown source status")
    declared = operations()
    require(len(declared) == 74, "backend operation count changed")
    require(sum(name.startswith("platform/") for name in declared) == 31,
            "platform operation count changed")
    targets = {
        ("/api/v1/platform-identity/" + name[9:]
         if name.startswith("platform/") else "/api/v1/identity/" + name):
        "; ".join(fields) for name, fields in declared.items()
    }
    indexed = {row["endpoint"]: row for row in api}
    for route, fields in targets.items():
        require(route in indexed, "unmapped backend operation: " + route)
        require(indexed[route]["campos_exigidos_transport"] == fields,
                "request contract drift: " + route)
    for extra in ("/api/v1/identity/registration-intents",
                  "/api/v1/identity/registration-proofs/confirm",
                  "/api/v1/identity/registration-completions",
                  "/api/v1/private/cases/{resourceId}"):
        require(extra in indexed, "independent API missing: " + extra)
    tabs = source(PAGES / "identity-portal.component.ts")
    for view in ("ingresar", "registro", "confirmar", "recuperar", "invitacion",
                 "acceso-existente", "operadores", "resumen", "perfil", "llaves",
                 "seguridad", "administracion"):
        require(f"'{view}'" in tabs, "view not allowlisted: " + view)
    forms = {name: source(PAGES / name).count("<form ")
             for name in ("identity-guest.component.html",
                          "identity-mfa.component.html",
                          "identity-recovery.component.html",
                          "identity-workspace.component.html",
                          "identity-portal.component.html",
                          "platform-identity-panel.component.html",
                          "platform-operator-cases.component.html",
                          "platform-operator-sessions.component.html",
                          "platform-operator-profile.component.html",
                          "platform-operator-security.component.html",
                          "platform-operator-monitor.component.html")}
    require(sum(forms.values()) == 47, "forms changed: " + str(forms))
    require(forms["identity-workspace.component.html"] == 15, "account work forms changed")
    operator = ("platform-identity-panel", "platform-operator-cases",
                "platform-operator-sessions", "platform-operator-profile",
                "platform-operator-security", "platform-operator-monitor")
    require(sum(forms[name + ".component.html"] for name in operator) == 19,
            "platform forms changed")
    modal_count = sum(source(PAGES / name).count("<cs-institutional-modal")
                      for name in forms)
    require(modal_count == 27, "modal count drift")
    print("F01/A static PASS: 65 surfaces, 27 dialogs, 47 forms, 78 API contracts")


def audit_b() -> None:
    ui = records("F01_MATRIZ_INTERFACES_BASE.csv")
    capture = source(ROOT / "frontend/tests/c05-f01-visual-baseline.spec.ts")
    fields = (row["control_de_acceso_que_se_debe_probar"] for row in ui)
    require(all(bool(permission.strip()) for permission in fields),
            "permission/risk mapping missing")
    require("synthetic: true" in capture and "Persona ficticia" in capture and
            "Operador ficticio" in capture, "synthetic-only evidence not explicit")
    require("password: 'real'" not in capture and "localStorage.setItem" not in capture,
            "secret/credential persistence introduced")
    require(BASE in capture, "screenshot evidence not tied to frozen SHA")
    api_module = source(ROOT / "backend/src/Crecisol.Api/Modules/Identity/OperationalIdentityModule.cs")
    for guard in ("IsLoopback", ".example.test", "StrictVerifiedHostTransport",
                  "X-Tenant", "X-Role", "Origin", "bearer"):
        require(guard in api_module, "backend boundary not mapped: " + guard)
    require("/api/v1/platform-identity/" in api_module and
            "/api/v1/identity/" in api_module, "realm separation not mapped")
    print("F01/B static PASS: scope, fictitious data, realm and contract guards mapped")
    print("F01/B runtime privacy, expiry and tenant-negative proofs: NOT EXECUTED")


def audit_c() -> None:
    capture = source(ROOT / "frontend/tests/c05-f01-visual-baseline.spec.ts")
    workflow = source(ROOT / ".github/workflows/c05-ux-f01-baseline.yml")
    for target in ("320", "390", "768", "1440"):
        require("width: " + target in capture, "viewport missing: " + target)
    for part in ("public journeys", "authenticated business surfaces",
                 "platform console", "empty, loading, error and expiry simulation"):
        require(part in capture, "visual capture branch missing: " + part)
    for code in ("sourceSha: BASELINE", "page.screenshot", "captureModals",
                 "testInfo.outputPath", "synthetic: true", "bodyScrollX"):
        require(code in capture, "capture metadata missing: " + code)
    for target in ("node-version: '24.15.0'", "playwright test", "upload-artifact@",
                   "retention-days:", "contents: read"):
        require(target in workflow, "CI capture guard missing: " + target)
    require("F01" in source(DOCS / "F01_CAPTURAS_Y_GATE.md"),
            "procedures absent")
    print("F01/C static PASS: 4 viewport presets and reproducible screenshot harness")
    print("F01/C Chromium screenshots and human ratings: NOT EXECUTED by this audit")


if __name__ == "__main__":
    try:
        require(len(sys.argv) == 2 and sys.argv[1] in ("A", "B", "C"),
                "run with A|B|C")
        {"A": audit_a, "B": audit_b, "C": audit_c}[sys.argv[1]]()
    except (AssertionError, OSError, csv.Error) as exc:
        print("FAIL:", exc, file=sys.stderr)
        raise SystemExit(1) from exc
