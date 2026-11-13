#!/usr/bin/env python3
"""CRECISOL C05-02 independent email proof A/B/C. Local only, never real inbox."""
from __future__ import annotations
import json
import os
from pathlib import Path
import re
import subprocess
import sys

from c05_01_preparation_audits import ROOT
from c05_02_neutral_ingress_audits import (
    audit_a as predecessor_a, audit_b as predecessor_b,
)

REPO = "crecisol-platform/CRECISOL"
FEATURE = "feature/c05-02-independent-email-proof-20261113"
BASE = "be90d670efa2a281e37bddbe7bce00f44563aa86"
PROOF = ROOT / ".c05-02-mail-proof"
FILES = {
    "domain": "backend/src/Crecisol.Domain/Identity/EmailPossessionChallenge.cs",
    "application": "backend/src/Crecisol.Application/Identity/VerifyNeutralRegistrationMailbox.cs",
    "infrastructure": "backend/src/Crecisol.Infrastructure/Identity/MySqlDisposableEmailPossessionAdapter.cs",
    "test": "backend/tests/Crecisol.EmailPossession.MySqlTests/Program.cs",
    "project": "backend/tests/Crecisol.EmailPossession.MySqlTests/Crecisol.EmailPossession.MySqlTests.csproj",
    "schema": "database/tests/fixtures/c05_02_mailbox_proof_atomic_fixture.sql",
    "script": "database/tests/c05_02_mailbox_proof_mysql_smoke.sh",
    "document": "docs/c05/INTEGRACION_PRUEBA_CORREO_C05_02.md",
    "workflow": ".github/workflows/c05-02-mailbox-proof.yml",
    "qa": "qa/c05_02_mailbox_proof_audits.py",
}
ALLOWED = set(FILES.values())
GUARDS = {
    "domain": (
        "class EmailPossessionDigest",
        "CRECISOL/C05-02/mailbox-possession/v1/",
        "trustedTenant.Value.ToByteArray(bigEndian: true)",
        "intentId.ToByteArray(bigEndian: true)",
        "rawToken.Length != 32",
        "CryptographicOperations.FixedTimeEquals",
        "FromProtectedStorage(ReadOnlySpan<byte> stored)",
        "CryptographicOperations.ZeroMemory(input)",
        "class EphemeralEmailPossessionCode",
        "RandomNumberGenerator.GetBytes(32)",
        "Interlocked.Exchange(ref secret, null)",
        "CryptographicOperations.ZeroMemory(current)",
        "EmailPossessionDigest[redacted]",
    ),
    "application": (
        "ITrustedEmailPossessionLedger",
        "ITrustedEmailPossessionDelivery",
        "TryIssueOnceAsync(",
        "TryVerifyOnceAsync(",
        "TryRevokeUndeliveredAsync(",
        "DeliverToRecordedMailboxAsync(",
        "class VerifyNeutralRegistrationMailbox",
        "ITrustedCurrentDirectHostReader",
        "ResolveVerifiedTenantHost",
        "ITrustedTenantCatalogReader",
        "TenantLifecycleState.Active",
        "snapshot.TenantId != verified.TenantId",
        "snapshot.AddressMode != verified.AddressMode",
        "verifiedHosts.ResolveAsync(host",
        "now.AddMinutes(10)",
        "EphemeralEmailPossessionCode.Generate()",
        "CreateDigest(tenant, intentId)",
        "challenge.TransferToTrustedDeliveryOnce()",
        "CryptographicOperations.ZeroMemory(bytes)",
        "ConfirmProofAsync(",
        "hexadecimalCode.Length != 64",
        "Convert.TryFromHexString",
        "NeutralRegistrationReceipt.Acknowledged",
    ),
    "infrastructure": (
        "class MySqlDisposableEmailPossessionAdapter",
        "ITrustedEmailPossessionLedger",
        "ITrustedEmailPossessionDelivery",
        "ITrustedLocalProofMailboxSink",
        "c05_02_disposable",
        "options.Server is not (\"127.0.0.1\" or \"localhost\" or \"::1\")",
        "options.AllowLoadLocalInfile",
        "Pooling = false",
        "serverOwnedMasterKey.Length != 32",
        "MAILBOX-ENCRYPTION/V1",
        "c05_02_mailbox_proof_issue",
        "c05_02_mailbox_proof_verify",
        "c05_02_mailbox_proof_revoke",
        "CommandType.StoredProcedure",
        "tenant_id=@tenant AND intent_id=@intent LIMIT 2",
        "if (await rows.ReadAsync(cancellationToken))",
        "nonce.Length != 12",
        "tag.Length != 16",
        "version != keyVersion",
        "expiry <= DateTime.UtcNow",
        "BinaryPrimitives.WriteInt32BigEndian",
        "aes.Decrypt(nonce, cipher, tag, plaintext, aad)",
        "RequestNeutralRegistrationIntent.CanonicalMailboxOrNull",
        "sink.DeliverInTestOnlyAsync(",
        "CryptographicOperations.ZeroMemory(buffer)",
        "CryptographicOperations.ZeroMemory(storedDigest)",
    ),
    "schema": (
        "CREATE TABLE c05_02_mailbox_proofs",
        "PRIMARY KEY (tenant_id,intent_id)",
        "FOREIGN KEY (tenant_id,intent_id)",
        "REFERENCES c05_02_neutral_intents(tenant_id,intent_id)",
        "challenge_digest BINARY(32)",
        "ENUM('PENDING','VERIFIED','REVOKED')",
        "CHECK (attempts BETWEEN 1 AND 3)",
        "ENGINE=InnoDB",
        "CREATE PROCEDURE c05_02_mailbox_proof_issue",
        "CREATE PROCEDURE c05_02_mailbox_proof_verify",
        "CREATE PROCEDURE c05_02_mailbox_proof_revoke",
        "SQL SECURITY DEFINER",
        "START TRANSACTION",
        "DECLARE EXIT HANDLER FOR SQLEXCEPTION",
        "FROM c05_02_tenants",
        "FOR SHARE",
        "FROM c05_02_neutral_intents",
        "FOR UPDATE",
        "v_tenant_state='ACTIVE'",
        "p_expires<=v_intent_expiry",
        "TIMESTAMPDIFF(SECOND,p_issued,p_expires) BETWEEN 60 AND 600",
        "v_lifecycle='REVOKED' AND v_attempts<3",
        "lifecycle='PENDING'",
        "challenge_digest=p_digest",
        "expires_utc>UTC_TIMESTAMP(6)",
        "SET lifecycle='VERIFIED'",
        "SET lifecycle='REVOKED'",
        "ROW_COUNT()",
        "SELECT IF(v_verified=1,'VERIFIED','DENIED')",
    ),
    "test": (
        "--integration",
        "MySqlDisposableNeutralRegistrationRecorder",
        "MySqlDisposableEmailPossessionAdapter",
        "SyntheticVerifiedHosts",
        "SyntheticCurrentHost",
        "IsolatedMailboxSink",
        "CryptographicOperations.ZeroMemory(master)",
        "Distinct",
        "EphemeralEmailPossessionCode.Generate()",
        "Domain-separated 256-bit challenge",
        "different tenant or intent",
        "already transferred",
        "Remote DSN",
        "Sixteen concurrent MySQL proof confirmations produce exactly one winner",
        "A consumed email proof cannot be replayed",
        "Failed trusted mailbox delivery revokes challenge",
        "Delivery retry is counted",
        "Expired proof cannot be confirmed",
        "Tampered AEAD envelope denies delivery",
        "Suspended tenant cannot issue proofs",
        "Unverified host cannot select a tenant",
        "Proof service role cannot SELECT internal verification records",
        "Proof service role cannot directly mark unverified mailbox",
        "Proof service role cannot insert forged proof",
        "Proof service role cannot erase protected evidence",
        "MySQL8.4 independent mailbox proof",
    ),
    "script": (
        "mysql:8.4@sha256:",
        "--network bridge -p '127.0.0.1::3306'",
        "openssl rand -hex 32",
        "GRANT EXECUTE ON PROCEDURE",
        "GRANT SELECT ON",
        "c05_02_mailbox_proof_atomic_fixture.sql",
        "CRECISOL_C05_02_PROOF_MASTER_HEX",
        "CRECISOL_C05_02_PROOF_TEST_DSN",
        "CRECISOL_C05_02_PROOF_SETUP_DSN",
        "-- --integration",
        "docker rm -f",
    ),
    "document": (
        "C05-02 COMPLETA NO CERTIFICADA",
        "NO CERTIFICADA",
        "SMTP externo",
        "C05-03 bloqueada",
        "16 confirmaciones en paralelo",
        "P0 no resueltos",
        "Rollback",
        BASE,
    ),
    "workflow": (
        "contents: read",
        "persist-credentials: false",
        "fetch-depth: 2",
        "dotnet-version: '10.0.x'",
        "c05_02_mailbox_proof_audits.py A",
        "c05_02_mailbox_proof_audits.py B",
        "c05_02_mailbox_proof_audits.py C",
        "bash database/tests/c05_02_mailbox_proof_mysql_smoke.sh",
    ),
}

def need(condition: bool, reason: str) -> None:
    if not condition:
        raise AssertionError(reason)

def sources() -> dict[str, str]:
    result = {}
    for kind, relative in FILES.items():
        path = ROOT / relative
        need(path.is_file(), "Missing C05-02 source " + relative)
        result[kind] = path.read_text(encoding="utf-8")
    return result

def invariants(data: dict[str,str]) -> None:
    for kind, markers in GUARDS.items():
        for marker in markers:
            need(marker in data[kind], f"Missing {kind} security guard: {marker}")

    need("HttpContext" not in data["application"] and
         "IConfiguration" not in data["application"] and
         "AccountExistsAsync" not in data["application"],
         "Application must not read HTTP claims or account-existence responses")
    need("HttpContext" not in data["infrastructure"] and
         "Environment.GetEnvironmentVariable" not in data["infrastructure"] and
         "INSERT INTO c05_02_mailbox_proofs" not in data["infrastructure"] and
         "UPDATE c05_02_mailbox_proofs" not in data["infrastructure"],
         "SQL state mutations only through stored procedures and trusted configuration")
    need("GRANT ALL" not in data["script"] and
         "GRANT UPDATE ON \`$DB\`.c05_02_mailbox_proofs TO 'c05_02_proof_limited'" not in data["script"],
         "Proof role must not receive table mutation privileges")
    need("CREATE TABLE users" not in data["schema"].lower() and
         "INSERT INTO memberships" not in data["schema"].lower(),
         "No user, session or membership creation in C05-02")
    need("SSMTP" not in data["test"] and "SmtpClient" not in data["test"] and
         "@example.test" in data["test"],
         "Local-only email delivery; no external service")
    need("<TargetFramework>net10.0</TargetFramework>" in data["project"] and
         "<TreatWarningsAsErrors>true</TreatWarningsAsErrors>" in data["project"] and
         "Crecisol.Infrastructure.csproj" in data["project"],
         "Strict actual .NET10 runtime project required")
    api = (ROOT / "backend/src/Crecisol.Api/Program.cs").read_text(encoding="utf-8")
    routes = re.findall(r'app\.Map(?:Get|Post|Put|Patch|Delete)\(\s*"([^"]+)"',api)
    need(routes == ["/api/v1/public/branding/platform"],
         "No production account verification route may be enabled")
    need("permissions:\n  contents: read" in data["workflow"] and
         "deploy" not in data["workflow"].lower() and
         "workflow_dispatch:" not in data["workflow"] and
         "id-token: write" not in data["workflow"],
         "CI must not deploy, impersonate or mutate repositories")
    need("C05-02 COMPLETA NO CERTIFICADA" in data["document"] and
         "P0 no resueltos" in data["document"],
         "Documentation cannot certify a local proof as a real inbox identity")

def audit_a() -> None:
    predecessor_a()
    invariants(sources())
    print("PASS C05-02 proof A: canonical Word/PDF/C05, boundaries, protected challenge lifecycle")

def audit_b() -> None:
    predecessor_b()
    original = sources()
    invariants(original)
    rejected = 0
    for kind, markers in GUARDS.items():
        for marker in markers:
            mutant = dict(original)
            mutant[kind] = mutant[kind].replace(marker,"[REMOVED_GUARD]")
            need(mutant[kind] != original[kind],"Mutation was ineffective: "+marker)
            try:
                invariants(mutant)
            except AssertionError:
                rejected += 1
            else:
                raise AssertionError("Adversarial proof mutation bypassed: "+marker)
    need(rejected >= 95,"Insufficient negative proof guard mutations")
    print(f"PASS C05-02 proof B: {rejected} negative guarded mutations rejected")
    print("LIMIT: local test mailbox; real SMTP and account creation not evaluated")

def git(*args: str) -> str:
    return subprocess.check_output(["git",*args],cwd=ROOT,text=True,
                                   stderr=subprocess.DEVNULL).strip()

def audit_c() -> None:
    need(os.environ.get("GITHUB_ACTIONS") == "true" and
         os.environ.get("GITHUB_REPOSITORY") == REPO,
         "Actual CRECISOL GitHub runner required")
    sha, run = os.environ.get("GITHUB_SHA",""),os.environ.get("GITHUB_RUN_ID","")
    need(re.fullmatch(r"[a-f0-9]{40}",sha) is not None and
         re.fullmatch(r"\d+",run) is not None and
         git("rev-parse","HEAD")==sha, "Exact SHA+run context required")
    for stage in ("A","B","dotnet","mysql"):
        file=PROOF/stage
        need(file.is_file() and file.read_text(encoding="utf-8").strip() ==
             f"PASS {stage} sha={sha} run={run}",
             "Missing completed same-SHA evidence for "+stage)
    event=os.environ.get("GITHUB_EVENT_NAME")
    if event == "pull_request":
        path=Path(os.environ.get("GITHUB_EVENT_PATH",""))
        need(path.is_file(),"PR JSON unavailable")
        pr=json.loads(path.read_text(encoding="utf-8"))["pull_request"]
        base,head=pr["base"],pr["head"]
        need(base["ref"]=="dev" and head["ref"]==FEATURE and
             base["repo"]["full_name"]==REPO and head["repo"]["full_name"]==REPO,
             "Unauthorized base branch or fork")
        need(git("rev-list","--parents","-n","1","HEAD").split() ==
             [sha,base["sha"],head["sha"]],
             "Synthetic merge no longer matches exact base and head")
        changed=git("diff","--name-only",base["sha"],head["sha"]).splitlines()
    else:
        need(event=="push" and os.environ.get("GITHUB_REF")=="refs/heads/dev",
             "Only PR or dev push can pass this gate")
        changed=git("show","--format=","--name-only",sha).splitlines()
    need(bool(changed) and set(changed).issubset(ALLOWED) and
         FILES["domain"] in changed and FILES["schema"] in changed,
         "Out-of-scope or missing C05-02 proof changes: "+repr(changed))
    invariants(sources())
    print(f"PASS C05-02 proof C: A/B/.NET10/REAL MySQL8.4 SHA={sha} run={run}")
    print("NOT CERTIFIED: external mailbox possession, account/session/grant")

if __name__=="__main__":
    try:
        need(len(sys.argv)==2 and sys.argv[1] in ("A","B","C"),
             "Usage: c05_02_mailbox_proof_audits.py A|B|C")
        {"A":audit_a,"B":audit_b,"C":audit_c}[sys.argv[1]]()
    except (AssertionError,OSError,ValueError,KeyError,
            subprocess.SubprocessError) as error:
        print("FAIL C05-02 email proof audit: "+str(error),file=sys.stderr)
        sys.exit(1)
