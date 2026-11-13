#!/usr/bin/env python3
"""CRECISOL C05-02 retention A/B/C: isolated proof only, never signup certification."""
from __future__ import annotations

import json
import os
from pathlib import Path
import re
import subprocess
import sys

from c05_01_preparation_audits import ROOT

REPO = "crecisol-platform/CRECISOL"
FEATURE = "feature/c05-02-privacy-retention-guard-20261113"
FILES = {
    "schema": "database/tests/fixtures/c05_02_privacy_retention_fixture.sql",
    "smoke": "database/tests/c05_02_privacy_retention_mysql.sh",
    "document": "docs/c05/RETENCION_PRIVACIDAD_C05_02.md",
    "audit": "qa/c05_02_privacy_retention_audits.py",
    "workflow": ".github/workflows/c05-02-privacy-retention.yml",
}
ALLOWED = set(FILES.values())
EVIDENCE = ROOT / ".c05-02-retention-proof"
GUARDS = {
    "schema": (
        "CREATE TABLE c05_02_privacy_dispositions",
        "PRIMARY KEY (tenant_id,intent_id)",
        "REFERENCES c05_02_neutral_intents(tenant_id,intent_id)",
        "ON DELETE CASCADE ON UPDATE RESTRICT",
        "CREATE PROCEDURE c05_02_dispose_expired_registration_pii",
        "SQL SECURITY DEFINER",
        "DECLARE EXIT HANDLER FOR SQLEXCEPTION",
        "ROLLBACK;",
        "p_batch_size IS NULL OR p_batch_size < 1 OR p_batch_size > 100",
        "START TRANSACTION;",
        "INSERT INTO c05_02_privacy_dispositions",
        "LEFT JOIN c05_02_privacy_dispositions AS d",
        "i.expires_at_utc <= v_now",
        "AND d.intent_id IS NULL",
        "LIMIT p_batch_size;",
        "UPDATE c05_02_neutral_intents AS i",
        "JOIN c05_02_privacy_dispositions AS d",
        "AND d.job_id=v_job",
        "i.ciphertext=RANDOM_BYTES(OCTET_LENGTH(i.ciphertext))",
        "i.nonce=RANDOM_BYTES(12)",
        "i.tag=RANDOM_BYTES(16)",
        "i.mailbox_digest=RANDOM_BYTES(32)",
        "i.peer_digest=RANDOM_BYTES(32)",
        "DELETE FROM c05_02_neutral_quota",
        "window_bucket < v_bucket - 2",
        "COMMIT;",
        "SELECT 'ACK' AS result;",
    ),
    "smoke": (
        "mysql:8.4@sha256:",
        "--network bridge",
        "-p '127.0.0.1::3306'",
        "openssl rand -hex 24",
        "docker rm -f",
        "c05_02_invitation_atomic_fixture.sql",
        "c05_02_neutral_registration_fixture.sql",
        "c05_02_privacy_retention_fixture.sql",
        "GRANT EXECUTE ON PROCEDURE",
        "TO 'c05_02_privacy_worker'@'%';",
        "SUSPENDED",
        "CALL c05_02_dispose_expired_registration_pii(1)",
        "SELECT COUNT(*) FROM c05_02_privacy_dispositions",
        "Active A ciphertext unchanged",
        "Active B ciphertext unchanged",
        "Expired encrypted data and mailbox digests overwritten",
        "Tenant-scoped foreign keys remain valid",
        "Only prior valid quota window remains",
        "for value in 0 101 NULL",
        "worker can read PII",
        "worker can mutate PII directly",
        "NO signup or functional certification",
    ),
    "workflow": (
        "permissions:",
        "contents: read",
        "persist-credentials: false",
        "fetch-depth: 2",
        "python3 qa/c05_02_privacy_retention_audits.py A",
        "python3 qa/c05_02_privacy_retention_audits.py B",
        "python3 qa/c05_02_privacy_retention_audits.py C",
        "bash database/tests/c05_02_privacy_retention_mysql.sh",
        "dotnet-version: '10.0.x'",
        ".c05-02-retention-proof",
    ),
    "document": (
        "C05-02",
        "Documento Maestro",
        "NO CERTIFICADA FUNCIONALMENTE",
        "C05-03 BLOQUEADA",
        "P0/P1",
        "MySQL 8.4",
        "SQL SECURITY DEFINER",
        "EXECUTE",
        "A/B/C",
        "Rollback",
    ),
}


def require(ok: bool, reason: str) -> None:
    if not ok:
        raise AssertionError(reason)


def sources() -> dict[str, str]:
    return {k: (ROOT / p).read_text(encoding="utf-8")
            for k, p in FILES.items() if k != "audit"}


def invariants(data: dict[str, str]) -> None:
    for key, markers in GUARDS.items():
        for marker in markers:
            require(marker in data[key], f"Missing fail-closed {key} marker: {marker}")
    require("GRANT ALL" not in data["smoke"] and
            "GRANT SELECT ON" not in data["smoke"] and
            "GRANT UPDATE ON" not in data["smoke"],
            "Maintenance worker must be EXECUTE-only")
    require("CREATE DATABASE" not in data["schema"] and
            "DROP TABLE" not in data["schema"] and
            "DELETE FROM c05_02_neutral_intents" not in data["schema"],
            "Privacy extension must preserve tenant+intent keys")
    require("workflow_dispatch:" not in data["workflow"] and
            "id-token: write" not in data["workflow"] and
            "deploy" not in data["workflow"].lower(),
            "No privileged CI or deployment")
    api = (ROOT / "backend/src/Crecisol.Api/Program.cs").read_text(encoding="utf-8")
    routes = re.findall(r'app\.Map(?:Get|Post|Put|Patch|Delete)\(\s*"([^"]+)"', api)
    require(routes == ["/api/v1/public/branding/platform"],
            "No private registration or identity API may be introduced")


def audit_a() -> None:
    subprocess.run([sys.executable, "qa/master_source_guard.py", "--self-test"],
                   cwd=ROOT, check=True)
    invariants(sources())
    print("PASS C05-02 retention A: canonical source, exact synthetic scope, no private API")


def audit_b() -> None:
    original = sources()
    invariants(original)
    rejected = 0
    for key, markers in GUARDS.items():
        for marker in markers:
            mutant = dict(original)
            mutant[key] = original[key].replace(marker, "[REMOVED_C05_PRIVACY_GUARD]")
            require(mutant[key] != original[key], "Ineffective mutation")
            try:
                invariants(mutant)
            except AssertionError:
                rejected += 1
            else:
                raise AssertionError(f"Unsafe mutation accepted: {key}:{marker}")
    require(rejected >= 45, "Insufficient negative guard coverage")
    print(f"PASS C05-02 retention B: {rejected} guard-removal mutations rejected")
    print("STATIC MUTATIONS DO NOT REPLACE REAL MYSQL8.4 RUNTIME CHECKS")


def git(*args: str) -> str:
    return subprocess.check_output(["git", *args], cwd=ROOT, text=True,
                                   stderr=subprocess.DEVNULL).strip()


def audit_c() -> None:
    require(os.getenv("GITHUB_ACTIONS") == "true" and
            os.getenv("GITHUB_REPOSITORY") == REPO,
            "Only CRECISOL GitHub Actions can attest this audit")
    sha = os.getenv("GITHUB_SHA", "")
    run = os.getenv("GITHUB_RUN_ID", "")
    require(re.fullmatch(r"[0-9a-f]{40}", sha) is not None and
            re.fullmatch(r"\d+", run) is not None and
            git("rev-parse", "HEAD") == sha,
            "Exact running SHA and run ID required")
    for stage in ("A", "B", "mysql", "dotnet"):
        file = EVIDENCE / stage
        require(file.is_file() and file.read_text(encoding="utf-8").strip() ==
                f"PASS {stage} sha={sha} run={run}",
                "Missing same-checkout proof: " + stage)
    event = os.getenv("GITHUB_EVENT_NAME")
    if event == "pull_request":
        payload = Path(os.getenv("GITHUB_EVENT_PATH", ""))
        require(payload.is_file(), "Pull request payload missing")
        pr = json.loads(payload.read_text(encoding="utf-8"))["pull_request"]
        base, head = pr["base"], pr["head"]
        require(base["ref"] == "dev" and head["ref"] == FEATURE and
                base["repo"]["full_name"] == REPO and
                head["repo"]["full_name"] == REPO,
                "Wrong target, branch or repository")
        parents = git("rev-list", "--parents", "-n", "1", "HEAD").split()
        require(parents == [sha, base["sha"], head["sha"]],
                "Synthetic merge must match exact base/head")
        changed = git("diff", "--name-only", base["sha"], head["sha"]).splitlines()
    else:
        require(event == "push" and os.getenv("GITHUB_REF") == "refs/heads/dev",
                "Only postmerge on dev")
        changed = git("show", "--format=", "--name-only", sha).splitlines()
    require(set(changed) == ALLOWED and len(changed) == len(ALLOWED),
            f"Unauthorized or missing files: {changed}")
    invariants(sources())
    print(f"PASS C05-02 retention C: A/B/.NET10/MySQL8.4 exactly SHA={sha} RUN={run}")
    print("NOT CERTIFIED: no real authenticated user, external inbox or membership")


if __name__ == "__main__":
    try:
        require(len(sys.argv) == 2 and sys.argv[1] in ("A", "B", "C"),
                "Usage: C05-02 audit A|B|C")
        {"A": audit_a, "B": audit_b, "C": audit_c}[sys.argv[1]]()
    except (AssertionError, OSError, ValueError, KeyError,
            subprocess.SubprocessError) as exc:
        print("FAIL C05-02 privacy retention: " + str(exc), file=sys.stderr)
        sys.exit(1)
