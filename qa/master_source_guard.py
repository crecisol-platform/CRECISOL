#!/usr/bin/env python3
"""CRECISOL: pin the CURRENT master, preserving byte-exact historical body.

Documentary-only gate. No private app, deployment, PSP or production accesses.
Historical SHA constants remain tied to C01/C02/C03 certification artifacts.
"""
from hashlib import sha1, sha256
from pathlib import Path
from zipfile import ZipFile
from xml.etree import ElementTree as ET
import re
import sys

ROOT = Path(__file__).resolve().parents[1]
WORD = ROOT / "docs/CRECISOL_DOCUMENTO_MAESTRO_UNICO.docx"
PDF = ROOT / "docs/CRECISOL_DOCUMENTO_MAESTRO_UNICO.pdf"
REFERENCE = ROOT / "docs/MASTER_REFERENCE.md"

HISTORICAL_WORD_SHA = "e45c2017e62bbbaa6853298efd5de5d785230005203d8791f791760f6f2c622e"
HISTORICAL_PDF_SHA = "17ffe48aab3b224cc9b11927c498e4f49c162d96bf639c894b84259f597738dd"
CURRENT_WORD_SHA = "ea5afc89e74915a9b18b9753ddb832f4be8e5d00f5e66b0f008e02e03476444c"
CURRENT_PDF_SHA = "0dfd3217439760540390707ea8196d6252464a501a7fe3e6eafdc49eda39da77"
CURRENT_WORD_BLOB = "fe7aaab60b603f1844e18666e2f99c0ea97e86e9"
CURRENT_PDF_BLOB = "93c8412b2450e85a882b622e7a012477c61c2359"
HISTORICAL_BODY_FINGERPRINT = "6238a81010d8bfd035eda77fa33897a009f4b363d4e281723c723b6b60c807d5"
HISTORICAL_BODY_ELEMENTS = 1876  # 1,687 paragraphs + 189 tables; original sectPr excluded
CURRENT_BODY_ELEMENTS = 1959   # preceding 1876 + appendix + terminal sectPr
HISTORICAL_LOGO_SHA = "9e83d1d0a891565cf80a21a7e90d7afcaa24c9bb63e7d2a589790127dadd549f"
W = "{http://schemas.openxmlformats.org/wordprocessingml/2006/main}"


def require(ok, why):
    if not ok:
        raise AssertionError(why)


def git_blob_sha(payload):
    return sha1(b"blob " + str(len(payload)).encode("ascii") + b"\x00" + payload).hexdigest()


def legacy_fingerprint(elements):
    h = sha256()
    for el in elements:
        h.update(ET.canonicalize(ET.tostring(el, encoding="unicode")).encode("utf-8"))
    return h.hexdigest()


def validate_document_xml(xml):
    body = ET.fromstring(xml).find(W + "body")
    require(body is not None, "Missing Word body")
    elements = list(body)
    require(len(elements) == CURRENT_BODY_ELEMENTS, "Word element count changed")
    require(elements[-1].tag == W + "sectPr", "Missing terminal section properties")
    prefix = elements[:HISTORICAL_BODY_ELEMENTS]
    require(sum(x.tag == W + "p" for x in prefix) == 1687, "Historical paragraph count changed")
    require(sum(x.tag == W + "tbl" for x in prefix) == 189, "Historical table count changed")
    require(legacy_fingerprint(prefix) == HISTORICAL_BODY_FINGERPRINT,
            "Original master body changed: C01/C02/C03 historical source must not drift")
    annex = elements[HISTORICAL_BODY_ELEMENTS:-1]
    text = " ".join(t.text or "" for el in annex for t in el.iter(W + "t"))
    for i in range(1, 15):
        require(f"COM-{i:03d}" in text, f"Missing requirement COM-{i:03d}")
    for i in range(1, 19):
        require(f"COM-T{i:02d}" in text, f"Missing COM-T{i:02d} proposed test")
    chapter_titles = []
    for para in elements:
        if para.tag != W + "p":
            continue
        ppr = para.find(W + "pPr")
        style = ppr.find(W + "pStyle") if ppr is not None else None
        if style is None or style.get(W + "val") != "Heading1":
            continue
        title = "".join(n.text or "" for n in para.iter(W + "t"))
        if re.match(r"^\d\d\. ", title):
            chapter_titles.append(title)
    require(len(chapter_titles) == 37, "Master 37 chapter headings changed")


def canonical_active_word(historical_sha=HISTORICAL_WORD_SHA):
    require(historical_sha == HISTORICAL_WORD_SHA, "Historic certification SHA pointer altered")
    word_bytes, pdf_bytes = WORD.read_bytes(), PDF.read_bytes()
    require(sha256(word_bytes).hexdigest() == CURRENT_WORD_SHA,
            "Current canonical Word differs from approved commercial edition")
    require(sha256(pdf_bytes).hexdigest() == CURRENT_PDF_SHA,
            "Current derived PDF differs from current Word edition")
    require(git_blob_sha(word_bytes) == CURRENT_WORD_BLOB and
            git_blob_sha(pdf_bytes) == CURRENT_PDF_BLOB,
            "Canonical Git blob fingerprint mismatch")
    master = REFERENCE.read_text(encoding="utf-8")
    for pin in (HISTORICAL_WORD_SHA, HISTORICAL_PDF_SHA,
                CURRENT_WORD_SHA, CURRENT_PDF_SHA, CURRENT_WORD_BLOB, CURRENT_PDF_BLOB,
                HISTORICAL_BODY_FINGERPRINT):
        require(pin in master, "MASTER_REFERENCE missing historical/current pin: " + pin)
    with ZipFile(WORD) as archive:
        require(archive.testzip() is None, "Corrupt canonical DOCX ZIP")
        image = archive.read("word/media/image1.png")
        require(sha256(image).hexdigest() == HISTORICAL_LOGO_SHA,
                "Official CRECISOL logo changed")
        validate_document_xml(archive.read("word/document.xml"))
    return True


def self_test():
    canonical_active_word()
    require(git_blob_sha(b"test") != CURRENT_WORD_BLOB,
            "Mutated blob incorrectly accepted")
    with ZipFile(WORD) as archive:
        root = ET.fromstring(archive.read("word/document.xml"))
    body = root.find(W + "body")
    original = body[25]
    oldhash = legacy_fingerprint(list(body)[:HISTORICAL_BODY_ELEMENTS])
    textnode = next((n for n in original.iter(W + "t") if n.text), None)
    require(textnode is not None, "Historical mutation test lacks text")
    old = textnode.text
    textnode.text = (old or "") + "MUTATED"
    require(legacy_fingerprint(list(body)[:HISTORICAL_BODY_ELEMENTS]) != oldhash,
            "Historical body mutation was not detected")
    textnode.text = old
    require(legacy_fingerprint(list(body)[:HISTORICAL_BODY_ELEMENTS]) == oldhash,
            "Historical body could not be restored in memory")
    try:
        validate_document_xml(ET.tostring(root, encoding="utf-8").replace(b"COM-T18", b"COM-TXX"))
    except AssertionError:
        pass
    else:
        raise AssertionError("Missing test ID mutation not rejected")
    print("PASS master guard negatives: historical mutation, missing test and Git blob forging rejected")


if __name__ == "__main__":
    canonical_active_word()
    print("PASS current CRECISOL master: Word/PDF SHA-256 + blob; original 1,876 body elements unchanged")
    if "--self-test" in sys.argv:
        self_test()
