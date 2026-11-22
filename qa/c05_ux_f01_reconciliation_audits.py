#!/usr/bin/env python3
"""F01/F06 exact-SHA source reconciliation; never issues commercial or production acceptance."""
from __future__ import annotations

import csv
import io
import json
import re
import subprocess
import sys
from collections import Counter
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
DOCS = ROOT / "docs/c05/ux"
BASE_SHA = "f61a79d242288c686a88a7c3dd711f2be9a48c18"
F06_SHA = "d539b75d13cde64c8ea3279d33ab35b6349c1adf"
F06_MATRIX = "docs/c05/ux/F06_65_SUPERFICIES_VALORACION.csv"
F06_BUYER = "docs/c05/ux/F06_65_REVISION_COMPRADOR_PRELIMINAR_2026_11_22.csv"
F06_TEST = "frontend/tests/c05-f06-inventory-65.spec.ts"
F06_AUDIT = "qa/c05_ux_f06_65_coverage.py"
EXPECTED_BLOBS = {
    F06_MATRIX: "1529df2257b75a9508f1d2276080bea31c9be7b3",
    F06_BUYER: "a70a8df4552f993425ce6a46e3fef5d1447a307f",
    F06_TEST: "adb978087b2f0fd7efb88b481ae4563830b9cf69",
    F06_AUDIT: "cedd507243755295ac10609f22a2e35f973d348b",
}
RECON = "F01_MATRIZ_RECONCILIADA_F06_2026_11_22.csv"


def require(ok: bool, message: str) -> None:
    if not ok:
        raise AssertionError("F01 RECONCILIACION NO-GO: " + message)


def git(*args: str) -> str:
    call = subprocess.run(["git", *args], cwd=ROOT, text=True,
                          encoding="utf-8", capture_output=True, check=False)
    require(call.returncode == 0, "git " + " ".join(args) + ": " + call.stderr[-260:])
    return call.stdout


def pinned(path: str) -> str:
    blob = git("rev-parse", F06_SHA + ":" + path).strip()
    require(blob == EXPECTED_BLOBS[path],
            "F06 immutable source blob mismatch: " + path + " " + blob)
    return git("show", F06_SHA + ":" + path)


def parse_csv(text: str) -> list[dict[str, str]]:
    return list(csv.DictReader(io.StringIO(text)))


def source_csv(name: str) -> list[dict[str, str]]:
    return parse_csv((DOCS / name).read_text(encoding="utf-8"))


def unique(rows: list[dict[str, str]], label: str) -> dict[str, dict[str, str]]:
    required = {f"C05-F01-{i:03}" for i in range(1, 66)}
    found = [row["id"] for row in rows]
    require(len(found) == 65 and len(set(found)) == 65 and set(found) == required,
            label + " missing, unknown or duplicated IDs")
    return {r["id"]: r for r in rows}


def validate() -> None:
    # Pin data exactly to audited GitHub source files, not an untracked copy
    # or the latest moving head of another, still-unmerged PR.
    base_blob = git("hash-object", str(DOCS / "F01_MATRIZ_INTERFACES_BASE.csv")).strip()
    require(base_blob == "a1ff218baf489d69104145048f94e7ff9ee48f73",
            "immutable historical F01 matrix changed")
    base = unique(source_csv("F01_MATRIZ_INTERFACES_BASE.csv"), "F01")
    current = unique(parse_csv(pinned(F06_MATRIX)), "F06")
    buyers = unique(parse_csv(pinned(F06_BUYER)), "F06 exploratory buyer")
    ledger = unique(source_csv(RECON), "F01 reconciled")
    test = pinned(F06_TEST)
    audit = pinned(F06_AUDIT)

    require(Counter(r["tipo"] for r in base.values()) ==
            {"vista": 17, "estado": 18, "complemento": 3, "modal": 27},
            "F01 surface type totals differ from baseline")
    require(Counter(r["tipo"] for r in current.values()) ==
            Counter(r["tipo"] for r in base.values()), "F06 type coverage drift")

    # These are the two observed false-positive scenarios: neither an
    # unauthenticated policy shell nor an unrelated profile can earn coverage.
    for fragment in (
        "id.startsWith('account-terms-current-loaded-')) add('C05-F01-014')",
        "id.startsWith('account-historial-empty-')) add('C05-F01-057')",
    ):
        require(fragment in test, "missing semantic scene mapping: " + fragment)
    for fragment in (
        'scene_id.startswith("account-terms-current-loaded-")',
        '"Condiciones qa-f01" in (data.get("visibleHeadings") or [])',
        'scene_id.startswith("account-historial-empty-")',
        'data.get("route") == "/cuenta?vista=historial"',
        '"Sin actividad disponible" in (data.get("visibleHeadings") or [])',
    ):
        require(fragment in audit, "missing semantic/route/heading guard: " + fragment)
    require("sourceSha" in audit and "runnerSha" in audit and "dialogsOpen" in audit,
            "missing F06 evidence identity or modal guard")

    differences = []
    old_evidence_refs = 0
    for sid in sorted(base):
        a, b, c, row = base[sid], current[sid], buyers[sid], ledger[sid]
        for key, old, now in (
            ("tipo", a["tipo"], b["tipo"]),
            ("interfaz", a["interfaz_o_estado"], b["interfaz_o_estado"]),
            ("Angular", a["componente_fuente"], b["fuente_angular"]),
        ):
            require(old == now, sid + " mismatch in " + key)
        if a["ruta_y_precondicion"] != b["ruta_y_precondicion"]:
            differences.append(sid)
            require(sid == "C05-F01-057"
                    and a["ruta_y_precondicion"] == "/cuenta?vista=perfil [empty]"
                    and b["ruta_y_precondicion"] == "/cuenta?vista=historial [empty]",
                    sid + " unexplained route change")
        require(b["nota_comercial_0_10"].strip() == ""
                and b["dictamen_compraria"] == "PENDIENTE",
                sid + " F06 score/decision falsely claims acceptance")
        require(c["calificacion_final_institucional"] == "PENDIENTE"
                and c["nota_exploratoria_0_10"].strip(),
                sid + " exploratory grade falsely promoted or missing")
        require(row["ambito"] == a["ambito"]
                and row["tipo"] == a["tipo"]
                and row["interfaz_o_estado"] == a["interfaz_o_estado"]
                and row["ruta_historica_F01"] == a["ruta_y_precondicion"]
                and row["ruta_codigo_F06"] == b["ruta_y_precondicion"]
                and row["componente_angular"] == b["fuente_angular"],
                sid + " crosswalk is not true to the original and F06 source")
        require(row["contratos_api_f01"] == a["contratos_api_referenciados"]
                and row["riesgo_acceso_f01"] == a["control_de_acceso_que_se_debe_probar"],
                sid + " API/security trace altered")
        require(row["sha_fuente_base"] == BASE_SHA
                and row["sha_codigo_evaluado"] == F06_SHA,
                sid + " evidence SHA mismatch")
        require(row["ci_inventario_65x5"] == "45032597168 SUCCESS, 65 IDs x 5 viewports"
                and row["artefacto_png_json"] == "14758472442, inventoryIds=" + sid
                and row["ci_angular_net_mysql"] ==
                "45032597178 SUCCESS, 344 Playwright / 340 E2E",
                sid + " stale or invented run/artifact mapping")
        require(row["nota_exploratoria_antigua_0_10"] ==
                c["nota_exploratoria_0_10"]
                and row["defecto_preliminar"] == c["defecto_y_contexto"],
                sid + " exploratory rating provenance changed")
        require(row["nota_institucional_final_0_10"] == ""
                and row["dictamen_final"] == "PENDIENTE"
                and row["revision_visual_integral"].startswith("PENDIENTE:"),
                sid + " false individual visual acceptance")
        expected = (
            "SEMANTICA_CORREGIDA_CONDICIONES_CARGADAS" if sid == "C05-F01-014"
            else "FALSO_POSITIVO_HISTORICO_CORREGIDO_F03" if sid == "C05-F01-057"
            else "ID_TIPO_NOMBRE_RUTA_FUENTE_COINCIDEN"
        )
        require(row["estado_conciliacion"] == expected,
                sid + " wrong reconciliation state")
        if sid == "C05-F01-014":
            require("account-terms-current-loaded-*" in row["criterio_semantico_estricto"],
                    "014 source-only evidence")
        elif sid == "C05-F01-057":
            require("account-historial-empty-*" in row["criterio_semantico_estricto"],
                    "057 profile screenshot falsely counted")
        else:
            require(row["criterio_semantico_estricto"] == "",
                    sid + " unexplained semantic exception")
        if "6384C545" in b["estado_evidencia_sha_actual"]:
            old_evidence_refs += 1

    require(differences == ["C05-F01-057"],
            "expected one historical route false positive, got " + str(differences))
    require(old_evidence_refs == 65,
            "historical F06 evidence references changed; review provenance")
    result = {
        "status": "PASS_static_exact_SHA_reconciliation_only",
        "repository": "crecisol-platform/CRECISOL",
        "F01_baseline": BASE_SHA,
        "F06_code_sha": F06_SHA,
        "surfaces_reconciled": len(ledger),
        "types": dict(Counter(r["tipo"] for r in ledger.values())),
        "historical_wrong_route": differences,
        "semantic_protections": ["C05-F01-014", "C05-F01-057"],
        "old_F06_evidence_cells_identified": old_evidence_refs,
        "inventory_run": 45032597168,
        "inventory_artifact": 14758472442,
        "e2e_run": 45032597178,
        "official_commercial_scores": 0,
        "individual_commercial_decisions": 0,
        "human_visual_or_physical_authenticator_executed_here": False,
        "production_or_tenant_access_executed_here": False,
    }
    print(json.dumps(result, indent=2, ensure_ascii=False))


if __name__ == "__main__":
    try:
        validate()
    except (AssertionError, KeyError, OSError, UnicodeError, csv.Error, ValueError) as exc:
        print(str(exc), file=sys.stderr)
        raise SystemExit(1) from exc
