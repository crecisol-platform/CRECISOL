#!/usr/bin/env python3
"""CRECISOL C05-03 A/B/C scoped password hashing: no functional login claims."""
from __future__ import annotations

import json
import os
from pathlib import Path
import re
import subprocess
import sys

from c05_01_preparation_audits import ROOT

REPO = "crecisol-platform/CRECISOL"
FEATURE = "feature/c05-03-argon2id-password-policy-20261113"
FILES = {
    "policy": "backend/src/Crecisol.Application/Identity/PasswordHashingPolicy.cs",
    "hash": "backend/src/Crecisol.Infrastructure/Identity/Argon2idPasswordHasher.cs",
    "infra": "backend/src/Crecisol.Infrastructure/Crecisol.Infrastructure.csproj",
    "test": "backend/tests/Crecisol.PasswordHash.ContractsTests/Program.cs",
    "testproj": "backend/tests/Crecisol.PasswordHash.ContractsTests/Crecisol.PasswordHash.ContractsTests.csproj",
    "document": "docs/c05/C05_03_ESPECIFICACION.md",
    "qa": "qa/c05_03_password_audits.py",
    "workflow": ".github/workflows/c05-03-password-hashing.yml",
}
ALLOWED = set(FILES.values())
PROOF = ROOT / ".c05-03-password-proof"
GUARDS = {
    "policy": (
        "public enum PasswordCompromiseVerdict",
        "public interface ITrustedCompromisedPasswordScreen",
        "PasswordCompromiseVerdict Check(ReadOnlySpan<char> candidate)",
        "PasswordHashingPolicy",
        "public const int MinAcceptedRunes = 15",
        "public const int MinAcceptedMaximum = 64",
        "public const int MaxAcceptedRunes = 256",
        "public const int MaxUtf8Bytes = 1024",
        "Argon2MinimumMemoryKib = 19456",
        "Argon2MaximumMemoryKib = 65536",
        "Argon2MinimumIterations = 2",
        "Argon2MaximumIterations = 4",
        "memoryKib % 1024 != 0",
        "policyRevision < 1",
        "Parallelism => 1",
        "PasswordHashCheck",
        "Credential policy violates approved security limits",
    ),
    "hash": (
        "using Konscious.Security.Cryptography;",
        "public sealed class PasswordVerifierEnvelope",
        "ExportForTrustedCredentialStore",
        "PasswordVerifierEnvelope[redacted]",
        "public sealed class Argon2idPasswordHasher",
        "ITrustedCompromisedPasswordScreen",
        "CreateVerifier(ReadOnlySpan<char> candidate)",
        "PasswordCompromiseVerdict.Allowed",
        "RandomNumberGenerator.GetBytes(16)",
        "CryptographicOperations.ZeroMemory(password)",
        "CryptographicOperations.ZeroMemory(salt)",
        "CryptographicOperations.ZeroMemory(digest)",
        "$argon2id$v=19$m=",
        "Verify(",
        "CryptographicOperations.FixedTimeEquals(",
        "CryptographicOperations.ZeroMemory(stored.Salt)",
        "CryptographicOperations.ZeroMemory(stored.Digest)",
        "new Argon2id(password)",
        "DegreeOfParallelism = 1",
        "MemorySize = memoryKib",
        "Iterations = iterations",
        "candidate.EnumerateRunes()",
        "Rune.IsControl(rune)",
        "StrictUtf8.GetBytes(candidate)",
        "catch (EncoderFallbackException)",
        "TryParse(string? encoded",
        "encoded.Length > 160",
        'parts[1] != "argon2id"',
        'parts[2] != "v=19"',
        'parameters[2] != "p=1"',
        "memoryKib < PasswordHashingPolicy.Argon2MinimumMemoryKib",
        "memoryKib > PasswordHashingPolicy.Argon2MaximumMemoryKib",
        "iterations < PasswordHashingPolicy.Argon2MinimumIterations",
        "iterations > PasswordHashingPolicy.Argon2MaximumIterations",
        "EncodeUnpadded(candidate) != segment",
        "CryptographicOperations.ZeroMemory(candidate)",
    ),
    "test": (
        "RandomNumberGenerator.GetBytes(32)",
        "Correct secret verifies cryptographically",
        "Wrong candidate rejects without grant or rehash",
        "Independent CSPRNG salt",
        "15 emoji = 15 runes accepted",
        "Unpaired UTF16 surrogate rejected",
        "Breach source unavailable fails closed",
        "Breach screen exception fails closed",
        "Over configured max rejected",
        "Padded/noncanonical digest rejected",
        "Excessive memory/DoS cost rejected",
        "Excessive iteration/DoS cost rejected",
        "Wrong candidate still rejected after upgrade",
        "Concurrent requests never reuse a salt",
        "Hasher exposes no identity/session/tenant authorization API",
        "NO login, actual user, database",
    ),
    "infra": (
        '<TargetFramework>net10.0</TargetFramework>',
        '<TreatWarningsAsErrors>true</TreatWarningsAsErrors>',
        'PackageReference Include="Konscious.Security.Cryptography.Argon2" Version="1.3.1"',
    ),
    "document": (
        "C05-03",
        "NO CERTIFICADA",
        "C05-04 BLOQUEADA",
        "Documento Maestro Único",
        "C05-02 certificada",
        "Argon2id",
        "comprometidas",
        "SQL",
        "NO-GO",
        "Rollback",
    ),
    "workflow": (
        "permissions:",
        "contents: read",
        "persist-credentials: false",
        "fetch-depth: 2",
        "dotnet-version: '10.0.x'",
        "qa/c05_03_password_audits.py A",
        "qa/c05_03_password_audits.py B",
        "qa/c05_03_password_audits.py C",
        "Crecisol.PasswordHash.ContractsTests.csproj",
        "Crecisol.AccountIdentity.ContractsTests.csproj",
        "Crecisol.InvitationSecurity.ContractsTests.csproj",
        ".c05-03-password-proof",
        "GITHUB_SHA",
        "GITHUB_RUN_ID",
    ),
}


def need(ok: bool, reason: str) -> None:
    if not ok:
        raise AssertionError(reason)


def sources() -> dict[str, str]:
    return {key: (ROOT / path).read_text(encoding="utf-8")
            for key, path in FILES.items() if key != "qa"}


def invariants(data: dict[str, str]) -> None:
    for kind, guards in GUARDS.items():
        for marker in guards:
            need(marker in data[kind], f"Missing fail-closed {kind} guard: {marker}")

    need(data["infra"].count('PackageReference Include="Konscious.Security.Cryptography.Argon2"') == 1,
         "Argon2 NuGet version must be pinned exactly once")
    need("Environment.GetEnvironmentVariable" not in data["hash"] and
         "IConfiguration" not in data["hash"] and
         "HttpContext" not in data["hash"] and
         "MySqlConnection" not in data["hash"] and
         "Console.WriteLine" not in data["hash"],
         "Hasher may not access request, DB, environment or log secrets")
    need("RawPassword" not in data["policy"] and
         "StoredPassword" not in data["policy"] and
         "AccountTenantBindingEvidence" not in data["hash"],
         "Password policy may never become an authorization grant")
    need("PasswordHashingPolicy" not in (
         ROOT / "backend/src/Crecisol.Api/Program.cs").read_text(encoding="utf-8"),
         "No credential service wired into production API")
    api = (ROOT / "backend/src/Crecisol.Api/Program.cs").read_text(encoding="utf-8")
    routes = re.findall(r'app\.Map(?:Get|Post|Put|Patch|Delete)\(\s*"([^"]+)"', api)
    need(routes == ["/api/v1/public/branding/platform"],
         "No login/signup/admin route may be introduced")
    need("id-token: write" not in data["workflow"] and
         "workflow_dispatch:" not in data["workflow"] and
         "deploy" not in data["workflow"].lower(),
         "No privileged workflow, manual execution or production deployment")
    need("Password" not in (
         ROOT / "backend/src/Crecisol.Domain/Identity/IdentityAccountModels.cs"
         ).read_text(encoding="utf-8").split("public sealed record AccountTenantLinkSnapshot")[0].split(
             "public sealed record AccountCredentialMetadata")[1].split(
                 "public override string ToString()")[0].replace("PasswordVerifier", ""),
         "C05-01 credential metadata must remain secret-free")


def audit_a() -> None:
    subprocess.run([sys.executable, "qa/master_source_guard.py", "--self-test"],
                   cwd=ROOT, check=True)
    subprocess.run([sys.executable, "qa/c05_01_preparation_audits.py", "source"],
                   cwd=ROOT, check=True)
    plan = (ROOT / "docs/planificacion-futura/C05_MICROFASES_PROPUESTAS.md").read_text(
        encoding="utf-8")
    need("C05-03 | Hash y política de credenciales" in plan and
         "C05-04 | Login contextual" in plan,
         "C05-03/C05-04 mandatory sequential scope mismatch")
    doc = sources()["document"]
    for marker in ("6862363822", "98cf4315110dcd9cfb9fe7e9eb920ecf19f0253f",
                   "Konscious.Security.Cryptography.Argon2",
                   "1.3.1"):
        need(marker in doc, "Cannot trace predecessor/dependency: " + marker)
    invariants(sources())
    print("PASS C05-03 audit A: canonical master and C05-02 gate, Argon2id code-to-tests")


def audit_b() -> None:
    original = sources()
    invariants(original)
    rejected = 0
    for key, guards in GUARDS.items():
        for guard in guards:
            mutant = dict(original)
            mutant[key] = original[key].replace(guard, "[REMOVED_C05_03_GUARD]")
            need(mutant[key] != original[key], "Ineffective security mutation")
            try:
                invariants(mutant)
            except AssertionError:
                rejected += 1
            else:
                raise AssertionError(f"Adversarial deletion not detected: {key}:{guard}")
    need(rejected >= 65, "Insufficient static negative assertions")
    print(f"PASS C05-03 audit B: {rejected} adversarial guard removals rejected")
    print("STATIC CHECKS DO NOT REPLACE REAL Argon2id NET10 TESTS")


def git(*args: str) -> str:
    return subprocess.check_output(["git", *args], cwd=ROOT, text=True,
                                   stderr=subprocess.DEVNULL).strip()


def audit_c() -> None:
    need(os.getenv("GITHUB_ACTIONS") == "true" and
         os.getenv("GITHUB_REPOSITORY") == REPO,
         "Only CRECISOL GitHub CI may attest SHA and run")
    sha, run = os.getenv("GITHUB_SHA", ""), os.getenv("GITHUB_RUN_ID", "")
    need(re.fullmatch("[0-9a-f]{40}", sha) is not None and
         re.fullmatch("[0-9]+", run) is not None and
         git("rev-parse", "HEAD") == sha,
         "Exact source SHA and run ID required")
    for stage in ("A", "B", "net10", "oldcontract"):
        f = PROOF / stage
        need(f.is_file() and
             f.read_text(encoding="utf-8").strip() ==
             f"PASS {stage} sha={sha} run={run}",
             "Missing same SHA/run runtime evidence: " + stage)

    event = os.getenv("GITHUB_EVENT_NAME")
    if event == "pull_request":
        event_path = Path(os.getenv("GITHUB_EVENT_PATH", ""))
        need(event_path.is_file(), "Missing PR payload")
        pr = json.loads(event_path.read_text(encoding="utf-8"))["pull_request"]
        base, head = pr["base"], pr["head"]
        need(base["ref"] == "dev" and head["ref"] == FEATURE and
             base["repo"]["full_name"] == REPO and
             head["repo"]["full_name"] == REPO,
             "PR branch/repository context invalid")
        need(git("rev-list", "--parents", "-n", "1", "HEAD").split() ==
             [sha, base["sha"], head["sha"]],
             "PR synthetic merge parent SHAs invalid")
        changed = git("diff", "--name-only", base["sha"], head["sha"]).splitlines()
    else:
        need(event == "push" and os.getenv("GITHUB_REF") == "refs/heads/dev",
             "Certification must be based on dev only")
        changed = git("show", "--format=", "--name-only", sha).splitlines()
    need(set(changed) == ALLOWED and len(changed) == len(ALLOWED),
         "Unexpected or missing changed files: " + repr(changed))
    invariants(sources())
    print(f"PASS C05-03 audit C: same SHA/run A/B NET10 and predecessor regression sha={sha} run={run}")
    print("NO production credentials, real breach screen, login, session or tenant grants")


if __name__ == "__main__":
    try:
        need(len(sys.argv) == 2 and sys.argv[1] in ("A", "B", "C"),
             "Usage: python qa/c05_03_password_audits.py A|B|C")
        {"A": audit_a, "B": audit_b, "C": audit_c}[sys.argv[1]]()
    except (AssertionError, OSError, KeyError, ValueError,
            subprocess.SubprocessError) as exc:
        print("FAIL CRECISOL C05-03 audit: " + str(exc), file=sys.stderr)
        sys.exit(1)
