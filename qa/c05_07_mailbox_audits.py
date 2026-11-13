#!/usr/bin/env python3
"""CRECISOL C05-07: scoped email proof / two-confirmation A-B-C gates."""
from __future__ import annotations

import json
import os
from pathlib import Path
import re
import subprocess
import sys

from c05_01_preparation_audits import ROOT

REPO = "crecisol-platform/CRECISOL"
BRANCH = "feature/c05-07-mailbox-sensitive-changes-20261113"
BASE = "4a97dbd29b3acc93e9c20ae09e0e26945af98ea0"
FILES = {
    "app": "backend/src/Crecisol.Application/Identity/SensitiveMailboxVerification.cs",
    "crypto": "backend/src/Crecisol.CredentialSecurity/MailboxApprovalTokenProtector.cs",
    "adapter": "backend/src/Crecisol.Infrastructure/Identity/MySqlDisposableSensitiveMailboxLedger.cs",
    "sql": "database/tests/fixtures/c05_07_disposable_mailbox.sql",
    "runner": "database/tests/c05_07_mailbox_http_mysql.sh",
    "tests": "backend/tests/Crecisol.MailboxApproval.MySqlTests/Program.cs",
    "csproj": "backend/tests/Crecisol.MailboxApproval.MySqlTests/Crecisol.MailboxApproval.MySqlTests.csproj",
    "doc": "docs/c05/C05_07_ESPECIFICACION.md",
    "qa": "qa/c05_07_mailbox_audits.py",
    "ci": ".github/workflows/c05-07-mailbox.yml",
}
ALLOWED = set(FILES.values())
PROOF = ROOT / ".c05-07-proof"

GUARDS = {
    "app": (
        "public sealed class MailboxApprovalDigest",
        "value.Length != 32",
        "MailboxApprovalDigest[redacted]",
        "public sealed class IssuedMailboxApproval",
        "cv7_",
        "IssuedMailboxApproval[redacted]",
        "public enum TrustedMailboxProofDeliveryKind",
        "VerifyRegisteredMailbox",
        "ConfirmPreviousMailbox",
        "ConfirmReplacementMailbox",
        "public interface ITrustedMailboxApprovalProtector",
        "public interface ITrustedMailboxApprovalDelivery",
        "public interface ITrustedSensitiveMailboxLedger",
        "public sealed class VerifyAndChangeSensitiveMailbox",
        "ContextualLoginPreparationStatus.Ready",
        "proof.TenantId != verifiedHost.TenantId",
        "proof.HostRevision != verifiedHost.Revision",
        "proof.HostRevision < 1",
        "previousMailbox.Value == nextMailbox.Value",
        "mailboxDigest.DigestCanonicalMailbox",
        "ledger.TryBeginVerificationAsync",
        "ledger.TryVerifyAsync",
        "ledger.TryBeginChangeAsync",
        "ledger.TryConfirmChangeAsync",
        "TrustedMailboxProofDeliveryKind.ConfirmPreviousMailbox",
        "TrustedMailboxProofDeliveryKind.ConfirmReplacementMailbox",
        "if (!await ledger.TryBeginVerificationAsync",
        "return digest is not null &&",
        "VerifyAndChangeSensitiveMailbox[redacted]",
    ),
    "crypto": (
        "public sealed class MailboxApprovalTokenProtector",
        'private const string Prefix = "cv7_"',
        "RandomNumberGenerator.GetBytes(32)",
        "serverHeldKey.Length is < 32 or > 64",
        "serverHeldKey.IndexOfAnyExcept((byte)0)",
        "HMACSHA256.HashData(secret, ascii)",
        "CryptographicOperations.ZeroMemory(bytes)",
        "CryptographicOperations.ZeroMemory(decoded)",
        "CryptographicOperations.ZeroMemory(secret)",
        "Convert.FromBase64String",
        "MailboxApprovalTokenProtector[redacted]",
        "ObjectDisposedException.ThrowIf(disposed, this)",
    ),
    "adapter": (
        "public sealed class MySqlDisposableSensitiveMailboxLedger",
        'options.Server != "127.0.0.1"',
        'options.Database != "c05_05_disposable"',
        "options.AllowLoadLocalInfile",
        "Pooling = false",
        "CommandType = CommandType.StoredProcedure",
        '"c05_07_begin_verify"',
        '"c05_07_confirm_verify"',
        '"c05_07_begin_change"',
        '"c05_07_confirm_change"',
        "proof.ActorId.Value",
        "proof.LinkId.Value",
        "proof.CredentialRevision",
        "proof.HostRevision",
        "CryptographicOperations.FixedTimeEquals",
        "CryptographicOperations.ZeroMemory(oldMail)",
        "CryptographicOperations.ZeroMemory(newToken)",
        "return result is \"RECORDED\" or \"APPLIED\"",
        "MySqlDisposableSensitiveMailboxLedger[redacted]",
    ),
    "sql": (
        "CREATE TABLE c05_07_verifications",
        "CREATE TABLE c05_07_changes",
        "CREATE TABLE c05_07_security_audit",
        "FOREIGN KEY (tenant_id,actor_id)",
        "UNIQUE KEY uq_c05_07_verify_token",
        "UNIQUE KEY uq_c05_07_old_token",
        "UNIQUE KEY uq_c05_07_new_token",
        "old_token_digest<>new_token_digest",
        "CREATE TRIGGER c05_07_audit_no_update",
        "CREATE TRIGGER c05_07_audit_no_delete",
        "SIGNAL SQLSTATE '45000'",
        "SQL SECURITY DEFINER",
        "CREATE PROCEDURE c05_07_begin_verify",
        "CREATE PROCEDURE c05_07_confirm_verify",
        "CREATE PROCEDURE c05_07_begin_change",
        "CREATE PROCEDURE c05_07_confirm_change",
        "ROLLBACK; RESIGNAL;",
        "START TRANSACTION;",
        "COMMIT;",
        "FOR UPDATE;",
        "UTC_TIMESTAMP(6)",
        "INTERVAL 10 MINUTE",
        "INTERVAL 5 MINUTE",
        "v_verified=0",
        "v_verified=1",
        "v_realm='TENANT'",
        "v_link=v_current_link",
        "v_global_rev=v_credential_rev",
        "v_global_rev=v_c",
        "v_old=p_old",
        "p_old<>p_new",
        "p_old_token<>p_new_token",
        "v_a=v_accountrev",
        "v_c=v_credrev",
        "v_m=v_memberrev",
        "v_t=v_tenantrev",
        "v_h=v_hostrev",
        "v_s=v_subrev",
        "v_current_mailrev=v_mailrev",
        "v_state='PENDING' AND v_exp>v_now",
        "v_old_utc IS NOT NULL",
        "v_new_utc IS NOT NULL",
        "SET old_confirmed_utc=v_now",
        "SET new_confirmed_utc=v_now",
        "UPDATE c05_06_mailboxes SET mailbox_digest=v_new",
        "mailbox_digest=v_old AND mailbox_revision=v_mailrev",
        "WHERE actor_id=v_actor AND revoked_utc IS NULL",
        "subject_revision=subject_revision+1",
        "UPDATE c05_06_challenges SET state='REVOKED'",
        "SET state='APPLIED'",
        "MAILBOX_VERIFIED",
        "PREVIOUS_CONFIRMED",
        "REPLACEMENT_CONFIRMED",
        "MAILBOX_CHANGED",
        "WHERE mailbox_digest=v_new AND actor_id<>v_actor",
        "SELECT COUNT(*) INTO v_collisions",
        "v_exp>v_now",
    ),
    "runner": (
        "set -euo pipefail",
        "mysql:8.4@sha256:",
        "127.0.0.1::3306",
        "openssl rand -hex 24",
        "trap cleanup EXIT",
        "database/tests/fixtures/c05_05_disposable_sessions.sql",
        "database/tests/fixtures/c05_06_disposable_recovery.sql",
        "database/tests/fixtures/c05_07_disposable_mailbox.sql",
        "GRANT EXECUTE ON PROCEDURE",
        "c05_07_begin_verify",
        "c05_07_confirm_verify",
        "c05_07_begin_change",
        "c05_07_confirm_change",
        "c05_05_is_current",
        "CRECISOL_C05_07_RUNNER_DSN",
        "CRECISOL_C05_07_SETUP_DSN",
        "Crecisol.MailboxApproval.MySqlTests.csproj",
        "-- --integration",
    ),
    "tests": (
        "SKIP C05-07 real MySQL/HTTP",
        'args[0] != "--integration"',
        "http://127.0.0.1:0",
        "Results.Accepted()",
        "Results.NoContent()",
        "Results.Unauthorized()",
        'MapPost("/__c05_07_test__/verify/request"',
        'MapPost("/__c05_07_test__/verify/confirm"',
        'MapPost("/__c05_07_test__/change/request"',
        'MapPost("/__c05_07_test__/change/confirm"',
        "Argon2idPasswordHasher",
        "new Argon2idLoginCandidateVerifier(hasher)",
        "PrepareContextualPasswordLogin LoginResolver",
        "ContextualLoginPreparationStatus.Ready",
        "ContextualLoginPreparationStatus.Denied",
        "Forwarded",
        "X-Forwarded-Host",
        "X-Tenant-Id",
        "no-store",
        "no-referrer",
        "No HMAC key, DSN or bearer leaks",
        "Reissued verification invalidates prior confirmation link",
        "Expired verification link cannot confirm",
        "Tenant A cannot confirm a mailbox belonging to B",
        "Already verified mailbox does not receive another verification link",
        "Incorrect Argon2id/unknown account changes yield neutral HTTP",
        "Globally occupied mailbox cannot be silently reassigned",
        "New address alone cannot replace old verified address",
        "Both one-use links permanently denied",
        "Sensitive email change revokes all actor A/B sessions",
        "B identity binding remains isolated",
        "Append-only database audit cannot be modified",
        "Append-only database audit cannot be deleted",
        "Revoked membership denies pending sensitive confirmation",
        "Restoring a revoked membership never resurrects stale challenge",
        "Re-verification cannot resurrect an earlier sensitive change",
        "Expired previous-address approval cannot be used",
        "16 concurrent real HTTP/MySQL second confirmations: exactly 1 winner",
        "No email may be changed without BOTH one-use mailbox proofs",
        "audit never stores token, digest, mailbox, password or email",
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
        "Documento Maestro Único",
        "C05-07",
        "Verificación de correo y cambios sensibles",
        "Reautenticación y confirmaciones auditable",
        "No cambio de correo sin comprobación",
        BASE,
        "C05-06",
        "HMAC-SHA256",
        "256-bit CSPRNG",
        "Argon2id",
        "MySQL 8.4",
        "16",
        "A/B/C",
        "C05-08 BLOQUEADA",
        "NO CERTIFICADA",
        "NO-GO",
        "Rollback",
    ),
    "ci": (
        "permissions:",
        "contents: read",
        "persist-credentials: false",
        "fetch-depth: 2",
        "dotnet-version: '10.0.x'",
        "qa/c05_07_mailbox_audits.py A",
        "qa/c05_07_mailbox_audits.py B",
        "qa/c05_07_mailbox_audits.py C",
        "Crecisol.MailboxApproval.MySqlTests.csproj",
        "database/tests/c05_07_mailbox_http_mysql.sh",
        "Crecisol.PasswordRecovery.MySqlTests.csproj",
        "database/tests/c05_06_recovery_http_mysql.sh",
        "Crecisol.SessionRevocation.MySqlTests.csproj",
        "database/tests/c05_05_session_http_mysql.sh",
        "Crecisol.ContextualLogin.ContractsTests.csproj",
        "Crecisol.AccountIdentity.ContractsTests.csproj",
        ".c05-07-proof",
        "GITHUB_SHA",
        "GITHUB_RUN_ID",
    ),
}


def demand(value: bool, reason: str) -> None:
    if not value:
        raise AssertionError(reason)


def read_sources() -> dict[str, str]:
    return {key: (ROOT / path).read_text(encoding="utf-8")
            for key, path in FILES.items() if key != "qa"}


def invariants(data: dict[str, str]) -> None:
    for key, markers in GUARDS.items():
        for marker in markers:
            demand(marker in data[key], "Missing C05-07 security marker "+key+": "+marker)
    api = (ROOT / "backend/src/Crecisol.Api/Program.cs").read_text(encoding="utf-8")
    routes = re.findall(r'app\.Map(?:Get|Post|Put|Patch|Delete)\(\s*"([^"]+)"', api)
    demand(routes == ["/api/v1/public/branding/platform"] and
           "VerifyAndChangeSensitiveMailbox" not in api and
           "MySqlDisposableSensitiveMailboxLedger" not in api,
           "Product API MUST still expose public branding only")
    infra = (ROOT / "backend/src/Crecisol.Infrastructure/Crecisol.Infrastructure.csproj"
             ).read_text(encoding="utf-8")
    demand('PackageReference Include="MySqlConnector" Version="2.6.2"' in infra,
           "Existing MySqlConnector@2.6.2 pin must remain")
    demand("MySqlConnection" not in data["app"] and
           "HttpContext" not in data["app"] and
           "Environment.GetEnvironmentVariable" not in data["app"] and
           "Console.WriteLine" not in data["app"],
           "Clean Application cannot depend on DB, HTTP, env or logs")
    demand("CommandType.Text" not in data["adapter"] and
           "SELECT * FROM" not in data["adapter"] and
           "CREATE TABLE" not in data["adapter"],
           "Only safe procedure calls may make state changes")
    demand("GRANT ALL" not in data["runner"] and
           "0.0.0.0::3306" not in data["runner"],
           "No broad SQL privilege or public MySQL exposure")
    demand("workflow_dispatch:" not in data["ci"] and
           "id-token: write" not in data["ci"] and
           "deploy" not in data["ci"].lower(),
           "No deployment, manual privileged trigger or OIDC escalation")


def audit_a() -> None:
    subprocess.run([sys.executable, "qa/master_source_guard.py", "--self-test"],
                   cwd=ROOT, check=True)
    subprocess.run([sys.executable, "qa/c05_06_recovery_audits.py", "A"],
                   cwd=ROOT, check=True)
    plan = (ROOT / "docs/planificacion-futura/C05_MICROFASES_PROPUESTAS.md"
            ).read_text(encoding="utf-8")
    demand("C05-07 | Verificación de correo y cambios sensibles" in plan and
           "No cambio de correo sin comprobación" in plan,
           "Canonical C05-07 positive/negative plan not found")
    invariants(read_sources())
    print("PASS CRECISOL C05-07 A: canonical Word, C05-06, reauth, dual mailbox proof and audit")


def audit_b() -> None:
    original=read_sources()
    invariants(original)
    count=0
    for name, markers in GUARDS.items():
        for marker in markers:
            mutant=dict(original)
            mutant[name]=original[name].replace(marker,"[REMOVED_C05_07_SECURITY_GUARD]")
            demand(mutant[name]!=original[name],"Adversarial deletion had no effect")
            try:
                invariants(mutant)
            except AssertionError:
                count+=1
            else:
                raise AssertionError("Hostile C05-07 marker bypassed: "+name+" "+marker)
    demand(count>=110,"Too few rejected static attacks")
    print(f"PASS CRECISOL C05-07 B: {count} adversarial static guard deletions rejected")
    print("STATIC only; actual .NET10 Argon2id HTTP and MySQL8.4 tests are separate")


def git(*args: str)->str:
    return subprocess.check_output(["git",*args],cwd=ROOT,
                                   text=True,stderr=subprocess.DEVNULL).strip()


def audit_c() -> None:
    demand(os.getenv("GITHUB_ACTIONS")=="true" and
           os.getenv("GITHUB_REPOSITORY")==REPO,
           "Only GitHub C05-07 read-only CI may attest")
    sha,run=os.getenv("GITHUB_SHA",""),os.getenv("GITHUB_RUN_ID","")
    demand(re.fullmatch(r"[0-9a-f]{40}",sha) is not None and
           re.fullmatch(r"[0-9]+",run) is not None and
           git("rev-parse","HEAD")==sha,
           "Exact SHA, verified checkout and run ID required")
    for stage in ("A","B","build","mysql","regressions"):
        p=PROOF/stage
        demand(p.is_file() and p.read_text(encoding="utf-8").strip()==
               f"PASS {stage} sha={sha} run={run}",
               "Missing real same-run/SHA proof: "+stage)
    if os.getenv("GITHUB_EVENT_NAME")=="pull_request":
        event=Path(os.getenv("GITHUB_EVENT_PATH",""))
        demand(event.is_file(),"Missing PR event payload")
        pr=json.loads(event.read_text(encoding="utf-8"))["pull_request"]
        base,head=pr["base"],pr["head"]
        demand(base["ref"]=="dev" and base["sha"]==BASE and
               head["ref"]==BRANCH and
               base["repo"]["full_name"]==REPO and
               head["repo"]["full_name"]==REPO,
               "Wrong owner, branch, or approved base SHA")
        demand(git("rev-list","--parents","-n","1","HEAD").split()==
               [sha,base["sha"],head["sha"]],
               "Synthetic PR merge ancestry mismatch")
        changed=git("diff","--name-only",base["sha"],head["sha"]).splitlines()
    else:
        demand(os.getenv("GITHUB_EVENT_NAME")=="push" and
               os.getenv("GITHUB_REF")=="refs/heads/dev",
               "POSTMERGE certification only on dev")
        changed=git("show","--format=","--name-only",sha).splitlines()
    demand(set(changed)==ALLOWED and len(changed)==len(ALLOWED),
           "Unreviewed files or missing C05-07 scope: "+repr(changed))
    invariants(read_sources())
    print(f"PASS CRECISOL C05-07 C: exact PR dev SHA={sha} run={run} A/B/NET10/HTTP/MySQL8.4")
    print("NO CLAIM of real email delivery, real login, MFA, production users or API")


if __name__ == "__main__":
    try:
        demand(len(sys.argv)==2 and sys.argv[1] in ("A","B","C"),
               "Usage: qa/c05_07_mailbox_audits.py A|B|C")
        {"A":audit_a,"B":audit_b,"C":audit_c}[sys.argv[1]]()
    except (AssertionError,OSError,KeyError,ValueError,
            subprocess.SubprocessError) as exc:
        print("FAIL CRECISOL C05-07 audit: "+str(exc),file=sys.stderr)
        sys.exit(1)
