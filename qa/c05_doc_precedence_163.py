#!/usr/bin/env python3
"""DOC-P2 #163: state precedence without deleting immutable historical evidence.

A: active state authority, B: negative mutations, C: document and scope consistency.
This gate validates *documents*, not C01-C05 production readiness.
"""
from pathlib import Path
import sys

ROOT = Path(__file__).resolve().parents[1]
BEGIN = "<!-- CRECISOL-DOC163-ESTADO-ACTUAL:INICIO -->"
END = "<!-- CRECISOL-DOC163-ESTADO-ACTUAL:FIN -->"
FILES = {
    ".agents/skills/crecisol-project-governance/SKILL.md":
        ("# CRECISOL · Gobierno técnico propio",
         "**PRECEDENCIA PROSPECTIVA ACTUAL (11-11-2026):**"),
    "AGENTS.md":
        ("# Agentes y colaboradores — CRECISOL",
         "**AUTORIDAD INSTITUCIONAL PROSPECTIVA (11-11-2026):**"),
    "docs/MASTER_REFERENCE.md":
        ("# Documento Maestro Único — referencia canónica CRECISOL",
         "**REGISTRO POSTERIOR DE ESTADO INSTITUCIONAL (11-11-2026):**"),
    "docs/PLAN_37_CAPITULOS.md":
        ("# Orden de implementación obligatorio de CRECISOL",
         "**NOTA VIGENTE C03-10 (11-11-2026):**"),
}

def require(condition: bool, reason: str) -> None:
    if not condition:
        raise AssertionError("DOC163 NO-GO: " + reason)

def check(path: str, value: str) -> None:
    heading, old_open = FILES[path]
    if path.startswith(".agents/"):
        require(value.startswith("---\nname: crecisol-project-governance\n") and
                value.count(heading) == 1, path + " changed Skill frontmatter")
    elif path == "docs/PLAN_37_CAPITULOS.md":
        # The historical C03 preflight intentionally reads the first seven
        # plan lines: keep their positions and original paragraphs intact.
        require(value.startswith(heading + "\n" + BEGIN),
                path + " moved the historical C03 plan line")
    else:
        require(value.startswith(heading + "\n\n"), path + " changed heading")
    require(value.count(BEGIN) == 1 and value.count(END) == 1,
            path + " missing/duplicate current-state boundary")
    prefix, after_begin = value.split(BEGIN, 1)
    block, history = after_begin.split(END, 1)
    if path == "docs/PLAN_37_CAPITULOS.md":
        require(prefix == heading + "\n" and
                history.startswith("  \n" + old_open) and
                value.splitlines()[6].startswith("**ESTADO VIGENTE:"),
                path + " historical C03 7-line status shifted")
    else:
        require(prefix.endswith(heading + "\n\n") and
                history.startswith("\n\n" + old_open),
                path + " historical first paragraph changed or precedence moved")
    required = (
        "16-11-2026", "#163",
        "docs/ESTADO_VIGENTE_CAPITULOS.md",
        "5/37", "C01–C05", "C03 10/10",
        "C04 12/12", "C06 permanece BLOQUEADO",
        "antecedentes históricos", "Documento Maestro Word/PDF",
    )
    for label in required:
        require(label in block, path + " no longer advertises " + label)
    require("C04 BLOQUEADO" in history and len(history) > 1000,
            path + " removed the immutable historical chapter lock")

def read(path: str) -> str:
    return (ROOT / path).read_text(encoding="utf-8")

def audit_a() -> None:
    for path in FILES:
        check(path, read(path))
    institutional = read("docs/ESTADO_VIGENTE_CAPITULOS.md")
    head = institutional.split("**INTEGRACIÓN OPERATIVA LOCAL C05", 1)[0]
    require("**CERTIFICACIÓN INTEGRAL OPERATIVA C05" in head and
            "**5/37**" in head and "**C06 permanece BLOQUEADO**" in head,
            "institutional authority is inconsistent with document headers")
    print("PASS DOC163-A: authority current, all four headers and historical paragraphs")

def audit_b() -> None:
    for path in FILES:
        original = read(path)
        check(path, original)
        mutations = (
            original.replace(BEGIN, "deleted", 1),
            original.replace("5/37", "3/37", 1),
            original.replace("C04 12/12", "C04 0/12", 1),
            original.replace("C06 permanece BLOQUEADO", "C06 autorizado", 1),
            original.replace("docs/ESTADO_VIGENTE_CAPITULOS.md", "docs/desconocido.md", 1),
            original.replace("**PRECEDENCIA PROSPECTIVA ACTUAL (11-11-2026):**", "Borrado", 1)
              if path.endswith("SKILL.md") else original.replace(FILES[path][1], "Borrado", 1),
        )
        for number, candidate in enumerate(mutations, 1):
            try:
                check(path, candidate)
            except AssertionError:
                continue
            raise AssertionError(f"DOC163 NO-GO: bad change {number} survived in {path}")
    print(f"PASS DOC163-B: {len(FILES)*6} hostile header/history mutations rejected")

def audit_c() -> None:
    for path in FILES:
        check(path, read(path))
    readme = read("README.md")
    require("<!-- CRECISOL-ESTADO-C03-RETRO:INICIO -->" in readme and
            "**5/37 capítulos certificados" in readme and
            "**C03: 10/10**" in readme and "**C04: 12/12**" in readme,
            "README current state differs")
    reference = read("docs/MASTER_REFERENCE.md")
    for sha in (
        "ea5afc89e74915a9b18b9753ddb832f4be8e5d00f5e66b0f008e02e03476444c",
        "0dfd3217439760540390707ea8196d6252464a501a7fe3e6eafdc49eda39da77",
    ):
        require(sha in reference, "master Word/PDF pinned hash lost")
    require("solqaryn/Solqaryn" in read(".agents/skills/crecisol-project-governance/SKILL.md"),
            "SOLQARYN write prohibition removed")
    print("PASS DOC163-C: canonical hashes and architecture boundary remain intact")

if __name__ == "__main__":
    try:
        require(len(sys.argv) == 2 and sys.argv[1] in ("A", "B", "C"),
                "expected a single audit A|B|C")
        {"A": audit_a, "B": audit_b, "C": audit_c}[sys.argv[1]]()
    except (AssertionError, OSError) as error:
        print("FAIL DOC163:", error, file=sys.stderr)
        sys.exit(1)
