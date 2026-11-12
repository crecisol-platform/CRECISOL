#!/usr/bin/env python3
"""Read-only, reproducible C04 Word source inventory; does not change the master."""
import hashlib
import re
import sys
import unicodedata
import xml.etree.ElementTree as ET
from zipfile import ZipFile
from c04_01_preparation_audits import ROOT, WORD, canonical_active_word

N = "{http://schemas.openxmlformats.org/wordprocessingml/2006/main}"
EXPECTED = "95be2bb0210e108317e67bd08fb9b2899d33554e684bb2d9ae3c8eced6995058"

def fold(v: str) -> str:
    return "".join(x for x in unicodedata.normalize("NFKD", v.casefold())
                   if not unicodedata.combining(x))

def paragraphs() -> list[str]:
    with ZipFile(WORD) as z:
        doc = ET.fromstring(z.read("word/document.xml"))
    return ["".join((t.text or "") for t in p.iter(N+"t")).strip()
            for p in doc.findall(".//"+N+"p")
            if "".join((t.text or "") for t in p.iter(N+"t")).strip()]

def chapter4() -> list[tuple[int, str]]:
    all_p = paragraphs()
    starts = [i for i,p in enumerate(all_p) if re.search(r"^capitulo\s*0?4\b",fold(p))]
    ends = [i for i,p in enumerate(all_p) if re.search(r"^capitulo\s*0?5\b",fold(p))]
    candidates = [(e-s,s,e) for s in starts for e in ends if e>s]
    if not candidates:
        raise AssertionError("Master does not expose C04/C05 boundaries")
    _, s, e = max(candidates)
    body = "\n".join(all_p[s:e])
    actual = hashlib.sha256(body.encode("utf-8")).hexdigest()
    if actual != EXPECTED:
        raise AssertionError("Canonical C04 normative text diverged: " + actual)
    return list(enumerate(all_p[s:e],start=s))

if __name__ == "__main__":
    if not canonical_active_word():
        raise SystemExit("FAIL: Word and PDF pinned hashes do not match")
    rows = chapter4()
    print("C04 SOURCE VERIFIED sha256="+EXPECTED+" paragraphs="+str(len(rows)))
    if len(sys.argv)==2 and sys.argv[1]=="inventory":
        for i,text in rows:
            # This is the original project-owned master, not invented paraphrase.
            print("MAE_C04_SOURCE["+str(i)+"] "+text.replace("\n"," "))
    elif len(sys.argv)==1 or sys.argv[1:] == ["verify"]:
        print("PASS C04-01 normative Word source integrity")
    else:
        raise SystemExit("Usage: c04_01_normative_inventory.py [verify|inventory]")
