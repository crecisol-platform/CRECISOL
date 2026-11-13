#!/usr/bin/env python3
"""C05-01: read the canonical Word C05 chapter; preparation, not a runtime audit."""
from __future__ import annotations

import hashlib
import os
import re
import sys
import unicodedata
import xml.etree.ElementTree as ET
import zipfile
from pathlib import Path

from master_source_guard import canonical_active_word, CURRENT_WORD_SHA, CURRENT_PDF_SHA

ROOT = Path(__file__).resolve().parents[1]
WORD = ROOT / "docs/CRECISOL_DOCUMENTO_MAESTRO_UNICO.docx"
W = "{http://schemas.openxmlformats.org/wordprocessingml/2006/main}"
SPEC = ROOT / "docs/c05/C05_01_ESPECIFICACION.md"
PREP = ROOT / "docs/C05_PREPARACION.md"
ACTA = ROOT / "docs/c05/C05_01_ACTA_PREPARACION.md"
PLAN = ROOT / "docs/planificacion-futura/C05_MICROFASES_PROPUESTAS.md"
WORKFLOW = ROOT / ".github/workflows/c05-01-preparation.yml"
API = ROOT / "backend/src/Crecisol.Api/Program.cs"
PROOF = ROOT / ".c05-01-prep-proof"
EXPECTED_C05_SHA = "bf7036849d6f1aad36a3a111b62121947aa12fae8c2a0551e63b0cc788d4a0aa"
REQUIRED = (
    "B01 Solo servidor valida credenciales",
    "B02 Una identidad en dos negocios no hereda permisos",
    "B03 El correo no autoriza",
    "B04 Host verificado no es permiso",
    "B05 Claim tenant_id no concede tenant",
    "B06 SuperAdmin realm separado y MFA",
    "B07 Sin selector de empresa en login normal",
    "B08 Pertenencia activa, inequívoca y revocable",
    "B09 Denegar en ambigüedad",
    "B10 Recuperación: token de un solo uso",
    "B11 Preguntas de seguridad prohibidas",
    "B12 Sesiones revocadas no se reutilizan",
    "B13 C05-02 a C05-11 bloqueadas",
    "B14 NO EJECUTADAS",
    "B15 NO IMPLEMENTADA Y NO CERTIFICADA",
    "B16 No incluir secretos ni PII en logs",
)



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


def read(path: Path) -> str:
    require(path.is_file(), "Required file missing: " + str(path.relative_to(ROOT)))
    return path.read_text(encoding="utf-8")


def spec_contract(value: str) -> None:
    plain = value.replace(chr(96), "")
    for item in REQUIRED:
        require(item in plain, "Missing fail-closed source requirement: " + item)
    ids = re.findall(r"^\\| T(\\d{2}) \\|", value, flags=re.MULTILINE)
    require(ids == [f"{i:02d}" for i in range(1, 25)],
            "Threat matrix must have T01..T24 ordered, with no gaps or duplicates")
    for term in ("TenantMembership", "PlatformOperator", "ProfileScope", "Credential",
                 "C05-01", "C05-11", "C04", "PREPARACIÓN DOCUMENTAL",
                 EXPECTED_C05_SHA, CURRENT_WORD_SHA, CURRENT_PDF_SHA):
        require(term in plain, "Missing identity/authority boundary: " + term)


def audit_a() -> None:
    digest, lines, start, stop = canonical_c05_source()
    require(digest == EXPECTED_C05_SHA, "C05 Word source changed or hash misrecorded")
    require((start, stop, len(lines)) == (1807, 1900, 93),
            "C05 canonical paragraph span drift")
    folded = fold("\\n".join(lines))
    for term in ("identidad", "autenticacion", "recuperacion", "invitaciones",
                 "passkeys", "mfa", "preguntas de seguridad", "sesiones", "selector",
                 "tenant", "revocacion"):
        require(term in folded, "Canonical C05 required topic absent: " + term)
    spec_contract(read(SPEC))
    prep = read(PREP)
    acta = read(ACTA)
    for term in (EXPECTED_C05_SHA, CURRENT_WORD_SHA, CURRENT_PDF_SHA,
                 "479cce19a27a1c947df615a01284f2de60f1168f",
                 "6843638186"):
        require(term in prep or term in acta, "Missing C04/C05 source gate: " + term)
    rows = re.findall(r"^\\| (C05-\\d{2}) \\|", read(PLAN), flags=re.MULTILINE)
    require(rows == [f"C05-{i:02d}" for i in range(1, 12)],
            "C05 11-phase proposed plan is not intact")
    require("C05-01 NO IMPLEMENTADA Y NO CERTIFICADA" in acta,
            "Preparation falsely resembles certification")
    print("PASS C05-01 A: pinned Word/PDF, C05 source hash, 11 plan phases, C04 institutional gate")


def audit_b() -> None:
    spec = read(SPEC)
    spec_contract(spec)
    for marker in REQUIRED:
        # The mutation changes only the first exact semantic contract anchor.
        mutated = spec.replace(marker, "[REMOVED TRUST BOUNDARY]", 1)
        require(mutated != spec, "No source mutation possible: " + marker)
        try:
            spec_contract(mutated)
        except AssertionError:
            continue
        raise AssertionError("Fail-open document mutation not rejected: " + marker)
    wf = read(WORKFLOW)
    require("contents: read" in wf and "persist-credentials: false" in wf,
            "Workflow must be read-only with no persistent checkout credentials")
    require("workflow_dispatch:" not in wf and "pull_request:" in wf,
            "Preparation workflow must not start privileged manual jobs")
    api = read(API)
    routes = re.findall(r"app\\.Map(?:Get|Post|Put|Patch|Delete)\\(\\s*\\\"([^\\\"]+)\\\"", api)
    require(routes == ["/api/v1/public/branding/platform"],
            "Unexpected private auth/tenant API in preparation")
    print(f"PASS C05-01 B: {len(REQUIRED)} adversarial text mutations rejected; no private API")
    print("LIMIT: no identity, credential, MFA, HTTP or MySQL runtime security tests executed")


def audit_c() -> None:
    require(os.environ.get("GITHUB_ACTIONS") == "true",
            "Audit C needs a genuine GitHub Actions runner")
    sha = os.environ.get("GITHUB_SHA", "")
    run_id = os.environ.get("GITHUB_RUN_ID", "")
    require(bool(re.fullmatch(r"[a-f0-9]{40}", sha) and re.fullmatch(r"[0-9]+", run_id)),
            "Missing exact SHA/run identity")
    for gate in ("A", "B"):
        marker = PROOF / gate
        require(marker.is_file() and
                marker.read_text(encoding="utf-8").strip() ==
                f"PASS {gate} sha={sha} run={run_id}",
                "Audit proof missing or from another SHA/run: " + gate)
    spec_contract(read(SPEC))
    require("NO IMPLEMENTADA Y NO CERTIFICADA" in read(ACTA),
            "False preparatory certification in acta")
    require("Sin nuevas migraciones ni SQL" in read(PREP),
            "Database no-change barrier removed")
    print(f"PASS C05-01 C: one SHA/run={sha[:12]}/{run_id}, A+B proved, docs-only boundary")
    print("NOT CERTIFIED: C05-01 runtime and C05-02..11 remain blocked")


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
        require(len(sys.argv) == 2 and sys.argv[1] in ("source", "A", "B", "C"),
                "Usage: c05_01_preparation_audits.py source|A|B|C")
        {"source": source, "A": audit_a, "B": audit_b, "C": audit_c}[sys.argv[1]]()
    except (OSError, AssertionError, ValueError, zipfile.BadZipFile, ET.ParseError) as e:
        print("FAIL C05-01 source preparation:", e, file=sys.stderr)
        sys.exit(1)
