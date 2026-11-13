#!/usr/bin/env python3
"""CRECISOL C05-05 A/B/C: real isolated HTTP/MySQL revocation, no production."""
from __future__ import annotations

import json
import os
from pathlib import Path
import re
import subprocess
import sys

from c05_01_preparation_audits import ROOT

REPO = "crecisol-platform/CRECISOL"
FEATURE = "feature/c05-05-session-revocation-20261113"
BASE = "358df52d785cb4074790ce7841c5c8d0d4873908"
FILES = {
    "app": "backend/src/Crecisol.Application/Identity/SessionLifecycleContracts.cs",
    "crypto": "backend/src/Crecisol.CredentialSecurity/OpaqueSessionTokenProtector.cs",
    "adapter": "backend/src/Crecisol.Infrastructure/Identity/MySqlDisposableSessionLedger.cs",
    "sql": "database/tests/fixtures/c05_05_disposable_sessions.sql",
    "runner": "database/tests/c05_05_session_http_mysql.sh",
    "test": "backend/tests/Crecisol.SessionRevocation.MySqlTests/Program.cs",
    "testproj": "backend/tests/Crecisol.SessionRevocation.MySqlTests/Crecisol.SessionRevocation.MySqlTests.csproj",
    "doc": "docs/c05/C05_05_ESPECIFICACION.md",
    "qa": "qa/c05_05_session_audits.py",
    "workflow": ".github/workflows/c05-05-sessions.yml",
}
ALLOWED = set(FILES.values())
PROOF = ROOT / ".c05-05-proof"
GUARDS = {
    "app": (
        "public sealed class SessionTokenDigest",
        "FromTrustedTokenProtector(ReadOnlySpan<byte> digest)",
        "digest.Length != 32",
        "SessionTokenDigest[redacted]",
        "public sealed class IssuedSessionToken",
        "private readonly string rawToken",
        "IssuedSessionToken[redacted]",
        "public interface ITrustedOpaqueSessionTokenProtector",
        "public interface ITrustedSessionLifecycleLedger",
        "Task<bool> TryIssueAsync",
        "Task<bool> IsCurrentAsync",
        "Task<bool> TryRotateAsync",
        "Task<bool> TryRevokeAsync",
        "public sealed class ScopedSessionLifecycle",
        "ContextualLoginPreparationStatus.Ready",
        "Guid.NewGuid()",
        "verifiedPreSession",
        "VerifiedTenantHostHint trustedHost",
        "ledger.IsCurrentAsync",
        "ledger.TryRotateAsync",
        "ledger.TryRevokeAsync",
    ),
    "crypto": (
        "RandomNumberGenerator.GetBytes(TokenBytes)",
        "private const int TokenBytes = 32",
        'private const string Prefix = "cs5_"',
        "serverKey.Length is < 32 or > 64",
        "serverKey.IndexOfAnyExcept((byte)0)",
        "HMACSHA256.HashData(key, raw)",
        "CryptographicOperations.ZeroMemory(key)",
        "CryptographicOperations.ZeroMemory(random)",
        "CryptographicOperations.ZeroMemory(decoded)",
        "encoded.ToString().All",
        "decoded.Length != TokenBytes",
        "IssuedSessionToken(Prefix + ToBase64Url(random))",
        "OpaqueSessionTokenProtector[redacted]",
    ),
    "adapter": (
        "public sealed class MySqlDisposableSessionLedger",
        'config.Database != "c05_05_disposable"',
        'config.Server != "127.0.0.1"',
        "config.AllowLoadLocalInfile",
        "Pooling = false",
        "CommandType = CommandType.StoredProcedure",
        '"c05_05_issue_once"',
        '"c05_05_is_current"',
        '"c05_05_rotate_once"',
        '"c05_05_logout_once"',
        "prepared.AccountRevision",
        "prepared.CredentialRevision",
        "prepared.MembershipRevision",
        "prepared.TenantCatalogRevision",
        "prepared.HostRevision",
        "CryptographicOperations.FixedTimeEquals",
        "CryptographicOperations.ZeroMemory(bytes)",
        "CryptographicOperations.ZeroMemory(oldBytes)",
        "CryptographicOperations.ZeroMemory(newBytes)",
        "prepared.TenantId.Value",
        "verifiedTenant.Value",
        "MySqlDisposableSessionLedger[redacted]",
    ),
    "sql": (
        "CREATE TABLE c05_05_subjects",
        "CREATE TABLE c05_05_sessions",
        "ENGINE=InnoDB",
        "BINARY(32) NOT NULL",
        "token_digest",
        "UNIQUE KEY uq_c05_05_current_digest",
        "FOREIGN KEY (tenant_id, actor_id)",
        "ON DELETE RESTRICT ON UPDATE RESTRICT",
        "CREATE PROCEDURE c05_05_issue_once",
        "CREATE PROCEDURE c05_05_is_current",
        "CREATE PROCEDURE c05_05_rotate_once",
        "CREATE PROCEDURE c05_05_logout_once",
        "SQL SECURITY DEFINER",
        "START TRANSACTION;",
        "FOR UPDATE;",
        "UTC_TIMESTAMP(6)",
        "v_a=p_account_revision",
        "v_c=p_credential_revision",
        "v_m=p_membership_revision",
        "v_t=p_tenant_revision",
        "p_host_revision=0 OR v_h=p_host_revision",
        "v_account=1 AND v_credential=1 AND v_membership=1",
        "v_host=1 AND v_tenant=1",
        "v_now<v_absolute",
        "v_session_revision=v_current_revision",
        "token_digest=p_new",
        "generation=generation+1",
        "revoked_utc=COALESCE(revoked_utc,v_now)",
        "revoked_utc=v_now",
        "ROW_COUNT()=1",
        "COMMIT;",
        "ROLLBACK;",
    ),
    "runner": (
        "set -euo pipefail",
        "mysql:8.4@sha256:",
        "127.0.0.1::3306",
        "openssl rand -hex 24",
        "CREATE USER 'c05_05_runner'",
        "GRANT EXECUTE ON PROCEDURE",
        "c05_05_issue_once",
        "c05_05_is_current",
        "c05_05_rotate_once",
        "c05_05_logout_once",
        "CRECISOL_C05_05_TEST_DSN",
        "CRECISOL_C05_05_SETUP_DSN",
        "-- --integration",
        "trap cleanup EXIT",
    ),
    "testproj": (
        '<TargetFramework>net10.0</TargetFramework>',
        '<TreatWarningsAsErrors>true</TreatWarningsAsErrors>',
        '<FrameworkReference Include="Microsoft.AspNetCore.App" />',
        "Crecisol.Infrastructure/Crecisol.Infrastructure.csproj",
        "Crecisol.CredentialSecurity/Crecisol.CredentialSecurity.csproj",
    ),
    "test": (
        'args[0] != "--integration"',
        "USELESS_C05_05_PLACEHOLDER_REPLACED",
        "UseUrls(",
        'http://127.0.0.1:0',
        'MapPost("/__c05_05_test__/login"',
        'MapGet("/__c05_05_test__/validate"',
        'MapPost("/__c05_05_test__/rotate"',
        'MapPost("/__c05_05_test__/logout"',
        "TrustedHostAsync",
        "CanonicalTenantHost.Parse",
        "host != hostA && host != hostB",
        "X-Tenant-Id",
        "Forwarded",
        "ContextualLoginPreparationStatus.Ready",
        "HttpStatusCode.Unauthorized",
        "HttpStatusCode.NoContent",
        "16 concurrent real HTTP+InnoDB rotations yield one winner",
        "Credential revoked in MySQL denies existing session",
        "Reactivating credential does NOT resurrect revoked session",
        "Unknown/random forged bearer is denied",
        "Old token invalid after atomic rotation",
        "Logout really revokes token immediately in MySQL",
        "Host B cannot validate bearer minted under tenant A",
        "No plaintext token, email, password, cookie, JWT or secret DB columns",
        "DB runner has EXECUTE only",
        "CryptographicOperations.ZeroMemory(key)",
        "app.StopAsync()",
    ),
    "doc": (
        "C05-05",
        "C05-04",
        BASE,
        "Documento Maestro Único",
        "Rotación y logout verificables en API",
        "Token revocado no vuelve a funcionar",
        "MySQL 8.4",
        "Argon2id",
        "HMAC-SHA256",
        "16 rotaciones concurrentes",
        "NO CERTIFICADA",
        "C05-06 BLOQUEADA",
        "Rollback",
        "NO-GO",
    ),
    "workflow": (
        "permissions:",
        "contents: read",
        "persist-credentials: false",
        "fetch-depth: 2",
        "dotnet-version: '10.0.x'",
        "qa/c05_05_session_audits.py A",
        "qa/c05_05_session_audits.py B",
        "qa/c05_05_session_audits.py C",
        "Crecisol.SessionRevocation.MySqlTests.csproj",
        "database/tests/c05_05_session_http_mysql.sh",
        "Crecisol.ContextualLogin.ContractsTests.csproj",
        "Crecisol.PasswordHash.ContractsTests.csproj",
        "Crecisol.AccountIdentity.ContractsTests.csproj",
        "Crecisol.InvitationSecurity.ContractsTests.csproj",
        ".c05-05-proof",
        "GITHUB_SHA",
        "GITHUB_RUN_ID",
    ),
}
GUARDS["test"] = tuple(
    "SKIP C05-05 HTTP/MySQL integration" if x == "USELESS_C05_05_PLACEHOLDER_REPLACED" else x
    for x in GUARDS["test"])


def need(ok: bool, why: str) -> None:
    if not ok:
        raise AssertionError(why)


def sources() -> dict[str, str]:
    return {name: (ROOT / path).read_text(encoding="utf-8")
            for name, path in FILES.items() if name != "qa"}


def invariants(data: dict[str, str]) -> None:
    for name, markers in GUARDS.items():
        for marker in markers:
            need(marker in data[name], f"Missing C05-05 {name} security gate: {marker}")
    product_api = (ROOT / "backend/src/Crecisol.Api/Program.cs").read_text(encoding="utf-8")
    actual_routes = re.findall(
        r'app\.Map(?:Get|Post|Put|Patch|Delete)\(\s*"([^"]+)"', product_api)
    need(actual_routes == ["/api/v1/public/branding/platform"],
         "No production session or private API may be exposed in C05-05")
    need("MySqlDisposableSessionLedger" not in product_api and
         "ScopedSessionLifecycle" not in product_api,
         "No production DI registrations or bearer issued to clients")
    legacy = (ROOT / "backend/src/Crecisol.Infrastructure/Crecisol.Infrastructure.csproj"
              ).read_text(encoding="utf-8")
    need('PackageReference Include="MySqlConnector" Version="2.6.2"' in legacy
         and "Konscious.Security.Cryptography.Argon2" not in legacy,
         "Historic C03-04 Infrastructure dependency pin unchanged")
    need("MySqlConnection" not in data["app"] and
         "HttpContext" not in data["app"] and
         "Environment.GetEnvironmentVariable" not in data["app"] and
         "Server=" not in data["app"] and
         "Console.WriteLine" not in data["app"],
         "Application must not depend on MySQL, runtime configuration or HTTP")
    need("CREATE TABLE" not in data["adapter"] and
         "SELECT * FROM" not in data["adapter"] and
         "CommandType.Text" not in data["adapter"],
         "Adapter must use only named protected SQL procedures")
    need("id-token: write" not in data["workflow"] and
         "workflow_dispatch:" not in data["workflow"] and
         "deploy" not in data["workflow"].lower(),
         "No privileged CI or deployment")
    need("GRANT ALL" not in data["runner"] and
         "GRANT SELECT ON" not in data["runner"] and
         "0.0.0.0::3306" not in data["runner"],
         "Disposable runner must lack direct access or public port")


def audit_a() -> None:
    subprocess.run([sys.executable, "qa/master_source_guard.py", "--self-test"],
                   cwd=ROOT, check=True)
    subprocess.run([sys.executable, "qa/c05_04_contextual_login_audits.py", "A"],
                   cwd=ROOT, check=True)
    plan = (ROOT / "docs/planificacion-futura/C05_MICROFASES_PROPUESTAS.md"
            ).read_text(encoding="utf-8")
    need("C05-05 | Sesiones y revocación" in plan and
         "Token revocado no vuelve a funcionar" in plan,
         "C05-05 user requirements must match canonical derived plan")
    invariants(sources())
    print("PASS CRECISOL C05-05 A: canonical Word, C05-04 precedent and real HTTP/MySQL session requirements")


def audit_b() -> None:
    original = sources()
    invariants(original)
    rejected = 0
    for name, markers in GUARDS.items():
        for marker in markers:
            mutant = dict(original)
            mutant[name] = original[name].replace(marker, "[REMOVED_C05_05_GUARD]")
            need(mutant[name] != original[name], "Ineffective mutation")
            try:
                invariants(mutant)
            except AssertionError:
                rejected += 1
            else:
                raise AssertionError(f"Failed to reject {name} mutation of {marker}")
    need(rejected >= 80, "At least 80 hostile guard deletions must fail")
    print(f"PASS CRECISOL C05-05 B: {rejected} static adversarial guard mutations rejected")
    print("These are static checks; real .NET10, MySQL8.4 and HTTP tests execute separately")


def git(*args: str) -> str:
    return subprocess.check_output(["git", *args], cwd=ROOT,
                                   text=True, stderr=subprocess.DEVNULL).strip()


def audit_c() -> None:
    need(os.getenv("GITHUB_ACTIONS") == "true" and
         os.getenv("GITHUB_REPOSITORY") == REPO,
         "C05-05 institutional evidence requires CRECISOL GitHub CI")
    sha, run = os.getenv("GITHUB_SHA", ""), os.getenv("GITHUB_RUN_ID", "")
    need(re.fullmatch("[0-9a-f]{40}", sha) is not None and
         re.fullmatch("[0-9]+", run) is not None and
         git("rev-parse", "HEAD") == sha, "Exact SHA/run lineage required")
    for stage in ("A", "B", "build", "mysql", "regressions"):
        file = PROOF / stage
        need(file.is_file() and file.read_text(encoding="utf-8").strip() ==
             f"PASS {stage} sha={sha} run={run}",
             "Missing same SHA/run evidence: "+stage)

    event = os.getenv("GITHUB_EVENT_NAME")
    if event == "pull_request":
        event_path = Path(os.getenv("GITHUB_EVENT_PATH", ""))
        need(event_path.is_file(), "No PR lineage event")
        data = json.loads(event_path.read_text(encoding="utf-8"))["pull_request"]
        base, head = data["base"], data["head"]
        need(base["ref"] == "dev" and base["sha"] == BASE and
             head["ref"] == FEATURE and
             base["repo"]["full_name"] == REPO and
             head["repo"]["full_name"] == REPO,
             "Incorrect branch, base SHA or repository")
        need(git("rev-list", "--parents", "-n", "1", "HEAD").split() ==
             [sha, base["sha"], head["sha"]],
             "Synthetic PR merge parents differ")
        changed = git("diff", "--name-only", base["sha"], head["sha"]).splitlines()
    else:
        need(event == "push" and os.getenv("GITHUB_REF") == "refs/heads/dev",
             "Only controlled dev POSTMERGE can attest")
        changed = git("show", "--format=", "--name-only", sha).splitlines()
    need(set(changed) == ALLOWED and len(changed) == len(ALLOWED),
         "Missing or unauthorized C05-05 files: " + repr(changed))
    invariants(sources())
    print(f"PASS CRECISOL C05-05 C: strict SHA/run and real HTTP/MySQL8.4 proof sha={sha} run={run}")
    print("ONLY disposable session contracts; no production login, sessions or tenant authorization")


if __name__ == "__main__":
    try:
        need(len(sys.argv) == 2 and sys.argv[1] in ("A", "B", "C"),
             "Usage: qa/c05_05_session_audits.py A|B|C")
        {"A": audit_a, "B": audit_b, "C": audit_c}[sys.argv[1]]()
    except (AssertionError, OSError, KeyError, ValueError,
            subprocess.SubprocessError) as exc:
        print("FAIL CRECISOL C05-05 audit: " + str(exc), file=sys.stderr)
        sys.exit(1)
