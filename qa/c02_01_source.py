#!/usr/bin/env python3
"""C02-01: exact canonical Word extraction + auditable inventory-only gates A/B/C.

Checks requirements documentation, NOT private authentication or live tenant isolation.
No external dependencies, network, production access, or new application permissions.
"""
from collections import Counter
from hashlib import sha256
from pathlib import Path
from zipfile import ZipFile
import re
import sys
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[1]
WORD = ROOT / "docs/CRECISOL_DOCUMENTO_MAESTRO_UNICO.docx"
REFERENCE = ROOT / "docs/MASTER_REFERENCE.md"
SPEC = ROOT / "docs/c02/C02_01_ESPECIFICACION.md"
PLAN = ROOT / "docs/C02_PREPARACION.md"
EXPECTED_SHA = "e45c2017e62bbbaa6853298efd5de5d785230005203d8791f791760f6f2c622e"
W = "{http://schemas.openxmlformats.org/wordprocessingml/2006/main}"


def require(value, message):
    if not value:
        raise AssertionError(message)


def text_of(node):
    return "".join(el.text or "" for el in node.iter(W + "t")).strip()


def style_of(node):
    props = node.find(W + "pPr")
    style = props.find(W + "pStyle") if props is not None else None
    return style.get(W + "val") if style is not None else "Normal"


def chapter_items():
    raw = WORD.read_bytes()
    require(sha256(raw).hexdigest() == EXPECTED_SHA, "Canonical Word SHA mismatch")
    require(EXPECTED_SHA in REFERENCE.read_text(encoding="utf-8"),
            "Master reference SHA mismatch")
    with ZipFile(WORD) as archive:
        body = ET.fromstring(archive.read("word/document.xml")).find(W + "body")
    require(body is not None, "Word document body missing")
    started = False
    paragraphs, tables = [], []
    first, last = "", ""
    for el in body:
        if el.tag == W + "p":
            txt, sty = text_of(el), style_of(el)
            if sty == "Heading1" and txt.startswith("02. "):
                require(not started, "Duplicate chapter 02 heading")
                first, started = txt, True
                continue
            if started and sty == "Heading1" and txt.startswith("03. "):
                last = txt
                break
            if started and txt:
                paragraphs.append((sty, txt))
        elif el.tag == W + "tbl" and started:
            tables.append([[text_of(cell) for cell in row.findall(W + "tc")]
                           for row in el.findall(W + "tr")])
    require(first == "02. Personas, experiencias y arquitectura de producto",
            "Chapter 02 title changed")
    require(last == "03. Arquitectura técnica limpia y dependencias",
            "Chapter 03 boundary changed")
    require(paragraphs, "C02 has no paragraphs")
    return first, last, paragraphs, tables


def original_parts():
    _, _, paragraphs, tables = chapter_items()
    boundary = next((idx for idx, (_, text) in enumerate(paragraphs)
                     if text == "Contrato de implementación y certificación del capítulo"), -1)
    require(boundary > 0, "Original chapter vs appendix boundary not found")
    original = [txt for style, txt in paragraphs[:boundary]
                if style != "OriginalNote" and not txt.startswith("PRECEDENCIA NORMATIVA")]
    require(len(original) == 11,
            f"Expected 11 original paragraphs, got {len(original)}")
    require(len(tables) == 3 and [len(t) for t in tables] == [7, 3, 3],
            "Expected source table (7 rows) and two 3-row supplementary tables")
    require(all(len(row) == 3 for row in tables[0]), "Original source table not 3 columns")
    require(all(text for row in tables[0] for text in row), "Blank source table field")
    anchors = (
        "CRECISOL expone seis experiencias", "2.1. Flujo", "El visitante accede",
        "El servidor resuelve", "El visitante navega", "Al reservar",
        "La acción se persiste", "El negocio ve", "La operación alimenta",
        "2.2. Requisitos de UX", "Botones de acción"
    )
    for i, (paragraph, anchor) in enumerate(zip(original, anchors), 1):
        require(anchor.casefold() in paragraph.casefold(),
                f"C02 original paragraph P{i:02d} shifted: {anchor}")
    roles = ("Visitante del SaaS", "SuperAdmin de plataforma", "Propietario de negocio",
             "Empleado", "Cliente final", "Centro de demostración")
    require(tables[0][0][0] == "Capacidad / regla", "C02 source header changed")
    for i, role in enumerate(roles, 1):
        require(tables[0][i][0] == role,
                f"C02 role/table T01-R{i:02d} mismatch")
    appendix = " ".join(text for _, text in paragraphs[boundary:])
    precedence = " ".join(text for _, text in paragraphs[:boundary])
    require("PRECEDENCIA NORMATIVA" in precedence and "sin selector" in precedence,
            "Automatic contextual login precedence missing")
    for marker in ("C02-01 Extraer", "C02-05 Auditar A", "C02-06 Auditar B",
                   "C02-07 Auditar C", "C02-08 Acta", "TEN-001", "TEN-032",
                   "UC-036", "UC-040", "G.7", "seis experiencias"):
        require(marker.casefold() in appendix.casefold(),
                f"Canonical C02 annex marker missing: {marker}")
    return original, tables[0], paragraphs, tables


def matrix_ids(spec):
    return re.findall(r"^\|\s*(P\d{2}|T01-R\d{2})\s*\|", spec, re.M)


def valid_ids(ids):
    expected = {f"P{i:02d}" for i in range(1, 12)}
    expected |= {f"T01-R{i:02d}" for i in range(1, 7)}
    return len(ids) == len(expected) and Counter(ids) == Counter(expected)


def audit_a():
    original, table, _, _ = original_parts()
    spec = SPEC.read_text(encoding="utf-8")
    ids = matrix_ids(spec)
    require(valid_ids(ids), "Missing/duplicated C02 source ID in inventory")
    for role in (row[0] for row in table[1:]):
        require(role in spec, f"Source persona absent from matrix: {role}")
    for marker in ("DIF-C02-01", "MAE-C02-01", "MAE-C02-08", EXPECTED_SHA,
                   "11 párrafos", "T01-R06", "P11"):
        require(marker in spec, f"Canonical source mapping not documented: {marker}")
    require(not valid_ids(ids[:-1]), "Negative: missing persona row undetected")
    require(not valid_ids(ids + [ids[0]]), "Negative: duplicate source row undetected")
    require(len(original) == 11 and len(table) == 7, "Source count regression")
    print("AUDIT A PASS C02-01: SHA-256 pinned, 11 original paragraphs,")
    print("  six original persona/context rows, C02/C03 boundary, supplemental gates,")
    print("  17 source IDs, mutation tests for missing/duplicate coverage.")


def audit_b():
    original_parts()
    spec = SPEC.read_text(encoding="utf-8")
    for marker in ("NO selector", "MFA", "sucursal", "propiedad", "identidad",
                   "tenant", "SuperAdmin", "cobros reales", "sintéticos",
                   "X-Tenant-Id", "URL", "G.7", "NO EJECUTADAS"):
        require(marker.casefold() in spec.casefold(),
                f"Security boundary not inventoried: {marker}")
    for i in range(1, 11):
        require(re.search(r"^\| NEG-" + f"{i:02d}" + r" \|", spec, re.M),
                f"Missing adversarial scenario NEG-{i:02d}")
    require("C02-01" in spec and "NO CERTIFICADO" in spec,
            "Inventory must not claim chapter security certification")
    require("backend" in spec and "no" in spec.casefold(),
            "Backend authorization boundaries not specified")
    # Negative controls verify absence/duplication is detected, NOT auth runtime behavior.
    require(not valid_ids(["P01"] * 11 + ["T01-R01"] * 6),
            "Negative: collapsed source personas accepted")
    for p in (SPEC, PLAN):
        require(not re.search(r"(?i)\b(?:password|pwd|api[_-]?key|secret)\s*[:=]\s*[^\s|]+",
                              p.read_text(encoding="utf-8")),
                f"Possible credential assignment in {p.name}")
    print("AUDIT B PASS C02-01: documentary risk/tenant/persona boundaries,")
    print("  ten future negative scenarios and no hardcoded credential assignment.")
    print("LIMIT: No runtime MFA/IDOR/BOLA/API/tenant security tests in this documentation-only microphase.")


def audit_c():
    original_parts()
    spec = SPEC.read_text(encoding="utf-8")
    plan = PLAN.read_text(encoding="utf-8")
    require("1/37" in plan and "C03 bloqueado" in plan,
            "Chapter gate/sequence not correctly represented")
    acta_path = ROOT / "docs/c02/C02_01_ACTA_MICROFASE.md"
    if acta_path.exists():
        acta = acta_path.read_text(encoding="utf-8")
        require("C02" in plan and "NO CERTIFICADO" in plan,
                "C02 must remain uncertified after any technically closed microphase")
        next_acta_path = ROOT / "docs/c02/C02_02_ACTA_MICROFASE.md"
        if next_acta_path.exists():
            next_acta = next_acta_path.read_text(encoding="utf-8")
            third_path = ROOT / "docs/c02/C02_03_ACTA_MICROFASE.md"
            if third_path.exists():
                third = third_path.read_text(encoding="utf-8")
                fourth_path = ROOT / "docs/c02/C02_04_ACTA_MICROFASE.md"
                if fourth_path.exists():
                    fourth = fourth_path.read_text(encoding="utf-8")
                    require("4/9" in plan and
                            "C02-04 CERRADA TÉCNICAMENTE" in fourth and
                            "ecb7807e79427d5059c8b5f4ee0c5dd2195b7cb5" in fourth and
                            "42630487933" in fourth,
                            "C02-04 advancement requires exact technical SHA and CI")
                else:
                    require("3/9" in plan,
                            "C02-03 closure remains 3/9 until C02-04 acta")
                require("C02-03 CERRADA TÉCNICAMENTE" in third and
                        "6ccd98960f36dce3407392fd26406366b4cd0650" in third and
                        "42624454291" in third,
                        "C02-03 provenance requires SHA and postmerge evidence")
            else:
                require("2/9" in plan,
                        "C02-02 closure must remain 2/9 until C02-03 acta")
            require("C02-02" in plan and
                    "C02-02 CERRADA TÉCNICAMENTE" in next_acta and
                    "455987893827cb5b25d4b127457efd81225267b7" in next_acta and
                    "42619923854" in next_acta,
                    "C02-02 provenance requires technical SHA and real CI")
        else:
            require("1/9" in plan,
                    "Closed C02-01 must retain 1/9 until following acta")
        require("CERRADA TÉCNICAMENTE" in acta and
                "f40ab1ec47731eddd4b55a4f28a311b3800e68f4" in acta and
                "42616163656" in acta and "42616163666" in acta,
                "Acta requires true postmerge SHA and successful workflow references")
    else:
        require("0/9" in plan and "NO CERTIFICADO" in plan,
                "Open C02-01 cannot claim microphase closure")
    for i in range(1, 10):
        require(re.search(r"^\| C02-" + f"{i:02d}" + r" \|", plan, re.M),
                f"Missing sequential phase C02-{i:02d}")
    for i in range(1, 7):
        require(re.search(r"^\| E" + f"{i:02d}" + r" ", spec, re.M),
                f"Missing canonical experience E{i:02d}")
    for marker in ("frontend/", "backend/", "database/", "WCAG 2.2 AA",
                   "movimiento reducido", "C02-02", "C02-09", "C03",
                   "sin cambios", "rollback"):
        # Explicit test for source/requirements; docs may use capitalized labels.
        require(marker.casefold() in spec.casefold() or marker.casefold() in plan.casefold(),
                f"Missing architecture/UX/dependency gate: {marker}")
    # No artifact links to missing local files; checked on documentation introduced in C02-01.
    for path in (SPEC, PLAN):
        for target in re.findall(r"\[[^\]]+\]\(([^)]+)\)", path.read_text(encoding="utf-8")):
            if target.startswith(("https://", "http://", "mailto:", "#")):
                continue
            resolved = (path.parent / target.split("#", 1)[0]).resolve()
            require(resolved.is_relative_to(ROOT) and resolved.is_file(),
                    f"Broken or escaping relative link: {target}")
    print("AUDIT C PASS C02-01: six experiences vs five actor contexts + demo,")
    print("  ordered phases C02-01..09, front/backend/database boundaries,")
    print("  future WCAG/motion requirements and local documentation links.")
    print("LIMIT: no new UI and no performance benchmark asserted by a documentation inventory.")


def preview():
    first, last, paragraphs, tables = chapter_items()
    print(f"CANONICAL_C02_START={first}")
    print(f"CANONICAL_C02_END={last}")
    print(f"SOURCE_COUNTS=paragraphs:{len(paragraphs)},tables:{len(tables)},rows:{sum(len(t) for t in tables)}")
    for i, (style, text) in enumerate(paragraphs, 1):
        print(f"P{i:03d} [{style}] {text}")
    for t, rows in enumerate(tables, 1):
        for r, cells in enumerate(rows, 1):
            print(f"T{t:02d}-R{r:02d} | " + " | ".join(cells))
    print("SOURCE_PREVIEW_COMPLETED: validated exact Word SHA and chapter boundary; no software certification")


def main():
    require(len(sys.argv) == 2 and sys.argv[1] in ("--preview", "A", "B", "C"),
            "Use --preview, A, B or C")
    if sys.argv[1] == "--preview":
        preview()
    else:
        {"A": audit_a, "B": audit_b, "C": audit_c}[sys.argv[1]]()


if __name__ == "__main__":
    try:
        main()
    except (AssertionError, OSError, KeyError, ValueError) as error:
        print(f"FAIL C02-01: {error}", file=sys.stderr)
        sys.exit(1)
