#!/usr/bin/env python3
"""Pre-audits A/B/C for PR73's commercial master, not software certification.

No deployments, payment providers or external services. Works on stdlib only.
"""
import re
import sys
from pathlib import Path
from zipfile import ZipFile
from xml.etree import ElementTree as ET

from master_source_guard import canonical_active_word, CURRENT_WORD_SHA, CURRENT_PDF_SHA

ROOT = Path(__file__).resolve().parents[1]
WORD = ROOT / "docs/CRECISOL_DOCUMENTO_MAESTRO_UNICO.docx"
PDF = ROOT / "docs/CRECISOL_DOCUMENTO_MAESTRO_UNICO.pdf"
W = "{http://schemas.openxmlformats.org/wordprocessingml/2006/main}"
CHAPTERS = (4,5,6,7,8,9,12,15,17,18,19,20,21,23,25,26,27,29,30,31,33,34,36,37)
SAFETY = (
    "No habilitar dominio personalizado sin propiedad",
    "No impedir presentar la solicitud por deudas",
    "sin borrar datos automáticamente",
    "No marcar PAGADO sin conciliación",
    "Sin integración técnica con SOLQARYN",
    "SuperAdmin no obtiene acceso privado de otro tenant sin grant excepcional",
    "No se mezclan entidades financieras",
    "no duplicar factura, renovación, suspensión ni movimiento por webhook repetido",
)


def require(cond, message):
    if not cond:
        raise AssertionError(message)


def get_doc():
    with ZipFile(WORD) as z:
        body = ET.fromstring(z.read("word/document.xml")).find(W + "body")
    return list(body)


def full_annex(elements):
    return " ".join(t.text or "" for el in elements[1876:-1] for t in el.iter(W + "t"))


def safety_contract(text):
    return all(term in text for term in SAFETY)


def audit_a():
    canonical_active_word()
    plan = (ROOT / "docs/PLAN_37_CAPITULOS.md").read_text(encoding="utf-8")
    for i in range(1, 38):
        require(f"C{i:02d} |" in plan, f"Chapter C{i:02d} vanished")
    for chapter in CHAPTERS:
        path = ROOT / f"docs/planificacion-futura/C{chapter:02d}_MICROFASES_PROPUESTAS.md"
        require(path.is_file(), "Missing proposed microphases: " + str(path))
        src = path.read_text(encoding="utf-8")
        require("Ampliación comercial COM-001..COM-014" in src,
                f"Commercial dependency missing in C{chapter:02d}")
        require(re.search(r"NO IMPLEMENTAD[OA]", src, re.IGNORECASE) and
                re.search(r"NO CERTIFICAD[OA]", src, re.IGNORECASE),
                f"Unapproved implementation/certification implication in C{chapter:02d}")
    for path, marker in (
        ("docs/DECISIONES_ABIERTAS.md", "ADR-014"),
        ("docs/ARQUITECTURA.md", "Contratos comerciales SaaS"),
        ("docs/SEGURIDAD_MULTITENANT.md", "Controles P0 comerciales propuestos"),
        ("docs/PLAN_MICROFASES_37_CAPITULOS_PROPUESTO.md", "Impacto transversal del modelo comercial"),
        ("docs/ANALISIS_IMPACTO_MODELO_COMERCIAL_2026-11-10.md", "COM-001"),
    ):
        require(marker in (ROOT / path).read_text(encoding="utf-8"),
                "Missing linked technical trace: " + path)
    print("PASS COM A: current master, historical prefix, 37 chapters, 24 microphase notes and control documents")


def audit_b():
    canonical_active_word()
    content = full_annex(get_doc())
    require(safety_contract(content),
            "Commercial annex is missing mandatory legal/payment/tenant/advertising safety requirements")
    for safety in SAFETY:
        require(not safety_contract(content.replace(safety, "MUTATED_UNSAFE_POLICY")),
                "Security mutation escaped: " + safety)
    require("COM-008 | Cancelación y no renovación" in content,
            "Cancellation and nonrenewal not separated")
    require("COM-005 | Pago anticipado" in content,
            "Payment advance not present")
    print("PASS COM B: 8 normative security controls and 8 hostile omissions rejected; NO RUNTIME AUTH CLAIM")


def audit_c():
    canonical_active_word()
    elements = get_doc()
    tables = [e for e in elements if e.tag == W + "tbl"]
    require(len(tables) == 194, "Commercial edition must have 189 original + 5 new tables")
    mapping = tables[-2].findall(W + "tr")
    chapters = []
    for row in mapping[1:]:
        cells = row.findall(W + "tc")
        chapters.append("".join(t.text or "" for t in cells[0].iter(W + "t")))
    require(chapters == [f"C{i:02d}" for i in range(1,38)],
            "Full 37 chapter appendix mapping not preserved")
    tests = ["".join(t.text or "" for t in row.findall(W+"tc")[0].iter(W+"t"))
             for row in tables[-1].findall(W+"tr")[1:]]
    require(tests == [f"COM-T{i:02d}" for i in range(1,19)],
            "18 new acceptance tests not preserved exactly")
    pdf = PDF.read_bytes()
    require(len(re.findall(rb"/Type\s*/Page\b",pdf)) == 237,
            "Derived PDF does not have 237 pages")
    master=(ROOT / "docs/MASTER_REFERENCE.md").read_text(encoding="utf-8")
    require(CURRENT_WORD_SHA in master and CURRENT_PDF_SHA in master,
            "Master reference does not record exact approved current edition")
    print("PASS COM C: 37 chapter mapping, 18 tests, 194 tables, 237-page pinned PDF; visual review is separate")


def main():
    if len(sys.argv) != 2 or sys.argv[1] not in ("A","B","C"):
        raise SystemExit("Usage: python3 qa/commercial_master_audits.py A|B|C")
    {"A":audit_a,"B":audit_b,"C":audit_c}[sys.argv[1]]()

if __name__ == "__main__":
    main()
