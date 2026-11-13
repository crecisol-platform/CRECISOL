#!/usr/bin/env python3
"""CRECISOL C05-06 A/B/C scoped test-only recovery; SHA-bound real CI proof."""
from __future__ import annotations

import json
import os
from pathlib import Path
import re
import subprocess
import sys

from c05_01_preparation_audits import ROOT

REPO = "crecisol-platform/CRECISOL"
BRANCH = "feature/c05-06-password-recovery-20261113"
BASE = "f34a751ee10dddc47c8f75fb49291a4741d5cca5"
FILES = {
    "app": "backend/src/Crecisol.Application/Identity/PasswordRecoveryContracts.cs",
    "crypto": "backend/src/Crecisol.CredentialSecurity/PasswordRecoveryCrypto.cs",
    "adapter": "backend/src/Crecisol.Infrastructure/Identity/MySqlDisposablePasswordRecoveryLedger.cs",
    "sql": "database/tests/fixtures/c05_06_disposable_recovery.sql",
    "runner": "database/tests/c05_06_recovery_http_mysql.sh",
    "tests": "backend/tests/Crecisol.PasswordRecovery.MySqlTests/Program.cs",
    "csproj": "backend/tests/Crecisol.PasswordRecovery.MySqlTests/Crecisol.PasswordRecovery.MySqlTests.csproj",
    "doc": "docs/c05/C05_06_ESPECIFICACION.md",
    "qa": "qa/c05_06_recovery_audits.py",
    "ci": ".github/workflows/c05-06-recovery.yml",
}
ALLOWED = set(FILES.values())
PROOF = ROOT / ".c05-06-proof"
GUARDS = {
    "app": (
        "public sealed class PasswordRecoveryTokenDigest",
        "public sealed class PasswordRecoveryMailboxDigest",
        "digest.Length != 32",
        "PasswordRecoveryTokenDigest[redacted]",
        "PasswordRecoveryMailboxDigest[redacted]",
        "IssuedPasswordRecoveryToken[redacted]",
        "public interface ITrustedPasswordRecoveryProtector",
        "public interface ITrustedPasswordRecoveryLedger",
        "public interface ITrustedPasswordRecoveryDeliverySink",
        "public interface ITrustedPasswordRecoveryPasswordEncoder",
        "public sealed class RecoverPasswordOnce",
        "CanonicalLoginHandle mailbox",
        "VerifiedTenantHostHint verifiedHost",
        "protector.DigestCanonicalMailbox(mailbox)",
        "ledger.TryIssueAsync(verifiedHost.TenantId",
        "delivery.DeliverToVerifiedMailboxAsync",
        "if (!issued) return;",
        "catch (Exception) when (!cancellationToken.IsCancellationRequested)",
        "passwords.EncodeNewPassword(newPassword.Span)",
        "ledger.TryConsumeAsync(",
        "Guid.NewGuid()",
        "public override string ToString() => \"RecoverPasswordOnce[redacted]\"",
    ),
    "crypto": (
        "public sealed class PasswordRecoveryTokenProtector",
        "private const string Prefix = \"cr6_\"",
        "RandomNumberGenerator.GetBytes(32)",
        "CryptographicOperations.FixedTimeEquals(tokenHmacKey, mailboxHmacKey)",
        "HMACSHA256.HashData(tokenKey, ascii)",
        "HMACSHA256.HashData(mailboxKey, source)",
        "crecisol:c05-06:mailbox:v1:",
        "CryptographicOperations.ZeroMemory(tokenKey)",
        "CryptographicOperations.ZeroMemory(mailboxKey)",
        "CryptographicOperations.ZeroMemory(parsed)",
        "PasswordRecoveryTokenProtector[redacted]",
        "public sealed class Argon2idRecoveryPasswordEncoder",
        "hasher.CreateVerifier(candidate).ExportForTrustedCredentialStore()",
    ),
    "adapter": (
        "MySqlDisposablePasswordRecoveryLedger",
        "settings.Database != \"c05_05_disposable\"",
        "settings.Server != \"127.0.0.1\"",
        "settings.AllowLoadLocalInfile",
        "Pooling = false",
        "CommandType = CommandType.StoredProcedure",
        "\"c05_06_issue_once\"",
        "\"c05_06_consume_once\"",
        "PasswordRecoveryMailboxDigest verifiedMailbox",
        "PasswordRecoveryTokenDigest digest",
        "newArgon2idPhc.StartsWith(\"$argon2id$v=19$\"",
        "CryptographicOperations.ZeroMemory(mailbox)",
        "CryptographicOperations.ZeroMemory(token)",
        "verifiedTenant.Value",
        "MySqlDisposablePasswordRecoveryLedger[redacted]",
    ),
    "sql": (
        "CREATE TABLE c05_06_credentials",
        "CREATE TABLE c05_06_mailboxes",
        "CREATE TABLE c05_06_challenges",
        "FOREIGN KEY (tenant_id,actor_id)",
        "UNIQUE KEY uq_c05_06_token_digest",
        "UNIQUE KEY uq_c05_06_mailbox",
        "CHECK (state IN ('PENDING','CONSUMED','REVOKED'))",
        "CREATE TRIGGER c05_06_mailbox_revision_bu",
        "CREATE TRIGGER c05_06_subject_revision_bu",
        "CREATE TRIGGER c05_06_credential_revision_bu",
        "CREATE PROCEDURE c05_06_issue_once",
        "CREATE PROCEDURE c05_06_consume_once",
        "SQL SECURITY DEFINER",
        "START TRANSACTION;",
        "ROLLBACK; RESIGNAL;",
        "FOR UPDATE;",
        "UTC_TIMESTAMP(6)",
        "INTERVAL 10 MINUTE",
        "v_verified=1",
        "v_realm='TENANT'",
        "v_global_cred_rev=v_credential_rev",
        "v_global_cred_rev=v_c",
        "v_exp>v_now",
        "v_state='PENDING' AND v_expires>v_now",
        "v_mailbox=v_real_mailbox",
        "v_mailbox_rev=v_real_mail_rev",
        "v_s=v_subject_rev",
        "v_account=1 AND v_credential=1",
        "v_membership=1 AND v_host=1 AND v_tenant=1",
        "WHERE tenant_id=p_tenant AND token_digest=p_token_digest FOR UPDATE",
        "verifier_phc=p_new_verifier",
        "credential_revision=credential_revision+1",
        "WHERE actor_id=v_actor AND revoked_utc IS NULL",
        "credential_revision=v_c+1",
        "subject_revision=subject_revision+1",
        "SET state='CONSUMED', consumed_utc=v_now",
        "WHERE challenge_id=v_challenge",
        "COMMIT;",
    ),
    "runner": (
        "set -euo pipefail",
        "mysql:8.4@sha256:",
        "127.0.0.1::3306",
        "openssl rand -hex 24",
        "database/tests/fixtures/c05_05_disposable_sessions.sql",
        "database/tests/fixtures/c05_06_disposable_recovery.sql",
        "CREATE USER 'c05_06_runner'",
        "GRANT EXECUTE ON PROCEDURE",
        "c05_06_issue_once",
        "c05_06_consume_once",
        "c05_05_is_current",
        "CRECISOL_C05_06_RUNNER_DSN",
        "CRECISOL_C05_06_SETUP_DSN",
        "trap cleanup EXIT",
        "-- --integration",
    ),
    "tests": (
        'args[0] != "--integration"',
        "SKIP C05-06 HTTP/MySQL integration",
        "http://127.0.0.1:0",
        'MapPost("/__c05_06_test__/request"',
        'MapPost("/__c05_06_test__/redeem"',
        "Results.Accepted()",
        "Results.NoContent()",
        "Results.Unauthorized()",
        "HostContextAsync",
        "X-Tenant-Id",
        "X-Forwarded-Host",
        "Referrer-Policy",
        "no-referrer",
        "no-store",
        "password.GetString()",
        "JsonDocument.ParseAsync",
        "PasswordRecoveryTokenProtector(k1, k2)",
        "Argon2idPasswordHasher",
        "PasswordCompromiseVerdict.Unavailable",
        "PasswordCompromiseVerdict.Compromised",
        "Pre-reset sessions A/B and unrelated actor B",
        "Same token cannot be replayed",
        "Once-consumed password reset token can NEVER be reused",
        "Issuing a new recovery link revoked all prior links",
        "Expired challenge cannot be consumed",
        "Off-then-on mailbox re-verification cannot resurrect stale link",
        "Restoring membership cannot resurrect prior challenge",
        "16 concurrent REAL HTTP/MySQL reset attempts: exactly ONE winner",
        "Reset revokes ALL actor sessions A/B",
        "Unrelated B token remains strictly isolated",
        "Procedure runner cannot SELECT",
        "app.StopAsync()",
    ),
    "csproj": (
        "<TargetFramework>net10.0</TargetFramework>",
        "<TreatWarningsAsErrors>true</TreatWarningsAsErrors>",
        '<FrameworkReference Include="Microsoft.AspNetCore.App" />',
        "Crecisol.Infrastructure/Crecisol.Infrastructure.csproj",
        "Crecisol.CredentialSecurity/Crecisol.CredentialSecurity.csproj",
    ),
    "doc": (
        "C05-06",
        "Documento Maestro Único",
        BASE,
        "Recuperación de contraseña",
        "Tokens de un solo uso con expiración y revocación",
        "No reutilizar enlaces",
        "HMAC-SHA256",
        "Argon2id",
        "MySQL 8.4",
        "16",
        "C05-05",
        "C05-07 BLOQUEADA",
        "NO CERTIFICADA",
        "NO-GO",
        "Rollback",
        "No se toca",
    ),
    "ci": (
        "permissions:",
        "contents: read",
        "persist-credentials: false",
        "fetch-depth: 2",
        "dotnet-version: '10.0.x'",
        "qa/c05_06_recovery_audits.py A",
        "qa/c05_06_recovery_audits.py B",
        "qa/c05_06_recovery_audits.py C",
        "Crecisol.PasswordRecovery.MySqlTests.csproj",
        "database/tests/c05_06_recovery_http_mysql.sh",
        "Crecisol.SessionRevocation.MySqlTests.csproj",
        "Crecisol.ContextualLogin.ContractsTests.csproj",
        "Crecisol.PasswordHash.ContractsTests.csproj",
        "Crecisol.AccountIdentity.ContractsTests.csproj",
        "Crecisol.InvitationSecurity.ContractsTests.csproj",
        ".c05-06-proof",
        "GITHUB_SHA",
        "GITHUB_RUN_ID",
    ),
}


def demand(ok: bool, why: str) -> None:
    if not ok:
        raise AssertionError(why)


def sources() -> dict[str, str]:
    return {name: (ROOT / path).read_text(encoding="utf-8")
            for name, path in FILES.items() if name != "qa"}


def check(data: dict[str, str]) -> None:
    for name, markers in GUARDS.items():
        for marker in markers:
            demand(marker in data[name], "Missing C05-06 "+name+" guard: "+marker)
    api = (ROOT / "backend/src/Crecisol.Api/Program.cs").read_text(encoding="utf-8")
    routes = re.findall(r'app\.Map(?:Get|Post|Put|Patch|Delete)\(\s*"([^"]+)"', api)
    demand(routes == ["/api/v1/public/branding/platform"] and
           "RecoverPasswordOnce" not in api and
           "PasswordRecoveryTokenProtector" not in api,
           "Product API must remain PUBLIC BRANDING ONLY")
    infra = (ROOT / "backend/src/Crecisol.Infrastructure/Crecisol.Infrastructure.csproj"
             ).read_text(encoding="utf-8")
    demand('PackageReference Include="MySqlConnector" Version="2.6.2"' in infra,
           "Existing C03-04 MySqlConnector pin required")
    demand("MySqlConnection" not in data["app"] and
           "HttpContext" not in data["app"] and
           "Console.WriteLine" not in data["app"] and
           "Environment.GetEnvironmentVariable" not in data["app"],
           "Clean Application cannot have HTTP/DB/secrets dependencies")
    demand("CommandType.Text" not in data["adapter"] and
           "SELECT * FROM" not in data["adapter"] and
           "CREATE TABLE" not in data["adapter"],
           "Only trusted database procedures may update credentials")
    demand("id-token: write" not in data["ci"] and
           "workflow_dispatch:" not in data["ci"] and
           "deploy" not in data["ci"].lower(),
           "No escalation/production automation")
    demand("GRANT ALL" not in data["runner"] and
           "0.0.0.0::3306" not in data["runner"],
           "No publicly exposed MySQL or privileged fixture runner")


def a() -> None:
    subprocess.run([sys.executable, "qa/master_source_guard.py", "--self-test"],
                   cwd=ROOT, check=True)
    subprocess.run([sys.executable, "qa/c05_05_session_audits.py", "A"],
                   cwd=ROOT, check=True)
    plan = (ROOT / "docs/planificacion-futura/C05_MICROFASES_PROPUESTAS.md"
            ).read_text(encoding="utf-8")
    demand("C05-06 | Recuperación de contraseña" in plan and
           "No reutilizar enlaces" in plan,
           "Canonical derived C05-06 recovery requirements missing")
    check(sources())
    print("PASS CRECISOL C05-06 A: Word, C05-05 precedent and real recovery scope")


def b() -> None:
    original = sources()
    check(original)
    count = 0
    for name, markers in GUARDS.items():
        for marker in markers:
            mutant = dict(original)
            mutant[name] = original[name].replace(marker, "[REMOVED_RECOVERY_GUARD]")
            demand(mutant[name] != original[name], "Ineffective security mutation")
            try:
                check(mutant)
            except AssertionError:
                count += 1
            else:
                raise AssertionError("Adversarial bypass: "+name+" "+marker)
    demand(count >= 100, "Not enough hostile negative cases")
    print(f"PASS CRECISOL C05-06 B: {count} static security guard deletions rejected")
    print("STATIC mutation testing only; real HTTP, Argon2id and MySQL8.4 are separately required")


def git(*args: str) -> str:
    return subprocess.check_output(["git", *args], cwd=ROOT,
                                   text=True, stderr=subprocess.DEVNULL).strip()


def c() -> None:
    demand(os.getenv("GITHUB_ACTIONS") == "true" and
           os.getenv("GITHUB_REPOSITORY") == REPO,
           "Only authenticated CRECISOL GitHub CI may attest")
    sha, run = os.getenv("GITHUB_SHA", ""), os.getenv("GITHUB_RUN_ID", "")
    demand(re.fullmatch("[0-9a-f]{40}", sha) is not None and
           re.fullmatch("[0-9]+", run) is not None and
           git("rev-parse", "HEAD") == sha,
           "Current exact SHA/run is required")
    for stage in ("A", "B", "build", "mysql", "regressions"):
        file = PROOF / stage
        demand(file.is_file() and file.read_text(encoding="utf-8").strip() ==
               f"PASS {stage} sha={sha} run={run}",
               "Missing real same-run proof: "+stage)
    event = os.getenv("GITHUB_EVENT_NAME")
    if event == "pull_request":
        event_file = Path(os.getenv("GITHUB_EVENT_PATH", ""))
        demand(event_file.is_file(), "Missing PR event")
        pr = json.loads(event_file.read_text(encoding="utf-8"))["pull_request"]
        base, head = pr["base"], pr["head"]
        demand(base["ref"] == "dev" and base["sha"] == BASE and
               head["ref"] == BRANCH and
               base["repo"]["full_name"] == REPO and
               head["repo"]["full_name"] == REPO,
               "Unreviewed base/branch/repository")
        demand(git("rev-list", "--parents", "-n", "1", "HEAD").split() ==
               [sha, base["sha"], head["sha"]],
               "Merge ancestors not exact approved SHAs")
        changed = git("diff", "--name-only", base["sha"], head["sha"]).splitlines()
    else:
        demand(event == "push" and os.getenv("GITHUB_REF") == "refs/heads/dev",
               "Only dev POSTMERGE can attest")
        changed = git("show", "--format=", "--name-only", sha).splitlines()
    demand(set(changed) == ALLOWED and len(changed) == len(ALLOWED),
           "Unapproved or missing C05-06 files: "+repr(changed))
    check(sources())
    print(f"PASS CRECISOL C05-06 C: A/B real MySQL8.4 HTTP .NET10 and regressions sha={sha} run={run}")
    print("DOES NOT CLAIM SMTP, account proof, production login, MFA reset or live sessions")


if __name__ == "__main__":
    try:
        demand(len(sys.argv) == 2 and sys.argv[1] in ("A","B","C"),
               "Usage: qa/c05_06_recovery_audits.py A|B|C")
        {"A": a, "B": b, "C": c}[sys.argv[1]]()
    except (AssertionError, OSError, ValueError, KeyError,
            subprocess.SubprocessError) as exc:
        print("FAIL CRECISOL C05-06 audit: "+str(exc), file=sys.stderr)
        sys.exit(1)
