#!/usr/bin/env python3
"""C05-01: read the canonical Word C05 chapter; preparation, not a runtime audit."""
from __future__ import annotations

import hashlib
import re
import sys
import unicodedata
import xml.etree.ElementTree as ET
import zipfile
from pathlib import Path

from master_source_guard import canonical_active_word

ROOT = Path(__file__).resolve().parents[1]
WORD = ROOT / "docs/CRECISOL_DOCUMENTO_MAESTRO_UNICO.docx"
W = "{http://schemas.openxmlformats.org/wordprocessingml/2006/main}"


def require(ok: bool, message: str) -> None:
    if not ok:
        raise AssertionError(message)


def fold(value: str) -> str:
    return "".join(ch for ch in unicodedata.normalize("NFKD", value.casefold())
                   if not unicodedata.combining(ch))


def canonical_c05_source() -> tuple[str, list[str], int, int]:
    require(canonical_active_word(), "Canonical Word/PDF integrity failed")
    with zipfile.ZipFile(WORD) as z:
        require(z.testzip() is None, "Word ZIP corrupted")
        doc = ET.fromstring(z.read("word/document.xml"))
    paras = []
    for p in doc.findall(".//" + W + "p"):
        value = "".join(t.text or "" for t in p.iter(W + "t")).strip()
        if value:
            paras.append(value)
    c05 = [i for i, value in enumerate(paras)
           if re.search(r"^capitulo\s*0?5\b", fold(value))]
    c06 = [i for i, value in enumerate(paras)
           if re.search(r"^capitulo\s*0?6\b", fold(value))]
    require(bool(c05) and bool(c06), "C05/C06 chapter headings absent")
    candidates = []
    for start in c05:
        stop = next((i for i in c06 if i > start), None)
        if stop is not None:
            segment = paras[start:stop]
            body = "\n".join(segment)
            candidates.append((len(body), start, stop, segment, body))
    require(bool(candidates), "C05/C06 chapter order invalid")
    chars, start, stop, segment, body = max(candidates)
    require(chars > 1000, "C05 section too short; possible TOC")
    folded = fold(body)
    require("identidad" in folded and ("autentic" in folded or "cuenta" in folded),
            "C05 chapter appears unrelated to identity")
    return hashlib.sha256(body.encode("utf-8")).hexdigest(), segment, start, stop


def source() -> None:
    digest, segments, start, stop = canonical_c05_source()
    body = "\n".join(segments)
    print(f"C05_SOURCE_SHA256={digest}")
    print(f"C05_SOURCE_RANGE={start}:{stop}")
    print(f"C05_SOURCE_PARAGRAPHS={len(segments)} CHARS={len(body)}")
    for index, line in enumerate(segments):
        print(f"C05_SOURCE_LINE_{index+1:02d}: {line[:500]}")
    print("LIMIT: this prints source evidence, not authentication implementation or certification")


if __name__ == "__main__":
    try:
        require(sys.argv[1:] == ["source"], "Usage: c05_01_preparation_audits.py source")
        source()
    except (OSError, AssertionError, ValueError, zipfile.BadZipFile, ET.ParseError) as e:
        print("FAIL C05-01 source preparation:", e, file=sys.stderr)
        sys.exit(1)
