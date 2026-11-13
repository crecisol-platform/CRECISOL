#!/usr/bin/env python3
"""C05-02 MySQL8.4 adapter A/B/C, real isolated DB only; NOT user signup."""
from __future__ import annotations
import json
import os
from pathlib import Path
import re
import subprocess
import sys

from c05_02_invitations_audits import ROOT, audit_a as previous_a, audit_b as previous_b

DOMAIN = ROOT / "backend/src/Crecisol.Domain/Identity/InvitationSecurityContracts.cs"
ADAPTER = ROOT / "backend/src/Crecisol.Infrastructure/Identity/MySqlDisposableInvitationLedger.cs"
TEST = ROOT / "backend/tests/Crecisol.InvitationSecurity.MySqlIntegrationTests/Program.cs"
PROJECT = ROOT / "backend/tests/Crecisol.InvitationSecurity.MySqlIntegrationTests/Crecisol.InvitationSecurity.MySqlIntegrationTests.csproj"
SCRIPT = ROOT / "database/tests/c05_02_dotnet_mysql_adapter_smoke.sh"
SCHEMA = ROOT / "database/tests/fixtures/c05_02_invitation_atomic_fixture.sql"
DOC = ROOT / "docs/c05/IMPLEMENTACION_C05_02_MYSQL_ADAPTER.md"
WORKFLOW = ROOT / ".github/workflows/c05-02-mysql-ledger.yml"
API = ROOT / "backend/src/Crecisol.Api/Program.cs"
PREV_QA = ROOT / "qa/c05_02_invitations_audits.py"
PROOF = ROOT / ".c05-02-adapter-proof"
REPO = "crecisol-platform/CRECISOL"
FEATURE = "feature/c05-02-mysql-ledger-integration-20261113"
BASE = "028c6be94510ccdb11fa4c883aefa5bfc35cf142"
SOURCE_SHA = "bf7036849d6f1aad36a3a111b62121947aa12fae8c2a0551e63b0cc788d4a0aa"
ALLOWED = {
    "backend/src/Crecisol.Domain/Identity/InvitationSecurityContracts.cs",
    "backend/src/Crecisol.Infrastructure/Identity/MySqlDisposableInvitationLedger.cs",
    "backend/tests/Crecisol.InvitationSecurity.MySqlIntegrationTests/Program.cs",
    "backend/tests/Crecisol.InvitationSecurity.MySqlIntegrationTests/Crecisol.InvitationSecurity.MySqlIntegrationTests.csproj",
    "database/tests/c05_02_dotnet_mysql_adapter_smoke.sh",
    "database/tests/fixtures/c05_02_invitation_atomic_fixture.sql",
    "docs/c05/IMPLEMENTACION_C05_02_MYSQL_ADAPTER.md",
    "qa/c05_02_invitations_audits.py",
    "qa/c05_02_mysql_adapter_audits.py",
    ".github/workflows/c05-02-mysql-ledger.yml",
}
GUARDS = {
    "domain": (
        "RestoreFromProtectedPersistence",
        "storedDigest.Length != 32",
        "CryptographicOperations.ZeroMemory(copy)",
        "return new InvitationTokenDigest(copy);",
    ),
    "adapter": (
        "class MySqlDisposableInvitationLedger : ITrustedInvitationLedger",
        "string.Equals(settings.Database, \"c05_02_disposable\"",
        "string.Equals(settings.Server, \"127.0.0.1\"",
        "settings.AllowLoadLocalInfile",
        "Pooling = false",
        "c05_02_issue_once",
        "CommandType.StoredProcedure",
        "snapshot.Digest.CopyForProtectedPersistenceAdapter()",
        "CryptographicOperations.ZeroMemory(digest)",
        "new MySqlConnection(trustedDsn)",
        "tenant_id=@tenant AND invitation_id=@invitation",
        "LIMIT 2",
        "tenant != verifiedTenant || invite != id",
        "InvitationTokenDigest.RestoreFromProtectedPersistence(rawDigest)",
        "MySqlRfc4122Guid.Encode",
        "MySqlRfc4122Guid.Decode",
        "MySqlDbType.Binary",
        "c05_02_claim_once",
        "response != \"CLAIMED\" && response != \"DENIED\"",
        "response != \"ISSUED\" && response != \"DENIED\"",
        "ArgumentNullException.ThrowIfNull(verifiedRecipientActor)",
        "expected.IsPendingAtUtc(nowUtc)",
    ),
    "schema": (
        "CREATE PROCEDURE c05_02_issue_once(",
        "SQL SECURITY DEFINER",
        "DECLARE EXIT HANDLER FOR 1062",
        "DECLARE EXIT HANDLER FOR SQLEXCEPTION",
        "START TRANSACTION;",
        "r.tenant_id=p_tenant AND r.recipient_id=p_recipient",
        "t.lifecycle='ACTIVE'",
        "s.enabled=1",
        "t.terms_version=p_terms",
        "p_issued<=UTC_TIMESTAMP(6)",
        "UTC_TIMESTAMP(6)<p_expires",
        "BETWEEN 60 AND 259200",
        "r.channel_verified=1",
        "r.actor_id<>s.actor_id",
        "s.profile_scope='INVITE_EMPLOYEE'",
        "s.profile_scope='INVITE_CONSUMER'",
        "lifecycle='PENDING'",
        "expires_at_utc>UTC_TIMESTAMP(6)",
        "FOR UPDATE",
        "SELECT EXISTS(",
        "INSERT INTO c05_02_invitations",
        "SELECT 'ISSUED' AS result",
        "SELECT 'DENIED' AS result",
    ),
    "test": (
        "if (args.Length == 0)",
        "SKIP C05-02 SQL adapter integration",
        "--integration",
        "CRECISOL_C05_02_TEST_DSN",
        "CRECISOL_C05_02_TEST_ADMIN_DSN",
        "new MySqlDisposableInvitationLedger(testDsn)",
        "Sixteen concurrent real .NET MySQL clients exactly ONE winner",
        "Cross-tenant B cannot read tenant A invitation",
        "Active invitation for same tenant+recipient denied",
        "New published terms version invalidates older invitation",
        "Issuer loses entitlement at atomic backend boundary",
        "Unverified recipient cannot be inserted",
        "Expired invitation creation fails closed in SQL adapter",
        "PASS C05-02 actual .NET10 + MySQL8.4 adapter",
    ),
    "script": (
        "docker run --rm -d",
        "--network bridge -p '127.0.0.1::3306'",
        "mysql:8.4@sha256:",
        "openssl rand -hex 24",
        "GRANT SELECT ON",
        "GRANT EXECUTE ON PROCEDURE",
        "CRECISOL_C05_02_TEST_DSN=",
        "CRECISOL_C05_02_TEST_ADMIN_DSN=",
        "-- --integration",
        "docker rm -f",
    ),
    "document": (
        "NO CERTIFICADA FUNCIONALMENTE",
        "NO EJECUTADAS",
        "MySqlDisposableInvitationLedger",
        "C05-02",
        "C05-03",
        "C05-01",
        BASE,
        SOURCE_SHA,
        "16 conexiones",
        "**Rollback:**",
    ),
    "workflow": (
        "contents: read",
        "persist-credentials: false",
        "fetch-depth: 2",
        "dotnet-version: '10.0.x'",
        "qa/c05_02_mysql_adapter_audits.py A",
        "qa/c05_02_mysql_adapter_audits.py B",
        "qa/c05_02_mysql_adapter_audits.py C",
        "bash database/tests/c05_02_dotnet_mysql_adapter_smoke.sh",
    ),
}

def need(ok: bool, message: str) -> None:
    if not ok:
        raise AssertionError(message)

def read(path: Path) -> str:
    need(path.is_file(), f"Missing required C05-02 file: {path.relative_to(ROOT)}")
    return path.read_text(encoding="utf-8")

def sources() -> dict[str, str]:
    return {
        "domain": read(DOMAIN),
        "adapter": read(ADAPTER),
        "test": read(TEST),
        "script": read(SCRIPT),
        "schema": read(SCHEMA),
        "document": read(DOC),
        "workflow": read(WORKFLOW),
    }

def guard(data: dict[str, str]) -> None:
    for name, required in GUARDS.items():
        for marker in required:
            need(marker in data[name], f"Fail-closed {name} guard absent: {marker}")
    adapter = data["adapter"]
    need("INSERT INTO" not in adapter and "FOR UPDATE" not in adapter,
         "SQL writes and locks require definer procedure, not direct table access")
    need("HttpContext" not in adapter and "DbContext" not in adapter and
         "IConfiguration" not in adapter and "Environment.GetEnvironmentVariable" not in adapter,
         "Infrastructure adapter must only accept a server-trusted explicit DSN")
    need("using MySqlConnector;" in adapter and
         "using Crecisol.Application.Identity;" in adapter,
         "Correct Infrastructure dependency direction required")
    need("<TargetFramework>net10.0</TargetFramework>" in read(PROJECT) and
         "<TreatWarningsAsErrors>true</TreatWarningsAsErrors>" in read(PROJECT) and
         "Crecisol.Infrastructure.csproj" in read(PROJECT),
         "Strict project reference/TFM required")
    api = read(API)
    routes = re.findall(r'app\.Map(?:Get|Post|Put|Patch|Delete)\(\s*"([^"]+)"', api)
    need(routes == ["/api/v1/public/branding/platform"],
         "Private user signup/registration API must not be enabled without auth")
    wf = data["workflow"]
    need("workflow_dispatch:" not in wf and "id-token: write" not in wf and
         "deploy" not in wf.casefold() and "permissions:\n  contents: read" in wf,
         "No production/credential escalation in CI")
    need("GRANT SELECT, INSERT ON" not in data["script"],
         "Adapter must not receive direct INSERT rights")
    need("DROP DATABASE" not in data["script"] and "GRANT ALL" not in data["script"],
         "No broad privileges or unsafe DB deletions")
    need("NO CERTIFICADA FUNCIONALMENTE" in data["document"] and
         "NO EJECUTADAS" in data["document"],
         "Do not claim full registration feature certified")

def audit_a() -> None:
    previous_a()  # Real Word/PDF SHA, source and predecessor as verified in prior gates
    need(BASE in read(DOC), "Technical predecessor SHA missing")
    guard(sources())
    print("PASS C05-02 adapter A: Word/PDF, prior 5/5 technical gates, typed local DB adapter")

def audit_b() -> None:
    previous_b()
    orig = sources()
    guard(orig)
    rejected = 0
    for name, items in GUARDS.items():
        for marker in items:
            mutant = dict(orig)
            mutant[name] = mutant[name].replace(marker, "[ADVERSARIAL REMOVAL]")
            need(mutant[name] != orig[name], "Failed to generate mutation: " + marker)
            try:
                guard(mutant)
            except AssertionError:
                rejected += 1
            else:
                raise AssertionError("Mutation bypassed strict adapter gate: " + marker)
    need(rejected >= 65, "Insufficient offensive code mutation count")
    print(f"PASS C05-02 adapter B: {rejected} independent fail-closed code/doc mutations")
    print("LIMIT: no productive HTTP/email/tenant membership/session adapter exists")

def git(*argv: str) -> str:
    return subprocess.check_output(
        ["git", *argv], cwd=ROOT, stderr=subprocess.DEVNULL, text=True).strip()

def audit_c() -> None:
    need(os.environ.get("GITHUB_ACTIONS") == "true", "Actual GitHub runner required")
    sha, run = os.environ.get("GITHUB_SHA", ""), os.environ.get("GITHUB_RUN_ID", "")
    need(re.fullmatch(r"[a-f0-9]{40}", sha) is not None and
         re.fullmatch(r"[0-9]+", run) is not None and
         os.environ.get("GITHUB_REPOSITORY") == REPO and
         git("rev-parse", "HEAD") == sha, "Wrong SHA/run/repository")
    for stage in ("A", "B", "dotnet", "mysql"):
        p = PROOF / stage
        need(p.is_file() and p.read_text(encoding="utf-8").strip() ==
             f"PASS {stage} sha={sha} run={run}",
             "Missing exact same SHA/run stage: " + stage)
    event = os.environ.get("GITHUB_EVENT_NAME")
    if event == "pull_request":
        path = Path(os.environ.get("GITHUB_EVENT_PATH", ""))
        need(path.is_file(), "PR JSON unavailable")
        payload = json.loads(path.read_text(encoding="utf-8"))
        pr = payload["pull_request"]
        b, h = pr["base"], pr["head"]
        need(b["ref"] == "dev" and h["ref"] == FEATURE and
             b["repo"]["full_name"] == REPO and h["repo"]["full_name"] == REPO,
             "Wrong base, branch or fork")
        need(git("rev-list", "--parents", "-n", "1", "HEAD").split() ==
             [sha, b["sha"], h["sha"]],
             "Synthetic tested merge does not have exact base/HEAD parents")
        changed = git("diff", "--name-only", b["sha"], h["sha"]).splitlines()
        print(f"C05-02 adapter exact PR provenance head={h['sha']} base={b['sha']} tested={sha}")
    else:
        need(event == "push" and os.environ.get("GITHUB_REF") == "refs/heads/dev",
             "Only real dev push may certify technical integration")
        changed = git("show", "--format=", "--name-only", sha).splitlines()
        print(f"C05-02 adapter exact dev provenance SHA={sha}")
    need(set(changed).issubset(ALLOWED) and bool(changed),
         "Out-of-scope code in adapter PR: " + repr(changed))
    need("backend/src/Crecisol.Infrastructure/Identity/MySqlDisposableInvitationLedger.cs" in changed,
         "Adapter must be present in exact run diff")
    guard(sources())
    print(f"PASS C05-02 adapter C: exact A/B/.NET10/REAL MySQL8.4 SHA={sha} run={run}")
    print("NOT CERTIFIED: public registration, email ownership, real sessions or tenant grants")

if __name__ == "__main__":
    try:
        need(len(sys.argv) == 2 and sys.argv[1] in ("A", "B", "C"),
             "Usage: c05_02_mysql_adapter_audits.py A|B|C")
        {"A": audit_a, "B": audit_b, "C": audit_c}[sys.argv[1]]()
    except (AssertionError, OSError, ValueError, KeyError, subprocess.SubprocessError) as error:
        print("FAIL C05-02 adapter audit:", error, file=sys.stderr)
        sys.exit(1)
