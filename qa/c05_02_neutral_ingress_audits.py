#!/usr/bin/env python3
"""C05-02 neutral HTTP/MySQL integration A/B/C; NEVER production certification."""
from __future__ import annotations
import json
import os
from pathlib import Path
import re
import subprocess
import sys

from c05_01_preparation_audits import ROOT

REPO = "crecisol-platform/CRECISOL"
FEATURE = "feature/c05-02-neutral-registration-intake-20261113"
BASE = "b891145f2c7e724a0a86c3f9c66585309f591ddf"
WORD = "ea5afc89e74915a9b18b9753ddb832f4be8e5d00f5e66b0f008e02e03476444c"
PDF = "0dfd3217439760540390707ea8196d6252464a501a7fe3e6eafdc49eda39da77"
C05 = "bf7036849d6f1aad36a3a111b62121947aa12fae8c2a0551e63b0cc788d4a0aa"
FILES = {
    "application": "backend/src/Crecisol.Application/Identity/RequestNeutralRegistrationIntent.cs",
    "infrastructure": "backend/src/Crecisol.Infrastructure/Identity/MySqlDisposableNeutralRegistrationRecorder.cs",
    "test": "backend/tests/Crecisol.NeutralRegistration.IntegrationTests/Program.cs",
    "project": "backend/tests/Crecisol.NeutralRegistration.IntegrationTests/Crecisol.NeutralRegistration.IntegrationTests.csproj",
    "schema": "database/tests/fixtures/c05_02_neutral_registration_fixture.sql",
    "script": "database/tests/c05_02_neutral_registration_http_mysql.sh",
    "document": "docs/c05/C05_REGISTRO_NEUTRAL_HTTP_MYSQL.md",
    "qa": "qa/c05_02_neutral_ingress_audits.py",
    "workflow": ".github/workflows/c05-02-neutral-registration.yml",
}
ALLOWED = set(FILES.values())
PROOF = ROOT / ".c05-02-neutral-proof"
GUARDS = {
    "application": (
        "ITrustedRegistrationNetworkPeer",
        "ITrustedNeutralRegistrationIntentRecorder",
        "class RequestNeutralRegistrationIntent",
        "ITrustedCurrentDirectHostReader",
        "ResolveVerifiedTenantHost",
        "ITrustedTenantCatalogReader",
        "NeutralRegistrationReceipt.Acknowledged",
        "CanonicalMailboxOrNull(requestedMailbox)",
        "verified.TenantId",
        "initialTenant.AddressMode != verified.AddressMode",
        "initialTenant.State != TenantLifecycleState.Active",
        "socketPeer.Value.Length != 16",
        "IndexOfAnyExcept((byte)0)",
        "refreshed != verified || latest != initialTenant",
        "finalNow < now",
        "latest.State != TenantLifecycleState.Active",
        "RecordWithoutEnumerationAsync(",
        "requestedMailbox",
        "domain.ToLowerInvariant()",
        "local.Contains(\"..\", StringComparison.Ordinal)",
        "email.Any(c => c < 33 || c > 126)",
        "at != email.LastIndexOf('@')",
        "public static string? CanonicalMailboxOrNull",
    ),
    "infrastructure": (
        "class MySqlDisposableNeutralRegistrationRecorder",
        "ITrustedNeutralRegistrationIntentRecorder",
        "c05_02_disposable",
        "options.Server is not (\"127.0.0.1\" or \"localhost\" or \"::1\")",
        "options.AllowLoadLocalInfile",
        "Pooling = false",
        "serverOwnedMasterKey.Length != 32",
        "serverOwnedKeyVersion < 1",
        "HMACSHA256.HashData(serverOwnedMasterKey,",
        "MAILBOX-FINGERPRINT/V1",
        "SOCKET-QUOTA/V1",
        "MAILBOX-ENCRYPTION/V1",
        "RequestNeutralRegistrationIntent.CanonicalMailboxOrNull(canonicalMailbox)",
        "tenantAndPeer.AsSpan(16)",
        "BinaryPrimitives.WriteInt32BigEndian",
        "nonce = RandomNumberGenerator.GetBytes(12)",
        "using (var aes = new AesGcm(encryptionKey, 16))",
        "aes.Encrypt(nonce, email, ciphertext, tag, aad)",
        "c05_02_record_registration_intent",
        "CommandType.StoredProcedure",
        "new MySqlConnection(dsn)",
        "nowUtc.AddMinutes(30)",
        "resultCode != \"ACK\"",
        "CryptographicOperations.ZeroMemory(secret)",
        "CryptographicOperations.ZeroMemory(mailboxKey)",
        "CryptographicOperations.ZeroMemory(peerKey)",
        "CryptographicOperations.ZeroMemory(encryptionKey)",
    ),
    "schema": (
        "ENGINE=InnoDB",
        "CREATE TABLE c05_02_neutral_quota",
        "CREATE TABLE c05_02_neutral_intents",
        "PRIMARY KEY (tenant_id,peer_digest,window_bucket)",
        "PRIMARY KEY (tenant_id,intent_id)",
        "REFERENCES c05_02_tenants(tenant_id)",
        "nonce BINARY(12)",
        "tag BINARY(16)",
        "ciphertext VARBINARY(254)",
        "mailbox_digest BINARY(32)",
        "CREATE PROCEDURE c05_02_record_registration_intent",
        "SQL SECURITY DEFINER",
        "DECLARE EXIT HANDLER FOR SQLEXCEPTION",
        "START TRANSACTION",
        "SELECT lifecycle INTO v_state",
        "FOR SHARE",
        "v_state='ACTIVE'",
        "p_key_version > 0",
        "p_created_utc >= DATE_SUB(UTC_TIMESTAMP(6), INTERVAL 5 MINUTE)",
        "TIMESTAMPDIFF(SECOND,p_created_utc,p_expires_utc) BETWEEN 60 AND 3600",
        "FLOOR(UNIX_TIMESTAMP(UTC_TIMESTAMP(6)) / 900)",
        "INSERT INTO c05_02_neutral_quota",
        "ON DUPLICATE KEY UPDATE attempts=LEAST(attempts+1,9)",
        "v_attempts <= 8",
        "INSERT INTO c05_02_neutral_intents",
        "COMMIT",
        "SELECT 'ACK' AS result",
    ),
    "test": (
        "--integration",
        "WebApplication.CreateBuilder()",
        "UseUrls(\"http://127.0.0.1:0\")",
        "builder.Logging.ClearProviders()",
        "MapPost(\"/__test_only/c05-02/public/registration-intent\"",
        "RequestNeutralRegistrationIntent",
        "app.MapPost",
        "Known@Example.TEST",
        "Unknown@Example.TEST",
        "known.Body == unknown.Body",
        "REQUEST_ACCEPTED_IF_ELIGIBLE",
        "Cache-Control",
        "X-Tenant-Id",
        "X-Forwarded-Host",
        "Forwarded",
        "X-Forwarded-For",
        "MapToIPv6().GetAddressBytes()",
        "MySqlDisposableNeutralRegistrationRecorder(limitedDsn, key, 1)",
        "Verify AES-GCM envelope independently",
        "AES-GCM rejects tampered ciphertext",
        "aDigest != bDigest",
        "COUNT(*) FROM c05_02_neutral_intents",
        "the same neutral ACK",
        "Suspended tenant receives neutral response",
        "Ingestion role cannot SELECT encrypted email",
        "Ingestion role cannot mutate tenant state",
        "REAL neutral HTTP + MySqlConnector/MySQL8.4",
    ),
    "script": (
        "--network bridge --name",
        "-p '127.0.0.1::3306'",
        "mysql:8.4@sha256:",
        "openssl rand -hex 32",
        "GRANT EXECUTE ON PROCEDURE",
        "GRANT SELECT ON",
        "GRANT INSERT, UPDATE ON",
        "CRECISOL_C05_02_NEUTRAL_TEST_DSN",
        "CRECISOL_C05_02_NEUTRAL_ADMIN_DSN",
        "CRECISOL_C05_02_NEUTRAL_MASTER_HEX",
        "-- --integration",
        "docker rm -f",
    ),
    "document": (
        REPO, BASE, WORD, PDF, C05,
        "NO CERTIFICADA FUNCIONALMENTE",
        "NO EJECUTADAS",
        "AES-256-GCM",
        "SQL SECURITY DEFINER",
        "C05-03",
        "Rollback",
    ),
    "workflow": (
        "contents: read",
        "persist-credentials: false",
        "fetch-depth: 2",
        "dotnet-version: '10.0.x'",
        "qa/c05_02_neutral_ingress_audits.py A",
        "qa/c05_02_neutral_ingress_audits.py B",
        "qa/c05_02_neutral_ingress_audits.py C",
        "bash database/tests/c05_02_neutral_registration_http_mysql.sh",
    ),
}

def need(condition: bool, message: str) -> None:
    if not condition:
        raise AssertionError(message)

def sources() -> dict[str, str]:
    result = {}
    for key, relative in FILES.items():
        path = ROOT / relative
        need(path.is_file(), f"Missing authorized {key}: {relative}")
        result[key] = path.read_text(encoding="utf-8")
    return result

def invariants(data: dict[str,str]) -> None:
    for kind, markers in GUARDS.items():
        for marker in markers:
            need(marker in data[kind], f"Missing fail-closed {kind} guard: {marker}")
    app = data["application"]
    infra = data["infrastructure"]
    sql = data["schema"]
    test = data["test"]
    need("FindByEmailAsync(" not in app and
         "FindAccountByEmailAsync(" not in app and "AccountExistsAsync(" not in app,
         "Account lookup would create a public existence/timing oracle")
    need("HttpContext" not in app and "IConfiguration" not in app,
         "Application cannot trust HTTP object or configuration")
    need("HttpContext" not in infra and "Environment.GetEnvironmentVariable" not in infra,
         "Infrastructure cannot select DSN or key from client request/environment")
    need("INSERT INTO c05_02_neutral_intents" not in infra and
         "SELECT * FROM c05_02_neutral_intents" not in infra,
         "Ingestion adapter MUST ONLY call definer routine without direct SQL read/write")
    need("GRANT ALL" not in data["script"] and
         "GRANT SELECT ON " in data["script"] and
         "GRANT EXECUTE ON PROCEDURE" in data["script"] and
         "c05_02_neutral_ingest'@'%';" in data["script"],
         "Least-privilege local ingestion DB user required")
    need("Email" not in data["schema"] and
         "account_exists" not in sql.lower() and "SELECT " in sql,
         "No existence lookup or raw email columns permitted")
    need("<TargetFramework>net10.0</TargetFramework>" in data["project"] and
         "<TreatWarningsAsErrors>true</TreatWarningsAsErrors>" in data["project"] and
         "Microsoft.AspNetCore.App" in data["project"],
         "Strict real ASP.NET Core 10 test reference required")
    existing_api = (ROOT / "backend/src/Crecisol.Api/Program.cs").read_text(encoding="utf-8")
    routes = re.findall(r'app\.Map(?:Post|Get|Put|Patch|Delete)\(\s*"([^"]+)"', existing_api)
    need(routes == ["/api/v1/public/branding/platform"],
         "Production API must NOT expose signup before identity guards exist")
    need("MapPost(" in test and "/__test_only/" in test and
         "AddTransient<RequestNeutralRegistrationIntent>" in test,
         "Loopback HTTP route must be isolated to test executable")
    wf = data["workflow"]
    need("id-token: write" not in wf and "workflow_dispatch:" not in wf and
         "permissions:\n  contents: read" in wf and
         "deploy" not in wf.lower(),
         "No production deployment or elevated token in CI")
    need("NO CERTIFICADA FUNCIONALMENTE" in data["document"] and
         "NO EJECUTADAS" in data["document"],
         "Neither test nor doc can falsely certify full registration")

def audit_a() -> None:
    data = sources()
    invariants(data)
    print("PASS C05-02 neutral A: canonical Word/PDF/C05, no account enumeration and isolated layers")

def audit_b() -> None:
    data = sources()
    invariants(data)
    rejected = 0
    for kind, markers in GUARDS.items():
        for marker in markers:
            mutation = dict(data)
            mutation[kind] = mutation[kind].replace(marker, "[DELETED_SECURITY_GUARD]")
            need(mutation[kind] != data[kind], "Mutator did not change: " + marker)
            try:
                invariants(mutation)
            except AssertionError:
                rejected += 1
            else:
                raise AssertionError("Adversarial security mutation not rejected: " + marker)
    need(rejected >= 90, "Insufficient independent negative security guard mutations")
    print(f"PASS C05-02 neutral B: {rejected} fail-closed code/SQL/privacy/document mutations")
    print("LIMIT: synthetic HTTP hosts; no real email verification or signup")

def git(*args: str) -> str:
    return subprocess.check_output(
        ["git", *args],cwd=ROOT,text=True,stderr=subprocess.DEVNULL).strip()

def audit_c() -> None:
    need(os.environ.get("GITHUB_ACTIONS") == "true" and
         os.environ.get("GITHUB_REPOSITORY") == REPO, "Real CRECISOL GitHub runner required")
    sha, run = os.environ.get("GITHUB_SHA", ""), os.environ.get("GITHUB_RUN_ID", "")
    need(re.fullmatch("[a-f0-9]{40}",sha) is not None and
         re.fullmatch("[0-9]+",run) is not None and
         git("rev-parse","HEAD") == sha,"Exact source SHA/run required")
    for stage in ("A","B","dotnet","mysql_http"):
        proof = PROOF / stage
        need(proof.is_file() and proof.read_text(encoding="utf-8").strip() ==
             f"PASS {stage} sha={sha} run={run}",
             "Missing independently completed same-SHA stage: "+stage)
    event = os.environ.get("GITHUB_EVENT_NAME")
    if event == "pull_request":
        event_path = Path(os.environ.get("GITHUB_EVENT_PATH",""))
        need(event_path.is_file(),"PR event required")
        pr = json.loads(event_path.read_text(encoding="utf-8"))["pull_request"]
        base, head = pr["base"], pr["head"]
        need(base["ref"] == "dev" and head["ref"] == FEATURE and
             base["repo"]["full_name"] == REPO and
             head["repo"]["full_name"] == REPO,"Unexpected base/fork")
        need(git("rev-list","--parents","-n","1","HEAD").split() ==
             [sha,base["sha"],head["sha"]],
             "Synthetic merge must contain exact base and feature parents")
        changed = git("diff","--name-only",base["sha"],head["sha"]).splitlines()
    else:
        need(event == "push" and os.environ.get("GITHUB_REF") == "refs/heads/dev",
             "Only verified push to dev allowed")
        changed = git("show","--format=","--name-only",sha).splitlines()
    need(bool(changed) and set(changed).issubset(ALLOWED),
         "Scope violation: "+repr(changed))
    need(FILES["application"] in changed and FILES["infrastructure"] in changed,
         "Integration source must belong to exact PR/push diff")
    invariants(sources())
    print(f"PASS C05-02 neutral C: exact SHA={sha} run={run}, A/B/.NET10/REAL HTTP+MySQL")
    print("NO CERTIFICATION: inbox ownership, delivery, real signup, role or session")

if __name__ == "__main__":
    try:
        need(len(sys.argv) == 2 and sys.argv[1] in ("A","B","C"),
             "Usage: c05_02_neutral_ingress_audits.py A|B|C")
        {"A":audit_a,"B":audit_b,"C":audit_c}[sys.argv[1]]()
    except (AssertionError, OSError, ValueError, KeyError, subprocess.SubprocessError) as err:
        print("FAIL C05-02 neutral audit: "+str(err),file=sys.stderr)
        sys.exit(1)
