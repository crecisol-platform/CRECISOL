#!/usr/bin/env python3
"""Extract the canonical C02 chapter from CRECISOL's pinned Word, read-only.

This is source discovery, not an acceptance or security certification.
No network, production connection, deployment, or dependency installation.
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


def text_of(node):
    return "".join(el.text or "" for el in node.iter(W + "t")).strip()


def style_of(node):
    props = node.find(W + "pPr")
    style = props.find(W + "pStyle") if props is not None else None
    return style.get(W + "val") if style is not None else "Normal"


def chapter_items():
    raw = WORD.read_bytes()
    assert sha256(raw).hexdigest() == EXPECTED_SHA, "Canonical Word SHA mismatch"
    assert EXPECTED_SHA in REFERENCE.read_text(encoding="utf-8"), "Master reference SHA mismatch"
    with ZipFile(WORD) as archive:
        body = ET.fromstring(archive.read("word/document.xml")).find(W + "body")
    assert body is not None, "Word document body missing"
    started = False
    paragraphs, tables = [], []
    first, last = "", ""
    for el in body:
        if el.tag == W + "p":
            txt, sty = text_of(el), style_of(el)
            if sty == "Heading1" and re.match(r"^02\.\s", txt):
                assert not started, "Duplicate chapter 02 heading"
                first = txt
                started = True
                continue
            if started and sty == "Heading1" and re.match(r"^03\.\s", txt):
                last = txt
                break
            if started and txt:
                paragraphs.append((sty, txt))
        elif el.tag == W + "tbl" and started:
            rows = [[text_of(cell) for cell in row.findall(W + "tc")]
                    for row in el.findall(W + "tr")]
            tables.append(rows)
    assert started and first.startswith("02."), "Chapter 02 not found"
    assert last.startswith("03."), "Chapter 03 boundary not found"
    assert paragraphs, "C02 has no source paragraphs"
    return first, last, paragraphs, tables


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


if __name__ == "__main__":
    if len(sys.argv) == 2 and sys.argv[1] == "--preview":
        preview()
    else:
        raise SystemExit("Usage: python3 qa/c02_01_source.py --preview")
