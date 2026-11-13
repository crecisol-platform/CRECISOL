#!/usr/bin/env python3
"""CRECISOL C05-01 contract-only A/B/C audits. NOT a production login pentest.

A: exact canonical Word/PDF and C05 normative source + C04 prerequisites.
B: fail-closed Domain/Application code markers + adversarial mutations + tests.
C: same-run A+B+.NET+Angular+Chromium+MySQL markers and Git SHA/provenance.
"""
from __future__ import annotations

import json
import os
from pathlib import Path
import re
import subprocess
import sys

from c05_01_preparation_audits import canonical_c05_source, ROOT
from master_source_guard import canonical_active_word, CURRENT_WORD_SHA, CURRENT_PDF_SHA

DOMAIN = ROOT / "backend/src/Crecisol.Domain/Identity/IdentityAccountModels.cs"
APP = ROOT / "backend/src/Crecisol.Application/Identity/ResolveCurrentAccountTenantLink.cs"
TEST = ROOT / "backend/tests/Crecisol.AccountIdentity.ContractsTests/Program.cs"
PROJ = ROOT / "backend/tests/Crecisol.AccountIdentity.ContractsTests/Crecisol.AccountIdentity.ContractsTests.csproj"
SPEC = ROOT / "docs/c05/IMPLEMENTACION_C05_01_CONTRATOS.md"
WORKFLOW = ROOT / ".github/workflows/c05-01-account-model.yml"
API = ROOT / "backend/src/Crecisol.Api/Program.cs"
PROOF = ROOT / ".c05-01-account-proof"

C05_SHA = "bf7036849d6f1aad36a3a111b62121947aa12fae8c2a0551e63b0cc788d4a0aa"
PRIOR_DEV = "04c8ad23231a50829e5fbf9025f8b7d4789595b2"
MARKERS = {
    "domain": (
        "public sealed record AccountCredentialId",
        "public sealed record AccountTenantLinkId",
        "public sealed record IdentityAccountSnapshot",
        "public sealed record AccountCredentialMetadata",
        "public sealed record AccountTenantLinkSnapshot",
        "public sealed record PlatformOperatorDirectoryEntry",
        "public sealed record AccountTenantBindingEvidence",
        "public enum IdentityAccountState",
        "public enum AccountCredentialKind",
        "public enum AccountTenantProfileKind",
        "public enum PlatformOperatorState",
        "if (value == default || value.Offset != TimeSpan.Zero)",
        "IdentityModelValidation.RequireRevision(revision);",
        "State == IdentityAccountState.Active",
        "State == TenantMembershipState.Active",
        "GrantedAtUtc.Value <= nowUtc",
        "nowUtc < ExpiresAtUtc.Value",
        "link.ActorId != context.ActorId",
        "link.TenantId != context.TenantId",
        "link.Revision != context.MembershipRevision",
        "!account.IsActiveAtUtc(nowUtc)",
        "!link.IsActiveAtUtc(nowUtc)",
        "AccountTenantBindingEvidence[redacted]",
        "AccountCredentialMetadata[redacted]",
    ),
    "app": (
        "ResolveCurrentTenantIdentityContext context",
        "ITrustedIdentityAccountReader",
        "ITrustedAccountTenantLinkReader",
        "ITrustedHostUtcClock",
        "public async Task<AccountTenantBindingEvidence> ExecuteAsync(",
        "CancellationToken cancellationToken)",
        "var identity = await context.ExecuteAsync(cancellationToken);",
        "identity.ActorId, cancellationToken",
        "identity.ActorId, identity.TenantId, cancellationToken",
        "matches is null || matches.Count != 1 || matches[0] is null",
        "link.ActorId != identity.ActorId",
        "link.TenantId != identity.TenantId",
        "link.Revision != identity.MembershipRevision",
        "!link.IsActiveAtUtc(firstNow)",
        "latestAccount != account",
        "latestLinks is null || latestLinks.Count != 1 || latestLinks[0] != link",
        "var latestIdentity = await context.ExecuteAsync(cancellationToken);",
        "latestIdentity != identity",
        "finalNow < firstNow",
        "!link.IsActiveAtUtc(finalNow)",
        "AccountTenantBindingEvidence.FromTrustedInternalSnapshots(",
    ),
    "test": (
        "Same actor is independently linked to B and never inherits A scope",
        "A actor cannot use B tenant link",
        "Different actor credential account rejected",
        "Different actor membership link rejected",
        "B tenant link rejected under verified A host",
        "Wrong C04 membership revision rejected",
        "Duplicate membership records denied",
        "Session revision switch detected on refresh",
        "Changing account revision mid-flight rejected",
        "Changing membership link ID mid-flight rejected",
        "Changing link profile mid-flight rejected",
        "Revoked C04 session cannot be a source of identity",
        "Suspended tenant cannot authorize account",
        "Clock moving backwards denied",
        "Account expiry between start and final checks denied",
        "Use case accepts cancellation ONLY",
        "Credential metadata cannot carry a password",
        "No actor, account, tenant or credential identifier appears in ToString",
        "LIMIT: synthetic trusted ports",
    ),
    "spec": (
        "C05-01",
        "NO CERTIFICADA",
        "T01",
        "T24",
        "C05-02",
        C05_SHA,
        CURRENT_WORD_SHA,
        CURRENT_PDF_SHA,
    ),
    "workflow": (
        "contents: read",
        "persist-credentials: false",
        "fetch-depth: 2",
        "10.0.x",
        "qa/c05_01_identity_audits.py A",
        "qa/c05_01_identity_audits.py B",
        "qa/c05_01_identity_audits.py C",
        "Crecisol.AccountIdentity.ContractsTests.csproj",
    ),
}
ALLOWED = {
    ".github/workflows/c05-01-account-model.yml",
    "qa/c05_01_identity_audits.py",
    "docs/c05/IMPLEMENTACION_C05_01_CONTRATOS.md",
    "docs/c05/ACTA_C05_01_CONTRATOS.md",
    "backend/src/Crecisol.Domain/Identity/IdentityAccountModels.cs",
    "backend/src/Crecisol.Application/Identity/ResolveCurrentAccountTenantLink.cs",
    "backend/tests/Crecisol.AccountIdentity.ContractsTests/Crecisol.AccountIdentity.ContractsTests.csproj",
    "backend/tests/Crecisol.AccountIdentity.ContractsTests/Program.cs",
}

def need(ok: bool, reason: str) -> None:
    if not ok:
        raise AssertionError(reason)

def read(path: Path) -> str:
    need(path.is_file(), "Missing required file " + str(path.relative_to(ROOT)))
    return path.read_text(encoding="utf-8")

def payload() -> dict[str, str]:
    return {
        "domain": read(DOMAIN),
        "app": read(APP),
        "test": read(TEST),
        "spec": read(SPEC),
        "workflow": read(WORKFLOW),
    }

def guard(data: dict[str, str]) -> None:
    for key, items in MARKERS.items():
        for item in items:
            need(item in data[key], key + ": required fail-closed guard missing: " + item)

    domain, app, tests, wf = (data["domain"], data["app"], data["test"], data["workflow"])
    need(domain.count("public sealed record") == 7,
         "Domain shall expose exactly 7 typed identity/credential/binding records")
    need(app.count("await accounts.FindByVerifiedActorAsync(") == 2,
         "The trusted account must be read twice around other tenant operations")
    need(app.count("await links.FindExactLinksAsync(") == 2,
         "The exact tenant and actor membership must be rechecked")
    need(app.count("await context.ExecuteAsync(cancellationToken)") == 2,
         "C04 verified host/session/membership context must be rechecked")
    need(app.index("var identity = await context.ExecuteAsync") <
         app.index("var account = await accounts.FindByVerifiedActorAsync") <
         app.index("var matches = await links.FindExactLinksAsync") <
         app.index("var latestAccount = await accounts.FindByVerifiedActorAsync") <
         app.index("var latestLinks = await links.FindExactLinksAsync") <
         app.index("var latestIdentity = await context.ExecuteAsync") <
         app.index("AccountTenantBindingEvidence.FromTrustedInternalSnapshots("),
         "Trusted session/account/tenant order or second reads bypassed")
    need("using Microsoft" not in domain and "using Microsoft" not in app,
         "Framework dependency leaked into Domain/Application")
    for forbidden in ("HttpContext", "HttpRequest", "MySqlConnection",
                      "ConnectionStringBuilder", "SqlCommand", "DbContext",
                      "IConfiguration", "SqlConnection"):
        need(forbidden not in domain and forbidden not in app,
             "Identity contract added HTTP/DB/DSN side effect: " + forbidden)
    need("app.MapPost" not in read(API) and
         "app.MapGet(\"/api/v1/public/branding/platform\"" in read(API),
         "C05-01 must not activate private API endpoints")
    need("workflow_dispatch:" not in wf and "permissions:\n  contents: read" in wf,
         "The new test workflow cannot use privileged dispatches or tokens")
    need("actions/checkout@" in wf and "actions/setup-dotnet@" in wf,
         "CI must use pinned read-only checkout and .NET SDK")
    need("deploy" not in wf.lower() and "kubectl" not in wf.lower(),
         "No deployment or production mutation in CI")
    need("string password" not in app.lower() and "byte[]" not in app.lower(),
         "No credential payload or password material in Application contract")
    need("<TargetFramework>net10.0</TargetFramework>" in read(PROJ) and
         "<TreatWarningsAsErrors>true</TreatWarningsAsErrors>" in read(PROJ),
         "Must compile .NET10 tests with warnings as errors")

def audit_a() -> None:
    need(canonical_active_word(), "Canonical Word/PDF changed")
    digest, paras, begin, end = canonical_c05_source()
    need(digest == C05_SHA and (begin, end, len(paras)) == (1807, 1900, 93),
         "Word C05 authority changed or TOC was extracted")
    plan = read(ROOT / "docs/planificacion-futura/C05_MICROFASES_PROPUESTAS.md")
    need("| C05-01 | Modelo de cuentas y credenciales |" in plan and
         "No cuentas compartidas entre negocios" in plan,
         "C05-01 purpose or negative contract changed")
    prep = read(ROOT / "docs/C05_PREPARACION.md")
    need(PRIOR_DEV in prep and "C05-01" in prep, "C04 prior certification missing")
    for item in (C05_SHA, CURRENT_WORD_SHA, CURRENT_PDF_SHA,
                 "6843853811", "CONTRATOS INTERNOS", "NO CERTIFICADA"):
        source = read(SPEC) + prep
        need(item.casefold() in source.casefold(), "Normative source/boundary absent: " + item)
    guard(payload())
    print("PASS C05-01 A: master Word/PDF SHA and 93 C05 lines, prior C04, precise account model")

def audit_b() -> None:
    data = payload()
    guard(data)
    rejected = 0
    for key in ("domain", "app", "test", "spec", "workflow"):
        for marker in MARKERS[key]:
            changed = dict(data)
            changed[key] = changed[key].replace(marker, "[ADVERSARIAL GUARD REMOVED]")
            need(changed[key] != data[key], "Cannot construct mutation: " + key)
            try:
                guard(changed)
            except AssertionError:
                rejected += 1
            else:
                raise AssertionError("Fail-open mutation accepted: " + key + ":" + marker)
    need(rejected >= 75, "Mutation strength below baseline")
    print(f"PASS C05-01 B: {rejected} adversarial code/document guard mutations rejected")
    print("LIMIT: static mutations supplement, but never replace, actual .NET negative tests")

def git(*args: str) -> str:
    return subprocess.check_output(
        ["git", *args], cwd=ROOT, text=True, stderr=subprocess.DEVNULL).strip()

def audit_c() -> None:
    need(os.environ.get("GITHUB_ACTIONS") == "true",
         "Audit C must run on a genuine GitHub runner")
    sha, run = os.environ.get("GITHUB_SHA", ""), os.environ.get("GITHUB_RUN_ID", "")
    need(re.fullmatch("[0-9a-f]{40}", sha) is not None and
         re.fullmatch("[0-9]+", run) is not None,
         "Runner SHA or run ID invalid")
    need(os.environ.get("GITHUB_REPOSITORY") == "crecisol-platform/CRECISOL",
         "Unexpected repository identity")
    need(git("rev-parse", "HEAD") == sha, "Checked-out code is not the tested run SHA")
    for stage in ("A", "B", "dotnet", "frontend", "chromium", "mysql"):
        marker = PROOF / stage
        need(marker.is_file() and
             marker.read_text(encoding="utf-8").strip() ==
             f"PASS {stage} sha={sha} run={run}",
             "Missing or foreign-source proof: " + stage)
    event = os.environ.get("GITHUB_EVENT_NAME", "")
    if event == "pull_request":
        event_path = Path(os.environ.get("GITHUB_EVENT_PATH", ""))
        need(event_path.is_file(), "PR event not available")
        data = json.loads(event_path.read_text(encoding="utf-8"))
        pr = data["pull_request"]
        base, head = pr["base"], pr["head"]
        need(base["ref"] == "dev" and
             base["repo"]["full_name"] == "crecisol-platform/CRECISOL" and
             head["repo"]["full_name"] == "crecisol-platform/CRECISOL",
             "Unexpected PR target or fork")
        parents = git("rev-list", "--parents", "-n", "1", "HEAD").split()
        need(len(parents) == 3 and
             parents == [sha, base["sha"], head["sha"]],
             "Tested PR synthetic merge must equal actual base and HEAD")
        touched = git("diff", "--name-only", base["sha"], head["sha"]).splitlines()
        print(f"C05-01 provenance: head={head['sha']} base={base['sha']} merge={sha}")
    else:
        need(event == "push" and os.environ.get("GITHUB_REF") == "refs/heads/dev",
             "Unsupported trigger or branch")
        touched = git("show", "--format=", "--name-only", sha).splitlines()
        print(f"C05-01 provenance: dev={sha}")
    need(bool(touched) and set(touched).issubset(ALLOWED),
         "Change scope exceeded C05-01 internal model: " + repr(touched))
    guard(payload())
    print(f"PASS C05-01 C: actual A/B and .NET/Angular/Chromium/MySQL on SHA={sha} run={run}")
    print("LIMIT: internal contracts only; no private login/MFA/session/persistence production")

def main() -> None:
    need(len(sys.argv) == 2 and sys.argv[1] in ("A", "B", "C"),
         "Usage: c05_01_identity_audits.py A|B|C")
    {"A": audit_a, "B": audit_b, "C": audit_c}[sys.argv[1]]()

if __name__ == "__main__":
    try:
        main()
    except (AssertionError, OSError, ValueError, KeyError, subprocess.SubprocessError) as exc:
        print("FAIL C05-01 identity:", exc, file=sys.stderr)
        sys.exit(1)
