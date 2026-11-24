#!/usr/bin/env python3
"""F02 read-only, exact-SHA crosswalk of F01 -> F06 and 65 visual surfaces.

This proves source/contract mapping, not pixel-identical design or ≥9.5/10.
All tests are fail-closed. No backend mutation, token, MFA or production data.
"""
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
F01_SHA = "01efbce91586597006254f721e5736adf991e2a4"
F06_SHA = "d539b75d13cde64c8ea3279d33ab35b6349c1adf"
F02_BASE = "f61a79d242288c686a88a7c3dd711f2be9a48c18"
LEDGER = ROOT / "docs/c05/ux/F02_65_PARIDAD_VISUAL_2026_11_22.csv"
F01_INVENTORY = "docs/c05/ux/F01_MATRIZ_INTERFACES_BASE.csv"
F06_INVENTORY = "docs/c05/ux/F06_65_SUPERFICIES_VALORACION.csv"
F06_BUYER = "docs/c05/ux/F06_65_REVISION_COMPRADOR_PRELIMINAR_2026_11_22.csv"
F06_TEST = "frontend/tests/c05-f06-inventory-65.spec.ts"
F06_AUDIT = "qa/c05_ux_f06_65_coverage.py"
FRONT = "frontend/src/app/pages/"
STYLES = "frontend/src/styles.css"
PORTAL_CSS = FRONT + "identity-portal.component.css"
FLOW_CSS = FRONT + "identity-flow.component.css"
OP_CSS = FRONT + "platform-operator-f04.component.css"
MODAL_CSS = "frontend/src/app/ui/institutional-modal/institutional-modal.component.css"
FEEDBACK_CSS = "frontend/src/app/ui/feedback/operation-feedback.component.css"
TOKEN_CSS = "frontend/src/app/ui/institutional-system/institutional-tokens.css"
PRIMITIVES = (
    "navigation", "card", "field", "form", "table", "notice", "state",
    "action", "icon",
)
# Prevent changing the pinned input while silently keeping a green audit.
BLOBS = {
    F01_INVENTORY: "a1ff218baf489d69104145048f94e7ff9ee48f73",
    F06_INVENTORY: "1529df2257b75a9508f1d2276080bea31c9be7b3",
    F06_BUYER: "a70a8df4552f993425ce6a46e3fef5d1447a307f",
    F06_TEST: "adb978087b2f0fd7efb88b481ae4563830b9cf69",
    F06_AUDIT: "cedd507243755295ac10609f22a2e35f973d348b",
    TOKEN_CSS: "9d5e0aebe031b42c30d82df489edf9af9f87d67e",
    STYLES: "b8c5b7cda54fd12e9702e21debc71dee02fc2cf4",
    PORTAL_CSS: "53628553948ad46dc4d4a5dbe1c51377a817ea36",
    FLOW_CSS: "5284c235c4575287b546b9e7392def780598b13d",
    OP_CSS: "148a89f18e3b9e19d0f4baf4496b1daa284b3f73",
    MODAL_CSS: "2e9d42306d7099f109dddf79358c9d0fafaa551a",
    FEEDBACK_CSS: "9fed006c09b1fe1cd334c2aa4a5f91f358cf7ebe",
    FRONT + "identity-guest.component.html": "644b7481cc5de3f1616e4b27c352adcd4530165b",
    FRONT + "identity-recovery.component.html": "33d9ecadf917932095eeae4ec80901488b26d554",
    FRONT + "identity-mfa.component.html": "ac7a2f3d999dd95762ec33404ed0c57cffcc8ac0",
    FRONT + "identity-workspace.component.html": "f47ad3da0c11cd1047f311c000ecd09f1fd5c7ec",
    FRONT + "identity-portal.component.html": "92460fcec810059a1c05a09774e277a6ce144ba7",
    FRONT + "platform-identity-panel.component.html": "015a5a1ff71740bc789d28fb8aebad9d80a2ac68",
    FRONT + "platform-operator-cases.component.html": "58d681eea203b53d6ce6d423a99ee72f082bd722",
    FRONT + "platform-operator-sessions.component.html": "3e9cb7b7578d2606cab4a7db0c2e784e91c74161",
    FRONT + "platform-operator-profile.component.html": "9de6712207ea0a3e99651f84a0ebd69c01d99ae6",
    FRONT + "platform-operator-security.component.html": "6aa22817f2a7d65ba3113d73aaa05f50cff241ed",
    FRONT + "platform-operator-monitor.component.html": "313f8fbf1d8795bd85470b39aaf4ba27b6024cf6",
}


def require(ok: bool, why: str) -> None:
    if not ok:
        raise AssertionError("F02-65 NO-GO: " + why)


def git(*args: str) -> str:
    proc = subprocess.run(["git", *args], cwd=ROOT, capture_output=True,
                          text=True, encoding="utf-8", check=False)
    require(proc.returncode == 0,
            "git " + " ".join(args) + ": " + proc.stderr[-280:])
    return proc.stdout


def source(sha: str, path: str) -> str:
    require(git("rev-parse", sha + ":" + path).strip() == BLOBS[path],
            "pinned blob has changed: " + sha + ":" + path)
    return git("show", sha + ":" + path)


def rows(text: str) -> list[dict[str, str]]:
    return list(csv.DictReader(io.StringIO(text)))


def index(items: list[dict[str, str]], label: str) -> dict[str, dict[str, str]]:
    required = {f"C05-F01-{n:03d}" for n in range(1, 66)}
    ids = [r.get("id") for r in items]
    require(len(items) == 65 and len(set(ids)) == 65 and set(ids) == required,
            label + " must contain 65 exact IDs, no missing or duplicates")
    return {r["id"]: r for r in items}


def scoped_css(path: str, template: str) -> list[str]:
    paths = [STYLES]
    paths.append(PORTAL_CSS if path.endswith("identity-portal.component.html") else FLOW_CSS)
    if "platform-operator-" in path:
        paths.append(OP_CSS)
    if "<cs-institutional-modal" in template:
        paths.append(MODAL_CSS)
    if "<cs-operation-feedback" in template:
        paths.append(FEEDBACK_CSS)
    return paths


def direct_primitives(template: str) -> list[str]:
    return [key for key in PRIMITIVES
            if re.search(r"<cs-ui-" + key + r"\b", template)]


def css_statistics(styles: dict[str, str], path: str) -> tuple[int, int]:
    return (len(re.findall(r"#[0-9a-fA-F]{3,8}\b", styles[path])),
            len(re.findall(r"var\(--cs-[a-zA-Z0-9-]+", styles[path])))


def fixture_check(row: dict[str, str]) -> None:
    require(row["nota_final_0_10"] == "" and
            row["dictamen_comercial"] == "PENDIENTE",
            row["id"] + " UI final grade/approval cannot be fabricated")
    require(row["estado_uniformidad_visual"].startswith("NO_CERTIFICADO:"),
            row["id"] + " aesthetic review must remain pending")
    require(row["f02_tema_heredado_por_cuenta"] == "SI_ROOT_csInstitutionalTheme",
            row["id"] + " inherited theme cannot be reported absent")


def load():
    require(git("rev-parse", F06_SHA).strip() == F06_SHA,
            "F06 pinned SHA unavailable")
    require(git("rev-parse", F01_SHA).strip() == F01_SHA,
            "F01 pinned SHA unavailable")
    current = index(rows(source(F06_SHA, F06_INVENTORY)), "F06 source")
    historical = index(rows(source(F01_SHA, F01_INVENTORY)), "F01 historical source")
    preliminary = index(rows(source(F06_SHA, F06_BUYER)), "F06 preview scores")
    reconciled = index(rows(LEDGER.read_text(encoding="utf-8")), "F02 reconciliation")
    templates = {path: source(F06_SHA, path)
                 for path in sorted({r["fuente_angular"] for r in current.values()})}
    css = {path: source(F06_SHA, path)
           for path in (STYLES, PORTAL_CSS, FLOW_CSS, OP_CSS, MODAL_CSS, FEEDBACK_CSS)}
    return current, historical, preliminary, reconciled, templates, css


def audit_a() -> None:
    current, original, old_score, reconciliation, templates, _ = load()
    require(len(templates) == 11, "expected 11 exact source templates")
    require(Counter(x["tipo"] for x in current.values()) ==
            {"vista": 17, "estado": 18, "complemento": 3, "modal": 27},
            "65 surfaces type distribution drift")
    differences = []
    for sid, row in current.items():
        old, report = original[sid], reconciliation[sid]
        require(row["tipo"] == old["tipo"] == report["tipo"]
                and row["interfaz_o_estado"] == old["interfaz_o_estado"] == report["interfaz_o_estado"]
                and row["fuente_angular"] == old["componente_fuente"] == report["fuente_angular"],
                sid + " source identity changed")
        if old["ruta_y_precondicion"] != row["ruta_y_precondicion"]:
            differences.append(sid)
            require(sid == "C05-F01-057"
                    and row["ruta_y_precondicion"] == "/cuenta?vista=historial [empty]",
                    sid + " unexplained route difference")
        for key in ("id", "ruta_y_precondicion", "fuente_angular"):
            require(report[key] == row[key], sid + " wrong crosswalk " + key)
        require(report["sha_blob_fuente_F06"] == BLOBS[row["fuente_angular"]],
                sid + " exact source hash mismatch")
        require(report["sha_escenas_F06"] == F06_SHA,
                sid + " wrong evidence SHA")
        require(report["evidencia_65x5_20261122"] ==
                "45032597168:65x5;artifact:14758472442",
                sid + " evidence reference is not exact run/artifact")
        fixture_check(report)
    require(differences == ["C05-F01-057"], "historical route correction drift")
    print(json.dumps({"A": "PASS source-only", "ids": len(current),
                      "templates": len(templates), "route_exception": differences}))


def audit_b() -> None:
    current, _, _, reconciled, templates, _ = load()
    token_local = (ROOT / TOKEN_CSS).read_text(encoding="utf-8")
    require(git("hash-object", str(ROOT / TOKEN_CSS)).strip() == BLOBS[TOKEN_CSS],
            "F02 semantic tokens diverge from certified F06 snapshot")
    require(token_local == source(F06_SHA, TOKEN_CSS), "tokens drift across branches")
    theme = source(F06_SHA, FRONT + "identity-portal.component.html")
    require('[csInstitutionalTheme]="publishedTheme()"' in theme,
            "C05 root does not bind F02 validated institutional theme")
    for sid, row in current.items():
        cross = reconciled[sid]
        require(row["dictamen_compraria"] == "PENDIENTE" and not row["nota_comercial_0_10"],
                sid + " F06 current official grade should be pending")
        fixture_check(cross)
        require(cross["modal_f05_en_plantilla"] in ("SI_PLANTILLA", "NO") and
                cross["feedback_f06_en_plantilla"] in ("SI_PLANTILLA", "NO"),
                sid + " modality/feedback usage must be scoped to source, not claimed per state")
    test = source(F06_SHA, F06_TEST)
    auditor = source(F06_SHA, F06_AUDIT)
    for part in ("account-terms-current-loaded-", "account-historial-empty-",
                 "C05-F01-014", "C05-F01-057"):
        require(part in test and part in auditor,
                "semantic negative coverage removed: " + part)
    for marker in ('data.get("route") == "/cuenta?vista=historial"',
                   '"Condiciones qa-f01"', '"Sin actividad disponible"'):
        require(marker in auditor, "semantic evidence guard removed: " + marker)
    # Negative mutations prove that evidence scores/acceptances cannot be
    # introduced silently into our F02 crosswalk.
    sample = next(iter(reconciled.values()))
    for field, fake in (("nota_final_0_10", "9.5"),
                        ("dictamen_comercial", "COMPRARIA"),
                        ("estado_uniformidad_visual", "CERTIFICADO")):
        mutation = dict(sample)
        mutation[field] = fake
        try:
            fixture_check(mutation)
        except AssertionError:
            pass
        else:
            raise AssertionError("mutated " + field + " wrongly accepted")
    print(json.dumps({"B": "PASS presentation-and-privacy-scope-only",
                      "theme_applied": True, "false-positive-guards": ["014", "057"],
                      "negative_mutations_rejected": 3,
                      "backend_auth_tests_in_this_audit": False}))


def audit_c() -> None:
    current, _, buyer, reconciliation, templates, css = load()
    token_counts = {path: css_statistics(css, path) for path in css}
    require(sum(n[0] for n in token_counts.values()) == 377
            and sum(n[1] for n in token_counts.values()) == 93,
            "CSS literal/token evidence counts are stale")
    families = set()
    direct_count = 0
    for sid, row in current.items():
        source_path = row["fuente_angular"]
        template, record = templates[source_path], reconciliation[sid]
        comps = direct_primitives(template)
        families.update(comps)
        if comps:
            direct_count += 1
        require(record["selector_f02_directo_en_plantilla"] == ("|".join(comps) or "NINGUNO_DIRECTO"),
                sid + " F02 native primitive adoption misrepresented")
        require(record["modal_f05_en_plantilla"] ==
                ("SI_PLANTILLA" if "<cs-institutional-modal" in template else "NO"),
                sid + " modal composition misrepresented")
        require(record["feedback_f06_en_plantilla"] ==
                ("SI_PLANTILLA" if "<cs-operation-feedback" in template else "NO"),
                sid + " feedback composition misrepresented")
        paths = scoped_css(source_path, template)
        require(record["css_del_recorrido"] == "|".join(paths),
                sid + " component stylesheet dependencies drift")
        for col, index_no in (("css_hex_directos_por_fichero", 0),
                              ("css_tokens_cs_por_fichero", 1)):
            expected = "|".join(p.split("/")[-1] + ":" +
                                str(token_counts[p][index_no]) for p in paths)
            require(record[col] == expected,
                    sid + " CSS token/color debt provenance incorrect: " + col)
        require(record["nota_preliminar_20261122_0_10"] ==
                buyer[sid]["nota_exploratoria_0_10"]
                and record["hallazgo_preliminar"] == buyer[sid]["defecto_y_contexto"],
                sid + " provisional purchase observation altered")
        fixture_check(record)
    require(families == {"navigation", "card", "state"},
            "F02 direct component adoption has changed, reassess required")
    require(direct_count == 31, "31 sources mapped to direct component use changed")
    print(json.dumps({
        "C": "PASS evidence ledger; NON_COMPLIANCE design adoption remains",
        "65_surfaces_with_global_F02_tokens": 65,
        "direct_primitives_in_11_templates": sorted(families),
        "per_surface_sources_with_direct_primitives": direct_count,
        "per_surface_sources_without_direct_primitives": 65 - direct_count,
        "CSS_hex_literal_occurrences": 377,
        "CSS_token_references": 93,
        "official_ratings": 0,
        "commercial_status": "NO_GO_65_INDIVIDUAL_REVIEWS_PENDING",
        "visual_computed_styles_verified_here": False,
    }, ensure_ascii=False))


if __name__ == "__main__":
    try:
        require(len(sys.argv) == 2 and sys.argv[1] in ("A", "B", "C"),
                "usage: A|B|C")
        {"A": audit_a, "B": audit_b, "C": audit_c}[sys.argv[1]]()
    except (OSError, AssertionError, KeyError, UnicodeError, ValueError, csv.Error) as exc:
        print("FAIL:", exc, file=sys.stderr)
        raise SystemExit(1) from exc
