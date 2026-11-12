#!/usr/bin/env python3
"""C04-01 preparatory-only gates. No tenant runtime, data or public APIs are created.

Audit A = source identity, Word chapter discovery and requirements traceability.
Audit B = document contract and adversarial falsification (NOT runtime penetration).
Audit C = same-run proof identity and architecture boundary.
"""
from __future__ import annotations

from pathlib import Path
import hashlib
import os
import re
import sys
import unicodedata
import xml.etree.ElementTree as ET
import zipfile

from master_source_guard import canonical_active_word, CURRENT_WORD_SHA, CURRENT_PDF_SHA
from c03_01_source import actual_graph

ROOT = Path(__file__).resolve().parents[1]
WORD = ROOT / "docs/CRECISOL_DOCUMENTO_MAESTRO_UNICO.docx"
SPEC = ROOT / "docs/c04/C04_01_ESPECIFICACION.md"
PREP = ROOT / "docs/C04_PREPARACION.md"
PLAN = ROOT / "docs/planificacion-futura/C04_MICROFASES_PROPUESTAS.md"
PROOF = ROOT / ".c04-01-prep-proof"
NS = "{http://schemas.openxmlformats.org/wordprocessingml/2006/main}"

REQUIRED = (
    "NO IMPLEMENTADA, NO CERTIFICADA",
    "Ningún tenant debe nacer por recibir un Host arbitrario",
    "El TenantId no es prueba de identidad ni autorización",
    "Prohibido resolver DSN desde",
    "Ningún request público crea tenants",
    "C04-02 a C04-12 permanecen bloqueadas",
    "NO EJECUTADAS",
    "C04-01 no pasa a IMPLEMENTADA ni CERTIFICADA",
)
NEGATIVES = (
    ("arbitrary Host", "Ningún tenant debe nacer por recibir un Host arbitrario"),
    ("public registration", "Ningún request público crea tenants"),
    ("client DSN", "Prohibido resolver DSN desde"),
    ("identifier trust", "El TenantId no es prueba de identidad ni autorización"),
    ("chapter jump", "C04-02 a C04-12 permanecen bloqueadas"),
    ("no implementation", "NO IMPLEMENTADA, NO CERTIFICADA"),
    ("future tests", "NO EJECUTADAS"),
    ("false chapter closure", "C04-01 no pasa a IMPLEMENTADA ni CERTIFICADA"),
)


def need(ok: bool, message: str) -> None:
    if not ok:
        raise AssertionError(message)


def read(p: Path) -> str:
    need(p.is_file(), "Required source missing: " + str(p.relative_to(ROOT)))
    return p.read_text(encoding="utf-8")


def fold(s: str) -> str:
    return "".join(c for c in unicodedata.normalize("NFKD", s.casefold()) if not unicodedata.combining(c))


def chapter_sections() -> tuple[str, int, int, int, list[str]]:
    """Detect canonical C04/05 headings inside the pinned Word XML.

    The Word also has a table of contents. Use longest chapter span, not TOC.
    """
    with zipfile.ZipFile(WORD, "r") as z:
        need(z.testzip() is None, "Corrupt canonical Word ZIP")
        root = ET.fromstring(z.read("word/document.xml"))
    paras = []
    for p in root.findall(".//" + NS + "p"):
        value = "".join((t.text or "") for t in p.iter(NS + "t")).strip()
        if value:
            paras.append(value)
    need(len(paras) > 250, "Unexpected short canonical Word")
    c04 = [i for i, p in enumerate(paras) if re.search(r"^capitulo\s*0?4\b", fold(p))]
    c05 = [i for i, p in enumerate(paras) if re.search(r"^capitulo\s*0?5\b", fold(p))]
    need(bool(c04 and c05), "Cannot locate C04/C05 in Word; do not invent source")
    spans = []
    for start in c04:
        stop = next((j for j in c05 if j > start), None)
        if stop is not None:
            body = "\n".join(paras[start:stop])
            spans.append((len(body), start, stop, body))
    need(bool(spans), "Canonical Word chapter boundaries are ambiguous")
    size, start, stop, body = max(spans)
    need(size >= 1000, "C04 source span too short; TOC is not normative chapter")
    need("tenant" in fold(body) or "dominio" in fold(body),
         "C04 content missing tenant/domain concepts")
    headings = [p[:130] for p in paras[start:stop] if
                (len(p) < 150 and
                 (re.search(r"^(?:capitulo\s|0?4[.:-]|mae-c04)", fold(p))
                  or "tenant" in fold(p) or "dominio" in fold(p)))]
    return hashlib.sha256(body.encode("utf-8")).hexdigest(), size, start, stop, headings[:12]


def spec_contract(txt: str) -> None:
    # Markdown code spans must not influence the content contract.
    plain = txt.replace(chr(96), "")
    for term in REQUIRED:
        need(term in plain, "Missing C04-01 trust boundary: " + term)
    ids = re.findall(r"^\| T(\d{2}) \|", txt, re.MULTILINE)
    need(ids == [f"{i:02d}" for i in range(1, 19)],
         "Threat matrix must contain exactly T01..T18 in order")
    need("NO IMPLEMENTADA" in plain and "NO CERTIFICADA" in plain,
         "Preparation must not masquerade as a product release")


def audit_a() -> None:
    need(canonical_active_word(), "Pinned master Word/PDF changed")
    source_sha, size, start, stop, _ = chapter_sections()
    spec_contract(read(SPEC))
    plan = read(PLAN)
    need("| C04-01 | Catálogo confiable de tenants |" in plan,
         "C04-01 diverged from the plan")
    need("C03 CERTIFICADO" in read(PREP) and "C04-01" in read(PREP),
         "C03 entrance gate or preparation reference missing")
    for pin in (CURRENT_WORD_SHA, CURRENT_PDF_SHA):
        need(pin in read(SPEC), "Source hash missing from spec: " + pin[:16])
    actual_graph()
    print(f"PASS C04-01 A: canonical Word verified; C04 text length={size}; "
          f"paragraphs={start}..{stop}; chapter_sha256={source_sha[:16]}; "
          "18 negative requirements mapped")


def audit_b() -> None:
    spec = read(SPEC)
    spec_contract(spec)
    need("contents: read" in read(ROOT / ".github/workflows/c04-01-preparation.yml"),
         "Preparation CI must be read-only")
    for name, term in NEGATIVES:
        plain = spec.replace(chr(96), "")
        need(term in plain, "Precondition absent: " + name)
        # Replace the exact markdown text or its unformatted equivalent.
        raw = term.replace("Host", chr(96) + "Host" + chr(96)) if name == "arbitrary Host" else term
        raw = raw.replace("TenantId", chr(96) + "TenantId" + chr(96)) if name == "identifier trust" else raw
        if raw not in spec:
            raw = term
        need(raw in spec, "Cannot mutate exact boundary: " + name)
        mutant = spec.replace(raw, "[ATTACKER REMOVED TRUST BOUNDARY]", 1)
        try:
            spec_contract(mutant)
        except AssertionError:
            continue
        raise AssertionError("Failed to reject falsified specification: " + name)
    print(f"PASS C04-01 B: {len(NEGATIVES)} adversarial documentation "
          "mutations rejected; NOT a runtime tenancy pentest")


def audit_c() -> None:
    need(os.environ.get("GITHUB_ACTIONS") == "true",
         "Audit C requires a real GitHub runner")
    sha, run = os.environ.get("GITHUB_SHA", ""), os.environ.get("GITHUB_RUN_ID", "")
    need(bool(re.fullmatch(r"[0-9a-f]{40}", sha) and re.fullmatch(r"\d+", run)),
         "GitHub run identity absent")
    for gate in ("A", "B"):
        marker = PROOF / gate
        need(marker.is_file() and
             marker.read_text(encoding="utf-8").strip() == f"PASS {gate} sha={sha} run={run}",
             "Same-run gate proof missing: " + gate)
    spec_contract(read(SPEC))
    need("No se ha creado ninguna base ni ejecutado SQL." in
         read(ROOT / "database/README.md"),
         "Database contract changed during preparation")
    api = read(ROOT / "backend/src/Crecisol.Api/Program.cs")
    need("/api/v1/tenants" not in api and "/api/v1/admin/tenants" not in api,
         "Active private tenant API detected during preparation")
    print("PASS C04-01 C: same-run SHA/A/B markers and non-activation boundaries")
    print("LIMIT: C04 runtime, BOLA/IDOR and MySQL tenant isolation tests NOT executed")


def source() -> None:
    need(canonical_active_word(), "Canonical Word/PDF pins invalid")
    sha, size, start, stop, headings = chapter_sections()
    print(f"C04 MASTER SOURCE sha256={sha}, chars={size}, "
          f"paragraph_range={start}:{stop}")
    for heading in headings:
        print("C04_SOURCE_HEADING:", heading)


def main() -> None:
    need(len(sys.argv) == 2 and sys.argv[1] in ("source", "A", "B", "C"),
         "Usage: c04_01_preparation_audits.py source|A|B|C")
    {"source": source, "A": audit_a, "B": audit_b, "C": audit_c}[sys.argv[1]]()


if __name__ == "__main__":
    try:
        main()
    except (OSError, AssertionError, ValueError, KeyError, zipfile.BadZipFile, ET.ParseError) as exc:
        print("FAIL C04-01 preparation:", exc, file=sys.stderr)
        sys.exit(1)
