#!/usr/bin/env python3
"""C05-02 real contract A/B/C gates (NOT production signup/invitation certification)."""
from __future__ import annotations
import json
import os
from pathlib import Path
import re
import subprocess
import sys

from c05_01_preparation_audits import ROOT, canonical_c05_source
from c05_02_preparation_audits import audit_a as preparation_a, audit_b as preparation_b
from master_source_guard import canonical_active_word, CURRENT_WORD_SHA, CURRENT_PDF_SHA

DOMAIN = ROOT / "backend/src/Crecisol.Domain/Identity/InvitationSecurityContracts.cs"
APP = ROOT / "backend/src/Crecisol.Application/Identity/InvitationUseCases.cs"
TEST = ROOT / "backend/tests/Crecisol.InvitationSecurity.ContractsTests/Program.cs"
PROJ = ROOT / "backend/tests/Crecisol.InvitationSecurity.ContractsTests/Crecisol.InvitationSecurity.ContractsTests.csproj"
DOC = ROOT / "docs/c05/IMPLEMENTACION_C05_02_CONTRATOS.md"
WORKFLOW = ROOT / ".github/workflows/c05-02-invitations.yml"
API = ROOT / "backend/src/Crecisol.Api/Program.cs"
SQL_SCHEMA = ROOT / "database/tests/fixtures/c05_02_invitation_atomic_fixture.sql"
SQL_TEST = ROOT / "database/tests/c05_02_invitation_atomic_smoke.sh"
PROOF = ROOT / ".c05-02-invitations-proof"
SHA_C05 = "bf7036849d6f1aad36a3a111b62121947aa12fae8c2a0551e63b0cc788d4a0aa"
PRIOR_SHA = "8468a448b3164944fb75fb17270e1d3dc2186c79"
FEATURES = {
    "feature/c05-02-invitations-core-20261112",
    "feature/c05-02-mysql-ledger-integration-20261113",
}
REPO = "crecisol-platform/CRECISOL"
ALLOWED = {
    "backend/src/Crecisol.Domain/Identity/InvitationSecurityContracts.cs",
    "backend/src/Crecisol.Infrastructure/Identity/MySqlDisposableInvitationLedger.cs",
    "backend/tests/Crecisol.InvitationSecurity.MySqlIntegrationTests/Program.cs",
    "backend/tests/Crecisol.InvitationSecurity.MySqlIntegrationTests/Crecisol.InvitationSecurity.MySqlIntegrationTests.csproj",
    "database/tests/c05_02_dotnet_mysql_adapter_smoke.sh",
    "qa/c05_02_mysql_adapter_audits.py",
    "docs/c05/IMPLEMENTACION_C05_02_MYSQL_ADAPTER.md",
    ".github/workflows/c05-02-mysql-ledger.yml",
    "backend/src/Crecisol.Application/Identity/InvitationUseCases.cs",
    "backend/tests/Crecisol.InvitationSecurity.ContractsTests/Program.cs",
    "backend/tests/Crecisol.InvitationSecurity.ContractsTests/Crecisol.InvitationSecurity.ContractsTests.csproj",
    "docs/c05/IMPLEMENTACION_C05_02_CONTRATOS.md",
    "docs/c05/ACTA_C05_02_CONTRATOS.md",
    "database/tests/fixtures/c05_02_invitation_atomic_fixture.sql",
    "database/tests/c05_02_invitation_atomic_smoke.sh",
    "qa/c05_02_invitations_audits.py",
    ".github/workflows/c05-02-invitations.yml",
}
GUARDS = {
    "domain": (
        "public sealed record InvitationId",
        "public sealed record RecipientChannelBindingId",
        "public sealed class InvitationTokenDigest",
        "public sealed class EphemeralInvitationCode",
        "public sealed record InvitationSnapshot",
        "public sealed record VerifiedRecipientChannel",
        "public sealed record VerifiedInvitationTermsAcceptance",
        "public sealed class NeutralRegistrationReceipt",
        "RandomNumberGenerator.GetBytes(32)",
        "SHA256.HashData(message)",
        "CryptographicOperations.FixedTimeEquals(digest, computed.digest)",
        "CryptographicOperations.ZeroMemory(message)",
        "CryptographicOperations.ZeroMemory(bytes)",
        "Interlocked.Exchange(ref secret, null)",
        "candidate.Length != 32",
        "profile is not (AccountTenantProfileKind.Employee or AccountTenantProfileKind.Consumer)",
        "State == InvitationLifecycleState.Pending",
        "now < ExpiresUtc",
        "TermsVersion",
        "public override string ToString() => \"InvitationSnapshot[redacted]\";",
        "public override string ToString() => \"EphemeralInvitationCode[redacted]\";",
        "REQUEST_ACCEPTED_IF_ELIGIBLE",
    ),
    "app": (
        "ResolveCurrentAccountTenantLink issuerResolver",
        "ITrustedInvitationIssuerPolicy",
        "ITrustedInvitationTargetDirectory",
        "ITrustedInvitationLedger",
        "ITrustedCurrentRecipientChannel",
        "ITrustedInvitationConsentLedger",
        "ITrustedCurrentInvitationTermsPolicy",
        "currentTermsVersion.Value != invitation.TermsVersion",
        "refreshedTermsVersion != currentTermsVersion",
        "ITrustedCurrentDirectHostReader directHost",
        "ResolveVerifiedTenantHost hostResolver",
        "TryInsertUniqueAsync(snapshot, cancellationToken)",
        "TryClaimOnceAtomicallyAsync(",
        "profile is not (AccountTenantProfileKind.Employee or AccountTenantProfileKind.Consumer)",
        "!decision.Allowed",
        "targets.IsEligibleAsync(issuer.TenantId, recipient, cancellationToken)",
        "refreshedIssuer != issuer || refreshedPolicy != decision",
        "new TrustedInvitationIssuance(snapshot.Id, capability)",
        "capability.Dispose()",
        "rawCode.Length != 32",
        "verifiedHost.TenantId",
        "tenant.State != TenantLifecycleState.Active",
        "recipient is null || !recipient.IsValidAtUtc(firstNow)",
        "accepted.TermsVersion != invitation.TermsVersion",
        "invitation.TenantId != verifiedHost.TenantId",
        "invitation.Recipient != recipient.Recipient",
        "invitation.IssuedBy == recipient.ActorId",
        "!invitation.IsPendingAtUtc(firstNow)",
        "!invitation.Digest.Matches(rawCode.Span)",
        "refreshedMatches[0].Revision != invitation.Revision",
        "refreshedHost != verifiedHost || refreshedTenant != tenant",
        "finalNow < firstNow",
        "return new InternalInvitationClaimReceipt(invitation.Id)",
        "public override string ToString() => \"InternalInvitationClaimReceipt[redacted]\";",
    ),
    "test": (
        "Sixteen parallel token consumptions have EXACTLY ONE winner",
        "Replay after consumption denied",
        "Host B cannot claim invitation issued to tenant A",
        "Unknown invite profile cannot grant membership",
        "Unapproved issuer fails closed",
        "Wrong 256-bit secret rejected",
        "Expired invite denied",
        "Invitation revoked denied",
        "Duplicate catalog rows denied",
        "Stale terms policy denied",
        "Published tenant policy changed denied",
        "Published tenant terms drift mid-flight denied",
        "Consent from tenant B denied",
        "Suspended tenant denied",
        "Tenant suspended only at atomic port denied",
        "Missing recipient verification denied",
        "Invitation issuer revoked before atomic update denied",
        "Neutral acknowledgement carries neither account existence nor recipient email",
        "No token persisted after changing policy",
        "Claim input cannot select tenant, actor, role or backend DSN",
        "LIMIT: synthetic trusted ports",
    ),
    "doc": (
        "C05-02",
        "NO CERTIFICADA",
        "T01..T20",
        "B01",
        "B18",
        "NO EJECUTADAS",
        "C05-03",
        "C05-01",
        PRIOR_SHA,
        SHA_C05,
        CURRENT_WORD_SHA,
        CURRENT_PDF_SHA,
    ),
    "sql": (
        "CREATE TABLE c05_02_invitations",
        "PRIMARY KEY (tenant_id, invitation_id)",
        "UNIQUE KEY uq_c05_02_digest_tenant",
        "SQL SECURITY DEFINER",
        "UPDATE c05_02_invitations i",
        "t.lifecycle = 'ACTIVE'",
        "t.terms_version = i.terms_version",
        "i.lifecycle = 'PENDING'",
        "i.revision = p_revision",
        "UTC_TIMESTAMP(6) < i.expires_at_utc",
        "r.channel_verified = 1",
        "s.enabled = 1",
        "ROW_COUNT() = 1",
    ),
    "sqltest": (
        "docker run --rm -d --network none",
        "16 real MySQL clients exactly ONE winner",
        "A invitation ID with B tenant and A digest",
        "replay of claimed A token",
        "revoked issuer",
        "terms upgraded without consent",
        "no membership grants created",
    ),
    "workflow": (
        "contents: read",
        "persist-credentials: false",
        "fetch-depth: 2",
        "10.0.x",
        "qa/c05_02_invitations_audits.py A",
        "qa/c05_02_invitations_audits.py B",
        "qa/c05_02_invitations_audits.py C",
        "Crecisol.InvitationSecurity.ContractsTests.csproj",
        "bash database/tests/c05_02_invitation_atomic_smoke.sh",
    ),
}

def need(ok: bool, message: str) -> None:
    if not ok:
        raise AssertionError(message)

def read(p: Path) -> str:
    need(p.is_file(), "Required file missing " + str(p.relative_to(ROOT)))
    return p.read_text(encoding="utf-8")

def files() -> dict[str, str]:
    return {
        "domain": read(DOMAIN),
        "app": read(APP),
        "test": read(TEST),
        "doc": read(DOC),
        "workflow": read(WORKFLOW),
        "sql": read(SQL_SCHEMA),
        "sqltest": read(SQL_TEST),
    }

def guard(data: dict[str, str]) -> None:
    for key, markers in GUARDS.items():
        for item in markers:
            need(item in data[key], "Fail-closed marker missing: " + key + ":" + item)
    domain, app, test, wf = (
        data["domain"], data["app"], data["test"], data["workflow"])
    need(domain.count("public sealed ") >= 9,
         "Domain contract is missing strongly typed immutable invitation models")
    need(app.count("await issuerResolver.ExecuteAsync(cancellationToken)") == 2,
         "The issuer must recheck C04/C05-01 verified context")
    need(app.count("await policy.EvaluateAsync(") == 2 and
         app.count("targets.IsEligibleAsync(") == 2,
         "Independent invitation authority and recipient must be rechecked")
    need(app.count("ledger.ReadExactAsync(") == 2,
         "Tenant-scoped invitation must be reread for revision drift")
    need("using Microsoft." not in domain and "using Microsoft." not in app,
         "No HTTP/EF framework dependencies in Domain/Application")
    for forbidden in ("HttpContext", "HttpRequest", "ConnectionString",
                      "MySqlConnection", "DbContext", "SqlCommand", "IConfiguration"):
        need(forbidden not in domain and forbidden not in app,
             "Out-of-scope HTTP/SQL/DSN implementation in contracts: " + forbidden)
    api = read(API)
    routes = re.findall(r'app\.Map(?:Get|Post|Put|Patch|Delete)\(\s*"([^"]+)"', api)
    need(routes == ["/api/v1/public/branding/platform"],
         "No new private identity endpoints allowed in this internal phase")
    need("workflow_dispatch:" not in wf and "id-token: write" not in wf and
         "permissions:\n  contents: read" in wf,
         "CI must not grant privileged actions")
    need("deploy" not in wf.lower() and "kubectl" not in wf.lower(),
         "No production deployment allowed")
    need("<TargetFramework>net10.0</TargetFramework>" in read(PROJ) and
         "<TreatWarningsAsErrors>true</TreatWarningsAsErrors>" in read(PROJ),
         ".NET 10 test project must build with strict warnings")
    need("GRANT ALL" not in data["sql"] and "DROP DATABASE" not in data["sqltest"],
         "SQL fixture may not grant broad privileges or touch production databases")
    need("Async" in app and "Task" in app and "CancellationToken" in app,
         "Asynchronous cancellation and typed ports required")

def audit_a() -> None:
    need(canonical_active_word(), "Canonical Word/PDF integrity failed")
    digest, pars, start, stop = canonical_c05_source()
    need(digest == SHA_C05 and (len(pars), start, stop) == (93, 1807, 1900),
         "C05 Word source changed")
    preparation_a()
    source = read(DOC)
    for marker in (SHA_C05, CURRENT_WORD_SHA, CURRENT_PDF_SHA, PRIOR_SHA, "C05-03"):
        need(marker in source, "Normative trace missing " + marker)
    guard(files())
    print("PASS C05-02 A: canonical Word, 11 phases, predecessor, internal invite contracts and tests")

def audit_b() -> None:
    preparation_b()
    data = files()
    guard(data)
    rejected = 0
    for key, markers in GUARDS.items():
        for item in markers:
            mutated = dict(data)
            mutated[key] = mutated[key].replace(item, "[REMOVED REQUIRED SECURITY BARRIER]")
            need(mutated[key] != data[key], "Cannot mutate guard " + item)
            try:
                guard(mutated)
            except AssertionError:
                rejected += 1
            else:
                raise AssertionError("Fail-open mutation accepted: " + key + ":" + item)
    need(rejected >= 80, "Insufficient mutation coverage")
    print(f"PASS C05-02 B: {rejected} adversarial code/document guards rejected")
    print("LIMIT: no HTTP/email/SQL invitation production transaction exists")

def git(*args: str) -> str:
    return subprocess.check_output(["git", *args], cwd=ROOT,
                                   text=True, stderr=subprocess.DEVNULL).strip()

def audit_c() -> None:
    need(os.environ.get("GITHUB_ACTIONS") == "true",
         "Audit C requires an actual GitHub runner")
    sha, run = os.environ.get("GITHUB_SHA", ""), os.environ.get("GITHUB_RUN_ID", "")
    need(re.fullmatch(r"[a-f0-9]{40}", sha) is not None and
         re.fullmatch(r"\d+", run) is not None and
         os.environ.get("GITHUB_REPOSITORY") == REPO and
         git("rev-parse", "HEAD") == sha, "Missing exact verified SHA/run provenance")
    for stage in ("A", "B", "dotnet", "frontend", "chromium", "mysql", "mysql_invites"):
        marker = PROOF / stage
        need(marker.is_file() and
             marker.read_text(encoding="utf-8").strip() ==
             f"PASS {stage} sha={sha} run={run}",
             "Stage was not run on same SHA/run: " + stage)
    if os.environ.get("GITHUB_EVENT_NAME") == "pull_request":
        event_path = Path(os.environ.get("GITHUB_EVENT_PATH", ""))
        need(event_path.is_file(), "PR event missing")
        pr = json.loads(event_path.read_text(encoding="utf-8"))["pull_request"]
        b, h = pr["base"], pr["head"]
        need(b["ref"] == "dev" and h["ref"] in FEATURES and
             b["repo"]["full_name"] == REPO and h["repo"]["full_name"] == REPO,
             "Unexpected PR base/head/fork")
        parents = git("rev-list", "--parents", "-n", "1", "HEAD").split()
        need(parents == [sha, b["sha"], h["sha"]], "PR synthetic SHA not actual merge")
        changed = git("diff", "--name-only", b["sha"], h["sha"]).splitlines()
        print(f"C05-02 provenance: PR HEAD={h['sha']} BASE={b['sha']} tested={sha}")
    else:
        need(os.environ.get("GITHUB_EVENT_NAME") == "push" and
             os.environ.get("GITHUB_REF") == "refs/heads/dev",
             "Unexpected CI event or protected destination")
        changed = git("show", "--format=", "--name-only", sha).splitlines()
        print(f"C05-02 provenance: dev SHA={sha}")
    need(bool(changed) and set(changed).issubset(ALLOWED),
         "C05-02 touches out-of-scope files: " + repr(changed))
    guard(files())
    print(f"PASS C05-02 C: A/B .NET/Angular/Chromium/MySQL + 16-session InnoDB fixture SHA={sha} run={run}")
    print("LIMIT: internal invitation contracts and synthetic fakes only; no production signup")

if __name__ == "__main__":
    try:
        need(len(sys.argv) == 2 and sys.argv[1] in ("A", "B", "C"),
             "Usage: c05_02_invitations_audits.py A|B|C")
        {"A": audit_a, "B": audit_b, "C": audit_c}[sys.argv[1]]()
    except (AssertionError, OSError, ValueError, KeyError, subprocess.SubprocessError) as ex:
        print("FAIL C05-02 invitation audit:", ex, file=sys.stderr)
        sys.exit(1)
