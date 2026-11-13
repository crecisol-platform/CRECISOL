#!/usr/bin/env python3
"""CRECISOL C05-04 scoped contextual login A/B/C, fail-closed and SHA-bound."""
from __future__ import annotations

import json
import os
from pathlib import Path
import re
import subprocess
import sys

from c05_01_preparation_audits import ROOT

REPO = "crecisol-platform/CRECISOL"
FEATURE = "feature/c05-04-contextual-password-login-20261113"
BASE = "70d68817269536b82e9e86bde0e7f448a5a48b6e"
FILES = {
    "app": "backend/src/Crecisol.Application/Identity/PrepareContextualPasswordLogin.cs",
    "crypto": "backend/src/Crecisol.CredentialSecurity/Argon2idLoginCandidateVerifier.cs",
    "test": "backend/tests/Crecisol.ContextualLogin.ContractsTests/Program.cs",
    "testproj": "backend/tests/Crecisol.ContextualLogin.ContractsTests/Crecisol.ContextualLogin.ContractsTests.csproj",
    "doc": "docs/c05/C05_04_ESPECIFICACION.md",
    "qa": "qa/c05_04_contextual_login_audits.py",
    "workflow": ".github/workflows/c05-04-contextual-login.yml",
}
ALLOWED = set(FILES.values())
PROOF = ROOT / ".c05-04-contextual-proof"
GUARDS = {
    "app": (
        "public sealed record CanonicalLoginHandle",
        "raw.Any(c => c < 33 || c > 126)",
        "raw.ToLowerInvariant()",
        "CanonicalTenantHost.Parse(domain)",
        "public enum TrustedLoginRealm",
        "TenantAccount = 1",
        "PlatformOperator = 2",
        "public sealed record TrustedLoginCredentialSnapshot",
        "credential.Kind != AccountCredentialKind.PasswordVerifier",
        "account.ActorId != credential.ActorId",
        "private string EncodedVerifier",
        "public bool SameVersionAs",
        "TrustedLoginCredentialSnapshot[redacted]",
        "public interface ITrustedLoginCredentialDirectory",
        "Task<IReadOnlyList<TrustedLoginCredentialSnapshot>?> FindExactAsync",
        "public interface ITrustedUnknownLoginVerifier",
        "public interface ITrustedLoginPasswordVerifier",
        "PasswordHashCheck Verify(ReadOnlyMemory<char> candidate",
        "public interface ITrustedLoginEntryReader",
        "ReadServerVerifiedEntryAsync",
        "public interface ITrustedLoginTenantLinkDirectory",
        "FindAllByActorAsync",
        "public enum ContextualLoginPreparationStatus",
        "Ambiguous = 2",
        "Ready = 3",
        "public sealed record PreparedContextualLoginEvidence",
        "PreparedContextualLoginEvidence[redacted]",
        "public sealed class PrepareContextualPasswordLogin",
        "ReadOnlyMemory<char> candidate",
        "unknown.ReadDummyVerifierAsync",
        "exact?.ExportForInternalVerifier() ?? dummyPhc",
        "verifier.Verify(candidate, phc)",
        "exact.Realm != TrustedLoginRealm.TenantAccount",
        "entryReader.ReadServerVerifiedEntryAsync",
        "hostResolver.ResolveAsync",
        "MaxLinkSnapshots = 32",
        "firstLinks!.ToArray()",
        "x.ActorId != actor",
        "rows.Select(x => x.TenantId).Distinct().Count() == rows.Count",
        "active.Length > 1",
        "ContextualLoginPreparation.Ambiguous",
        "selected.TenantId",
        "TenantLifecycleState.Active",
        "againCredentials",
        "exact.SameVersionAs",
        "againLinks",
        "!firstFrozen.SequenceEqual(againLinks!)",
        "againEntry != entry",
        "againHostHint != hostHint",
        "againCatalog != catalog",
        "finalNow < firstNow",
        "!selected.IsActiveAtUtc(finalNow)",
        "PasswordVerifier",
        "AccountCredentialState.Active",
        "ContextualLoginPreparation.Ready",
        "catch (UnauthorizedAccessException)",
        "catch (InvalidOperationException)",
    ),
    "crypto": (
        "public sealed class Argon2idLoginCandidateVerifier",
        "ITrustedLoginPasswordVerifier",
        "hasher.Verify(candidate.Span, storedPhc)",
        "Argon2idLoginCandidateVerifier[redacted]",
    ),
    "test": (
        "RandomNumberGenerator.GetBytes(32)",
        "real .NET10 Argon2id",
        "Canonical global login is deterministic ASCII",
        "Case-insensitive canonical lookup without tenant selector",
        "On host A, actor with both memberships resolves only A",
        "On host B, exact actor resolves only B",
        "Host B cannot get A-only membership",
        "Two active contexts cannot be silently resolved or listed",
        "Central entry uniquely resolves one active membership without tenant chooser",
        "Unknown account exercises server-owned Argon2id dummy PHC once",
        "Platform operator cannot enter tenant realm",
        "Failed uniqueness with duplicate accounts denied",
        "Wrong password never performs tenant lookup",
        "Expired membership denied",
        "Membership version changed during resolution",
        "Tenant catalog mismatch rejected",
        "Backend host changes after credential check",
        "Backend clock rollback denied",
        "Non-UTC server clock must be rejected",
        "Credential digest rotated during resolution",
        "Concurrent synthetic logins keep exact tenant context",
        "No session issuance or user JWT method exposed",
        "Strong policy recommends rehash without role/session issuance",
    ),
    "testproj": (
        '<TargetFramework>net10.0</TargetFramework>',
        '<TreatWarningsAsErrors>true</TreatWarningsAsErrors>',
        "Crecisol.CredentialSecurity/Crecisol.CredentialSecurity.csproj",
    ),
    "doc": (
        "C05-04",
        "C05-03",
        BASE,
        "C05-05 BLOQUEADA",
        "Documento Maestro Único",
        "sin selector de empresa",
        "Argon2id",
        "NO-GO",
        "Rollback",
    ),
    "workflow": (
        "permissions:",
        "contents: read",
        "persist-credentials: false",
        "fetch-depth: 2",
        "dotnet-version: '10.0.x'",
        "qa/c05_04_contextual_login_audits.py A",
        "qa/c05_04_contextual_login_audits.py B",
        "qa/c05_04_contextual_login_audits.py C",
        "Crecisol.ContextualLogin.ContractsTests.csproj",
        "Crecisol.PasswordHash.ContractsTests.csproj",
        "Crecisol.AccountIdentity.ContractsTests.csproj",
        "Crecisol.InvitationSecurity.ContractsTests.csproj",
        ".c05-04-contextual-proof",
        "GITHUB_SHA",
        "GITHUB_RUN_ID",
    ),
}


def require(condition: bool, message: str) -> None:
    if not condition:
        raise AssertionError(message)


def read() -> dict[str, str]:
    return {k: (ROOT / p).read_text(encoding="utf-8")
            for k, p in FILES.items() if k != "qa"}


def invariant(data: dict[str, str]) -> None:
    for kind, markers in GUARDS.items():
        for marker in markers:
            require(marker in data[kind], f"Missing {kind} security guard: {marker}")
    api = (ROOT / "backend/src/Crecisol.Api/Program.cs").read_text(encoding="utf-8")
    routes = re.findall(r'app\.Map(?:Get|Post|Put|Patch|Delete)\(\s*"([^"]+)"', api)
    require(routes == ["/api/v1/public/branding/platform"],
            "C05-04 must NOT expose a login, session or private API")
    require("PrepareContextualPasswordLogin" not in api and
            "Argon2idLoginCandidateVerifier" not in api,
            "Contextual login adapters may not be registered in production API")
    legacy = (ROOT / "backend/src/Crecisol.Infrastructure/Crecisol.Infrastructure.csproj").read_text(
        encoding="utf-8")
    require('PackageReference Include="MySqlConnector" Version="2.6.2"' in legacy and
            "Argon2" not in legacy, "C03 original Infrastructure pin must remain")
    crypto_project = (ROOT / "backend/src/Crecisol.CredentialSecurity/Crecisol.CredentialSecurity.csproj").read_text(
        encoding="utf-8")
    require('<ProjectReference Include="../Crecisol.Application/Crecisol.Application.csproj" />'
            in crypto_project and
            '<PackageReference Include="Konscious.Security.Cryptography.Argon2" Version="1.3.1" />'
            in crypto_project, "Only C05-03 reviewed crypto dependency")
    require("MySqlConnection" not in data["app"] and
            "HttpContext" not in data["app"] and
            "Configuration.GetConnectionString" not in data["app"] and
            "Environment.GetEnvironmentVariable" not in data["app"] and
            "Console.WriteLine" not in data["app"],
            "Application must not reach API, SQL, DSN, secrets or logging")
    require("id-token: write" not in data["workflow"] and
            "workflow_dispatch:" not in data["workflow"] and
            "deploy" not in data["workflow"].lower(),
            "No privileged CI, production deploy or manual workflow")
    require("JWT token" not in data["test"] and
            "Set-Cookie" not in data["app"], "No auth bearer/session must be fabricated")


def audit_a() -> None:
    subprocess.run([sys.executable, "qa/master_source_guard.py", "--self-test"],
                   cwd=ROOT, check=True)
    subprocess.run([sys.executable, "qa/c05_01_preparation_audits.py", "source"],
                   cwd=ROOT, check=True)
    plan = (ROOT / "docs/planificacion-futura/C05_MICROFASES_PROPUESTAS.md").read_text(
        encoding="utf-8")
    require("C05-04 | Login contextual" in plan and
            "C05-05 | Sesiones y revocación" in plan,
            "Canonical sequential identity planning mismatched")
    for marker in ("6863110105", "70d68817269536b82e9e86bde0e7f448a5a48b6e",
                   "No confundir sesión de tenant A y B"):
        require(marker in (read()["doc"] + plan), "Missing predecessor or expected negative: "+marker)
    invariant(read())
    print("PASS CRECISOL C05-04 A: Word, C05-03 gate and no-selector login contract")


def audit_b() -> None:
    original = read()
    invariant(original)
    rejected = 0
    for kind, markers in GUARDS.items():
        for marker in markers:
            mutant = dict(original)
            mutant[kind] = original[kind].replace(marker, "[REMOVED_C05_04_SECURITY]")
            require(mutant[kind] != original[kind], "Ineffective mutation")
            try:
                invariant(mutant)
            except AssertionError:
                rejected += 1
            else:
                raise AssertionError("Undetected guard mutation: " + kind + marker)
    require(rejected >= 65, "Insufficient adversarial boundary mutation coverage")
    print(f"PASS CRECISOL C05-04 B: {rejected} adversarial guard deletions rejected")
    print("STATIC MUTATIONS DO NOT SUBSTITUTE REAL NET10 Argon2id AND TENANT TESTS")


def git(*args: str) -> str:
    return subprocess.check_output(["git", *args], cwd=ROOT,
                                   text=True, stderr=subprocess.DEVNULL).strip()


def audit_c() -> None:
    require(os.getenv("GITHUB_ACTIONS") == "true" and
            os.getenv("GITHUB_REPOSITORY") == REPO,
            "Only CRECISOL CI may certify run")
    sha = os.getenv("GITHUB_SHA", "")
    run = os.getenv("GITHUB_RUN_ID", "")
    require(re.fullmatch("[0-9a-f]{40}", sha) is not None and
            re.fullmatch("[0-9]+", run) is not None and
            git("rev-parse", "HEAD") == sha, "Exact SHA/run required")
    for stage in ("A", "B", "net10", "regression"):
        file = PROOF / stage
        require(file.is_file() and file.read_text(encoding="utf-8").strip() ==
                f"PASS {stage} sha={sha} run={run}",
                "Missing same SHA/run proof: "+stage)

    event = os.getenv("GITHUB_EVENT_NAME")
    if event == "pull_request":
        payload = Path(os.getenv("GITHUB_EVENT_PATH", ""))
        require(payload.is_file(), "Missing PR payload")
        pr = json.loads(payload.read_text(encoding="utf-8"))["pull_request"]
        base, head = pr["base"], pr["head"]
        require(base["ref"] == "dev" and head["ref"] == FEATURE and
                base["sha"] == BASE and
                base["repo"]["full_name"] == REPO and
                head["repo"]["full_name"] == REPO,
                "Wrong repository, base or head")
        require(git("rev-list", "--parents", "-n", "1", "HEAD").split() ==
                [sha, base["sha"], head["sha"]],
                "PR synthetic merge parent SHAs inconsistent")
        changed = git("diff", "--name-only", base["sha"], head["sha"]).splitlines()
    else:
        require(event == "push" and os.getenv("GITHUB_REF") == "refs/heads/dev",
                "Only dev may attest postmerge")
        changed = git("show", "--format=", "--name-only", sha).splitlines()
    require(set(changed) == ALLOWED and len(changed) == len(ALLOWED,
            "Unauthorized or missing files: " + repr(changed))
    invariant(read())
    print(f"PASS CRECISOL C05-04 C: exact A/B .NET10 real hash + tenant regressions sha={sha} run={run}")
    print("NO signed session, login API, real credentials, MFA, tenant authorization or production")


if __name__ == "__main__":
    try:
        require(len(sys.argv) == 2 and sys.argv[1] in ("A", "B", "C"),
                "Usage: qa/c05_04_contextual_login_audits.py A|B|C")
        {"A": audit_a, "B": audit_b, "C": audit_c}[sys.argv[1]]()
    except (AssertionError, OSError, KeyError, ValueError,
            subprocess.SubprocessError) as exc:
        print("FAIL CRECISOL C05-04 audit: " + str(exc), file=sys.stderr)
        sys.exit(1)
