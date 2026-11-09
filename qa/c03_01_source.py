#!/usr/bin/env python3
"""CRECISOL C03-01: read the exact approved Word chapter, without changing it.

Preview-only bootstrap. A/B/C audit gates and the exact traced inventory are
added after the original C03 section has been extracted from the checked-out
canonical DOCX, not guessed from a provisional plan.
"""
from hashlib import sha256
from pathlib import Path
from zipfile import ZipFile
import re
import sys
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[1]
WORD = ROOT / "docs/CRECISOL_DOCUMENTO_MAESTRO_UNICO.docx"
REFERENCE = ROOT / "docs/MASTER_REFERENCE.md"
EXPECTED_SHA = "e45c2017e62bbbaa6853298efd5de5d785230005203d8791f791760f6f2c622e"
W = "{http://schemas.openxmlformats.org/wordprocessingml/2006/main}"


def require(ok, why):
    if not ok:
        raise AssertionError(why)


def text_of(node):
    return "".join(el.text or "" for el in node.iter(W + "t")).strip()


def style_of(node):
    ppr = node.find(W + "pPr")
    st = ppr.find(W + "pStyle") if ppr is not None else None
    return st.get(W + "val") if st is not None else "Normal"


def chapter_items():
    blob = WORD.read_bytes()
    require(sha256(blob).hexdigest() == EXPECTED_SHA,
            "Canonical C03 Word SHA-256 mismatch")
    require(EXPECTED_SHA in REFERENCE.read_text(encoding="utf-8"),
            "Canonical SHA not pinned in MASTER_REFERENCE")
    with ZipFile(WORD) as doc:
        root = ET.fromstring(doc.read("word/document.xml"))
    body = root.find(W + "body")
    require(body is not None, "DOCX body missing")
    paragraphs, tables = [], []
    beginning = ending = ""
    active = False
    for node in body:
        if node.tag == W + "p":
            label, style = text_of(node), style_of(node)
            if style == "Heading1" and re.match(r"^03\.\s", label):
                require(not active and not beginning,
                        "Duplicated chapter 03 heading")
                beginning, active = label, True
                continue
            if active and style == "Heading1" and re.match(r"^04\.\s", label):
                ending = label
                break
            if active and label:
                paragraphs.append((style, label))
        elif node.tag == W + "tbl" and active:
            tables.append([[text_of(cell) for cell in row.findall(W+"tc")]
                           for row in node.findall(W+"tr")])
    require(beginning == "03. Arquitectura técnica limpia y dependencias",
            "Unexpected canonical C03 title")
    require(ending.startswith("04. "), "Unexpected end of C03 / C04")
    require(len(paragraphs) >= 3, "Canonical C03 text missing")
    require(all(len(table)>0 and all(row for row in table) for table in tables),
            "Empty C03 table")
    return beginning, ending, paragraphs, tables


def preview():
    beginning, ending, ps, tables = chapter_items()
    print("CANONICAL_C03_START=" + beginning)
    print("CANONICAL_C03_END=" + ending)
    print(f"C03_SOURCE_COUNTS paragraphs={len(ps)} tables={len(tables)} "
          f"rows={sum(len(tab) for tab in tables)}")
    for i, (style, line) in enumerate(ps, 1):
        print(f"C03-P{i:03d} [{style}] " + line[:1400])
    for i, table in enumerate(tables, 1):
        for j, row in enumerate(table, 1):
            print(f"C03-T{i:02d}-R{j:02d} " +
                  " | ".join(c[:600] for c in row))
    print("C03_CANONICAL_PREVIEW_SUCCESS: SHA and C03/C04 boundaries verified. "
          "Not an implementation or certification.")


if __name__ == "__main__":
    try:
        require(len(sys.argv)==2 and sys.argv[1]=="--preview",
                "Usage: python3 qa/c03_01_source.py --preview")
        preview()
    except (AssertionError, KeyError, OSError, ValueError, TypeError) as e:
        print("C03_SOURCE_FAILED: " + str(e), file=sys.stderr)
        sys.exit(1)
