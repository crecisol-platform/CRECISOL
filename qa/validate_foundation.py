#!/usr/bin/env python3
"""Comprobaciones locales de estructura CRECISOL; no certifican el SaaS."""
from pathlib import Path
import sys

ROOT = Path(__file__).resolve().parents[1]
required = (
    "README.md", "AGENTS.md", ".gitignore", ".editorconfig",
    ".agents/skills/crecisol-project-governance/SKILL.md",
    ".agents/skills/crecisol-project-governance/agents/openai.yaml",
    "frontend/README.md", "backend/README.md", "database/README.md",
    "docs/MASTER_REFERENCE.md", "docs/PLAN_37_CAPITULOS.md",
    "docs/SEGURIDAD_MULTITENANT.md", "docs/REPOSITORY_GATES.md",
)
errors = [f"Missing required file: {name}" for name in required if not (ROOT / name).is_file()]
skill = (ROOT / ".agents/skills/crecisol-project-governance/SKILL.md").read_text(encoding="utf-8")
if not skill.startswith("---\nname: crecisol-project-governance\n"):
    errors.append("Invalid CRECISOL skill header")
if "solqaryn/Solqaryn" not in skill or "prohibido" not in skill.lower():
    errors.append("Missing explicit SOLQARYN read-only boundary")
ignore = (ROOT / ".gitignore").read_text(encoding="utf-8")
for marker in (".env", "node_modules/", "bin/", "obj/", "backups/"):
    if marker not in ignore:
        errors.append("Missing ignore rule: " + marker)
plan = (ROOT / "docs/PLAN_37_CAPITULOS.md").read_text(encoding="utf-8")
for i in range(1, 38):
    if f"C{i:02d} |" not in plan:
        errors.append(f"Missing chapter index C{i:02d}")
if len(list((ROOT / ".agents/skills").glob("*/SKILL.md"))) != 1:
    errors.append("Exactly one local governance skill must exist")
for forbidden in ("../Solqaryn/", "solqaryn/Solqaryn/backend/", "solqaryn/Solqaryn/frontend/"):
    if forbidden in (ROOT / "AGENTS.md").read_text(encoding="utf-8"):
        errors.append("Prohibited cross-repository path: " + forbidden)
if errors:
    print("FOUNDATION FAILED")
    print("\n".join(errors))
    sys.exit(1)
print("CRECISOL foundation OK: structure, 37-chapter index, skill boundary and ignore policy")
print("NOTE: this check does not compile Angular/.NET, run MySQL or certify security")
