#!/usr/bin/env python3
"""C05-02 isolated delivery outbox gates A/B/C; cannot certify a real SMTP flow."""
from __future__ import annotations

import json
import os
from pathlib import Path
import re
import subprocess
import sys

from c05_01_preparation_audits import ROOT
from c05_02_mailbox_proof_audits import audit_a as predecessor_a

REPO = "crecisol-platform/CRECISOL"
FEATURE = "feature/c05-02-protected-delivery-outbox-20261113"
BASE = "a49fe605eb25916ca554c0d6a7f69e30648ce75b"
EVIDENCE = ROOT / ".c05-02-delivery-outbox"
FILES = {
    "infra": "backend/src/Crecisol.Infrastructure/Identity/MySqlDisposableProofDeliveryOutbox.cs",
    "test": "backend/tests/Crecisol.ProofOutbox.MySqlTests/Program.cs",
    "project": "backend/tests/Crecisol.ProofOutbox.MySqlTests/Crecisol.ProofOutbox.MySqlTests.csproj",
    "sql": "database/tests/fixtures/c05_02_delivery_outbox_atomic_fixture.sql",
    "smoke": "database/tests/c05_02_proof_outbox_mysql_smoke.sh",
    "docs": "docs/c05/INTEGRACION_OUTBOX_PROTEGIDO_C05_02.md",
    "qa": "qa/c05_02_proof_outbox_audits.py",
    "workflow": ".github/workflows/c05-02-proof-outbox.yml",
}
ALLOWED = set(FILES.values())
MARKERS = {
    "infra": (
        "class MySqlDisposableProofDeliveryOutbox",
        "ITrustedEmailPossessionDelivery",
        "Pooling = false",
        "options.Database != \"c05_02_disposable\"",
        "options.AllowLoadLocalInfile",
        "serverOwnedMasterKey.Length != 32",
        "PROOF-OUTBOX-ENCRYPTION/V1",
        "EphemeralEmailPossessionCode.Generate()",
        "challenge.TransferToTrustedDeliveryOnce()",
        "RandomNumberGenerator.GetBytes(12)",
        "RandomNumberGenerator.GetBytes(16)",
        "new AesGcm(encryptionKey, 16)",
        "aes.Encrypt(",
        "aes.Decrypt(",
        "CryptographicOperations.FixedTimeEquals",
        "CryptographicOperations.ZeroMemory(buffer)",
        "CreateAad(trustedTenant, storedIntentId, envelope.Digest, keyVersion)",
        "envelope.KeyVersion != keyVersion",
        "c05_02_outbox_issue",
        "c05_02_outbox_claim",
        "c05_02_outbox_mark_sent",
        "c05_02_outbox_mark_failed",
        "trustedDelivery.DeliverToRecordedMailboxAsync(",
        "BestEffortRevokeAsync(",
        "return false;",
    ),
    "sql": (
        "CREATE TABLE c05_02_delivery_outbox",
        "PRIMARY KEY (tenant_id,intent_id)",
        "FOREIGN KEY (tenant_id,intent_id)",
        "REFERENCES c05_02_mailbox_proofs(tenant_id,intent_id)",
        "challenge_digest BINARY(32)",
        "nonce BINARY(12)",
        "tag BINARY(16)",
        "ciphertext BINARY(32)",
        "key_version INT UNSIGNED",
        "CHECK (key_version > 0 AND attempts BETWEEN 0 AND 3)",
        "CREATE PROCEDURE c05_02_outbox_issue",
        "CREATE PROCEDURE c05_02_outbox_claim",
        "CREATE PROCEDURE c05_02_outbox_mark_sent",
        "CREATE PROCEDURE c05_02_outbox_mark_failed",
        "SQL SECURITY DEFINER",
        "START TRANSACTION",
        "DECLARE EXIT HANDLER FOR SQLEXCEPTION",
        "FOR UPDATE",
        "FOR SHARE",
        "v_tenant_state='ACTIVE'",
        "v_proof_state='PENDING'",
        "v_proof_digest=v_outbox_digest",
        "v_attempts<3",
        "lifecycle='REVOKED'",
        "lease_nonce=p_lease",
        "leased_until_utc>UTC_TIMESTAMP(6)",
        "SELECT IF(v_issued=1,'ISSUED','DENIED') AS result",
        "SELECT IF(v_marked=1,'SENT','DENIED') AS result",
        "SELECT IF(v_revoked=1,'REVOKED','DENIED') AS result",
    ),
    "test": (
        "--integration",
        "MySqlDisposableNeutralRegistrationRecorder",
        "MySqlDisposableProofDeliveryOutbox",
        "MySqlDisposableEmailPossessionAdapter",
        "Sixteen simultaneous issue attempts",
        "Sixteen competing workers",
        "Tenant B cannot stage",
        "Stored independent email challenge verifies exactly once",
        "Verified challenge cannot be replayed",
        "Failed delivery atomically revokes outbox",
        "Tampered AEAD tag",
        "Suspended tenant cannot dispatch",
        "Expired lease recovers the SAME encrypted code",
        "Exhausted lease budget atomically revokes",
        "Worker cannot SELECT raw encrypted outbox table",
        "Worker cannot directly forge SENT state",
        "CryptographicOperations.ZeroMemory(master)",
        "@example.test",
    ),
    "smoke": (
        "mysql:8.4@sha256:",
        "--network bridge -p '127.0.0.1::3306'",
        "openssl rand -hex 32",
        "c05_02_delivery_outbox_atomic_fixture.sql",
        "GRANT EXECUTE ON PROCEDURE",
        "GRANT SELECT ON",
        "CRECISOL_C05_02_OUTBOX_MASTER_HEX",
        "CRECISOL_C05_02_OUTBOX_WORKER_DSN",
        "docker rm -f",
        "-- --integration",
    ),
    "docs": (
        "C05-02 funcional",
        "NO CERTIFICADA",
        "C05-03 bloqueada",
        "No hay proveedor SMTP/HTTP real",
        "Roll",
        BASE,
    ),
    "project": (
        "<TargetFramework>net10.0</TargetFramework>",
        "<TreatWarningsAsErrors>true</TreatWarningsAsErrors>",
        "Crecisol.Infrastructure.csproj",
    ),
    "workflow": (
        "contents: read",
        "persist-credentials: false",
        "dotnet-version: '10.0.x'",
        "c05_02_proof_outbox_audits.py A",
        "c05_02_proof_outbox_audits.py B",
        "c05_02_proof_outbox_audits.py C",
        "c05_02_proof_outbox_mysql_smoke.sh",
    ),
}

def need(ok: bool, reason: str) -> None:
    if not ok:
        raise AssertionError(reason)

def sources() -> dict[str, str]:
    return {k:(ROOT / v).read_text(encoding="utf-8") for k,v in FILES.items()}

def invariants(data: dict[str, str]) -> None:
    for kind, markers in MARKERS.items():
        for marker in markers:
            need(marker in data[kind], f"Missing {kind} guard: {marker}")
    need("MapPost(" not in data["infra"] and
         "SmtpClient" not in data["infra"] and
         "AccountExistsAsync" not in data["infra"] and
         "INSERT INTO memberships" not in data["infra"],
         "Outbox must not expose a public route, SMTP or account grant")
    need("GRANT ALL" not in data["smoke"] and
         "GRANT UPDATE ON \`$DB\`.c05_02_delivery_outbox TO 'c05_02_outbox_worker'" not in data["smoke"],
         "Outbox worker may only EXECUTE trusted state mutations")
    need("CREATE TABLE users" not in data["sql"].lower() and
         "CREATE TABLE memberships" not in data["sql"].lower() and
         "password_hash" not in data["sql"].lower(),
         "C05-02 outbox fixture cannot create identity/grant tables")
    need("workflow_dispatch:" not in data["workflow"] and
         "id-token: write" not in data["workflow"] and
         "deploy" not in data["workflow"].lower(),
         "No write/deploy permissions in CI")
    api = (ROOT / "backend/src/Crecisol.Api/Program.cs").read_text(encoding="utf-8")
    routes = re.findall(r'app\.Map(?:Get|Post|Put|Patch|Delete)\(\s*"([^"]+)"', api)
    need(routes == ["/api/v1/public/branding/platform"],
         "Protected delivery must not enable an account route or production DI")
    need("C05-02 funcional" in data["docs"] and "NO CERTIFICADA" in data["docs"],
         "No false certification of a disposable test delivery")

def audit_a() -> None:
    predecessor_a()
    invariants(sources())
    need((ROOT / "docs/c05/C05_02_PUERTA_ENTREGA_PRIVACIDAD_20261113.md").exists() is False or
         True, "PR121 is optional and must not affect this gated test")
    print("PASS outbox A: Word/C05 predecessor, tenant safety, protected only test transport")

def audit_b() -> None:
    original = sources()
    invariants(original)
    rejected = 0
    for kind, guards in MARKERS.items():
        for marker in guards:
            mutated = dict(original)
            mutated[kind] = mutated[kind].replace(marker, "[REMOVED_GUARD]")
            need(mutated[kind] != original[kind], "Mutation ineffective: "+marker)
            try:
                invariants(mutated)
            except AssertionError:
                rejected += 1
            else:
                raise AssertionError("Adversarial mutation bypass: "+marker)
    need(rejected >= 65, "Too few guard mutations rejected")
    print(f"PASS outbox B: {rejected} adverse source mutations rejected; MySQL attacks still required")

def git(*arguments: str) -> str:
    return subprocess.check_output(
        ["git",*arguments], cwd=ROOT, text=True,
        stderr=subprocess.DEVNULL).strip()

def audit_c() -> None:
    need(os.environ.get("GITHUB_ACTIONS")=="true" and
         os.environ.get("GITHUB_REPOSITORY")==REPO,
         "Only actual CRECISOL runner may attest SHA")
    sha, run = os.environ.get("GITHUB_SHA",""), os.environ.get("GITHUB_RUN_ID","")
    need(re.fullmatch(r"[a-f0-9]{40}",sha) is not None and
         re.fullmatch(r"\d+",run) is not None and
         git("rev-parse","HEAD")==sha, "Final SHA/run mismatch")
    for stage in ("A","B","dotnet","mysql"):
        p = EVIDENCE / stage
        need(p.is_file() and p.read_text(encoding="utf-8").strip() ==
             f"PASS {stage} sha={sha} run={run}",
             "Unverified real execution: "+stage)
    event=os.environ.get("GITHUB_EVENT_NAME")
    if event=="pull_request":
        path=Path(os.environ.get("GITHUB_EVENT_PATH",""))
        need(path.is_file(),"PR payload missing")
        pr=json.loads(path.read_text(encoding="utf-8"))["pull_request"]
        base,head=pr["base"],pr["head"]
        need(base["ref"]=="dev" and head["ref"]==FEATURE and
             base["repo"]["full_name"]==REPO and
             head["repo"]["full_name"]==REPO,
             "Foreign PR base, branch or repository")
        need(git("rev-list","--parents","-n","1","HEAD").split() ==
             [sha,base["sha"],head["sha"]],
             "PR merge SHA parents do not match")
        changed=git("diff","--name-only",base["sha"],head["sha"]).splitlines()
    else:
        need(event=="push" and os.environ.get("GITHUB_REF")=="refs/heads/dev",
             "Only dev postmerge push is authorized")
        changed=git("show","--format=","--name-only",sha).splitlines()
    need(bool(changed) and set(changed).issubset(ALLOWED) and
         FILES["infra"] in changed and FILES["sql"] in changed,
         "Unauthorized or incomplete outbox diff: "+repr(changed))
    invariants(sources())
    print(f"PASS outbox C: A/B + NET10/MySQL8.4 same SHA={sha} run={run}")
    print("NOT CERTIFIED: external SMTP/real recipient, auth, membership or session")

if __name__=="__main__":
    try:
        need(len(sys.argv)==2 and sys.argv[1] in ("A","B","C"),
             "Usage: c05_02_proof_outbox_audits.py A|B|C")
        {"A":audit_a,"B":audit_b,"C":audit_c}[sys.argv[1]]()
    except (AssertionError,OSError,ValueError,KeyError,subprocess.SubprocessError) as exc:
        print("FAIL C05-02 protected outbox audit: "+str(exc),file=sys.stderr)
        sys.exit(1)
