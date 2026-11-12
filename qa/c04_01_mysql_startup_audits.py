#!/usr/bin/env python3
"""Regression gates for the *test runner's* MySQL 8.4 final-startup race.

This does not implement or certify tenant storage. MySQL smoke must execute
separately and publish a same-run proof for audit C.
"""
from pathlib import Path
import os
import re
import sys

ROOT = Path(__file__).resolve().parents[1]
SMOKE = ROOT / "database/tests/c01_04_api_mysql_smoke.sh"
REPORT = ROOT / "docs/c04/C04_01_REMEDIACION_MYSQL_CI_2026_11_12.md"
PROOF = ROOT / ".c04-01-mysql-proof"
DIGEST = "mysql:8.4@sha256:a81d043930e51745522bd5c430961cf8286f0a23f0a9a2a6e0108f7ea71b0902"
REQUIRED = (
    "--network none",
    DIGEST,
    "MySQL init process done. Ready for start up.",
    'if [ "$init_done" -eq 1 ]',
    'root_sql "SELECT 1"',
    'sleep 1',
    'docker rm -f "$CONTAINER"',
    '[ -S "$SOCKET_DIR/mysqld.sock" ]',
    "GRANT SELECT ON",
    "UPDATE brand_revisions SET name='illegal'",
)
def need(ok: bool, msg: str) -> None:
    if not ok: raise AssertionError(msg)

def validate(text: str) -> None:
    for s in REQUIRED:
        need(s in text, "Missing MySQL fail-closed safety contract: " + s)
    need(text.count('root_sql "SELECT 1"') == 2,
         "Final server must pass exactly two SQL readiness checks")
    need(text.index("MySQL init process done. Ready for start up.") <
         text.index('root_sql "SELECT 1"'), "Bootstrap-final marker ordering changed")
    need('--publish' not in text and '--privileged' not in text,
         "MySQL test exposed network/privileged capability")
    need("READER_PASSWORD=\"$(openssl rand -hex 18)\"" in text,
         "Synthetic local password generation changed")
    need("trap cleanup EXIT" in text, "Container/socket cleanup lost")
    need("FAIL MySQL final server initialization/socket readiness" in text,
         "MySQL final readiness timeout must fail closed")

def audit_a() -> None:
    text = SMOKE.read_text(encoding="utf-8")
    report = REPORT.read_text(encoding="utf-8")
    validate(text)
    need("43242306682" in report and "ERROR 2002" in report,
         "Incident evidence absent from report")
    need("hipótesis" in report.casefold() and "NO" in report,
         "Do not call an unconfirmed startup race proven")
    print("PASS C04-01 MySQL audit A: real failed-run provenance and final-server bootstrap fence")

def audit_b() -> None:
    text = SMOKE.read_text(encoding="utf-8")
    validate(text)
    rejected = 0
    for marker in REQUIRED:
        tampered = text.replace(marker, "[REMOVED REQUIRED GATE]")
        try:
            validate(tampered)
        except AssertionError:
            rejected += 1
        else:
            raise AssertionError("Unsafe mutation accepted: " + marker)
    print("PASS C04-01 MySQL audit B: " + str(rejected) +
          " mutations rejected; pinned image, Unix socket, no network or secrets")

def audit_c() -> None:
    need(os.environ.get("GITHUB_ACTIONS") == "true",
         "Audit C requires actual hosted runner")
    sha = os.environ.get("GITHUB_SHA","")
    run = os.environ.get("GITHUB_RUN_ID","")
    need(bool(re.fullmatch("[0-9a-f]{40}",sha) and re.fullmatch("[0-9]+",run)),
         "Invalid run identity")
    for stage in ("A","B","mysql"):
        p = PROOF / stage
        need(p.is_file() and p.read_text(encoding="utf-8").strip() ==
             "PASS " + stage + " sha=" + sha + " run=" + run,
             "Same-run proof absent: " + stage)
    validate(SMOKE.read_text(encoding="utf-8"))
    print("PASS C04-01 MySQL audit C: actual disposable 8.4 + HTTP smoke and SHA/run A-B linked")
    print("LIMIT: MySQL synthetic public branding, not tenant storage or production")

if __name__ == "__main__":
    try:
        need(len(sys.argv)==2 and sys.argv[1] in ("A","B","C"),
             "Usage: c04_01_mysql_startup_audits.py A|B|C")
        {"A":audit_a, "B":audit_b,"C":audit_c}[sys.argv[1]]()
    except (AssertionError, OSError) as exc:
        print("FAIL C04-01 MySQL startup audit:", exc, file=sys.stderr)
        sys.exit(1)
