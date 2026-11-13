#!/usr/bin/env python3
"""CRECISOL C05-02 scoped issuer: A/B/C gates (no real login or certification)."""
from __future__ import annotations

import json
import os
from pathlib import Path
import re
import subprocess
import sys

from c05_01_preparation_audits import ROOT
from c05_02_independent_smtp_audits import audit_a as predecessor_a

REPO = "crecisol-platform/CRECISOL"
FEATURE = "feature/c05-02-atomic-issuer-scope-20261113"
BASE = "fc20c1af344ea88dfe2eac261822671159b9e158"
EVIDENCE = ROOT / ".c05-02-issuer-scope"
FILES = {
    "app": "backend/src/Crecisol.Application/Identity/IssueScopedInternalInvitation.cs",
    "infra": "backend/src/Crecisol.Infrastructure/Identity/MySqlDisposableScopedInvitationLedger.cs",
    "sql": "database/tests/fixtures/c05_02_issuer_scope_atomic_fixture.sql",
    "test": "backend/tests/Crecisol.IssuerScope.MySqlTests/Program.cs",
    "project": "backend/tests/Crecisol.IssuerScope.MySqlTests/Crecisol.IssuerScope.MySqlTests.csproj",
    "smoke": "database/tests/c05_02_issuer_scope_mysql_smoke.sh",
    "doc": "docs/c05/INTEGRACION_AUTORIZACION_INVITANTE_C05_02.md",
    "qa": "qa/c05_02_issuer_scope_audits.py",
    "workflow": ".github/workflows/c05-02-issuer-scope.yml",
}
ALLOWED = set(FILES.values())
GUARDS = {
    "app": (
        "interface ITrustedScopedInvitationLedger",
        "class IssueScopedInternalInvitation",
        "ResolveCurrentAccountTenantLink",
        "ResolveCurrentBranchResourceScope",
        "TenantRequiredAction.FromServerPolicy(\"identity.invitation.issue\")",
        "issuer.SessionRevision != scope.SessionRevision",
        "issuer.LinkRevision != scope.MembershipRevision",
        "issuer.HostRevision != scope.HostRevision",
        "issuer.TenantCatalogRevision != scope.TenantCatalogRevision",
        "scope.RequiredAction != RequiredAction",
        "issuer != refreshedIssuer",
        "scope != refreshedScope",
        "refreshedDecision != decision",
        "targets.IsEligibleAsync(",
        "profile is not (AccountTenantProfileKind.Employee",
        "capability.CreateDigest()",
        "TryIssueAuthorizedOnceAsync(",
        "capability.Dispose()",
        "TrustedInvitationIssuance(",
    ),
    "infra": (
        "class MySqlDisposableScopedInvitationLedger",
        "ITrustedScopedInvitationLedger",
        "RequiredAction = \"identity.invitation.issue\"",
        "Pooling = false",
        "settings.Database != \"c05_02_disposable\"",
        "settings.AllowLoadLocalInfile",
        "pending.State != InvitationLifecycleState.Pending",
        "pending.TenantId != issuer.TenantId",
        "pending.IssuedBy != issuer.ActorId",
        "scope.ActorId != issuer.ActorId",
        "scope.RequiredAction.Value != RequiredAction",
        "scope.SessionRevision != issuer.SessionRevision",
        "scope.MembershipRevision != issuer.LinkRevision",
        "scope.BranchGrantRevision <= 0",
        "pending.ExpiresUtc - pending.IssuedUtc > TimeSpan.FromDays(3)",
        "c05_02_issue_scoped_once",
        "MySqlDbType.Binary",
        "CryptographicOperations.ZeroMemory(digest)",
        "BranchGrantScope.OwnerOnly",
        "BranchGrantScope.BranchWide",
        "p_resource_rev",
        "p_grant_rev",
        "p_session_rev",
    ),
    "sql": (
        "CREATE TABLE c05_02_auth_accounts",
        "CREATE TABLE c05_02_auth_sessions",
        "CREATE TABLE c05_02_auth_links",
        "CREATE TABLE c05_02_auth_hosts",
        "CREATE TABLE c05_02_auth_branches",
        "CREATE TABLE c05_02_auth_resources",
        "CREATE TABLE c05_02_auth_grants",
        "CREATE TABLE c05_02_auth_issue_policy",
        "FOREIGN KEY (tenant_id,actor_id)",
        "FOREIGN KEY (tenant_id,branch_id)",
        "PRIMARY KEY (tenant_id,actor_id,branch_id,action_key)",
        "CREATE PROCEDURE c05_02_issue_scoped_once",
        "SQL SECURITY DEFINER",
        "START TRANSACTION",
        "FOR UPDATE",
        "DECLARE EXIT HANDLER FOR SQLEXCEPTION",
        "acc.lifecycle='ACTIVE'",
        "sess.lifecycle='ACTIVE'",
        "sess.expires_utc>UTC_TIMESTAMP(6)",
        "link.lifecycle='ACTIVE'",
        "host.lifecycle='VERIFIED'",
        "host.host_revision=p_host_rev",
        "host.catalog_revision=p_catalog_rev",
        "br.lifecycle='ACTIVE'",
        "res.lifecycle='ACTIVE'",
        "grant_row.lifecycle='ACTIVE'",
        "grant_row.revision=p_grant_rev",
        "grant_row.expires_utc>UTC_TIMESTAMP(6)",
        "res.owner_actor_id=p_issuer",
        "p_action='identity.invitation.issue'",
        "policy_row.lifecycle='ACTIVE'",
        "policy_row.max_lifetime_seconds",
        "r.channel_verified=1",
        "r.actor_id<>p_issuer",
        "t.lifecycle='ACTIVE'",
        "t.terms_version=p_terms",
        "lifecycle='PENDING'",
        "SELECT 'DENIED' AS result",
        "SELECT 'ISSUED' AS result",
    ),
    "test": (
        "--integration",
        "MySqlDisposableScopedInvitationLedger",
        "AccountTenantBindingEvidence.FromTrustedInternalSnapshots(",
        "TenantBranchResourceScopeEvidence.FromTrustedInternalSnapshots(",
        "TenantRequiredAction.FromServerPolicy(action)",
        "Remote MySQL rejected before connecting",
        "Tenant A trusted evidence cannot issue to B",
        "Foreign resource and actor scope cannot be passed as A",
        "An Employee issuer cannot forge Consumer/Owner",
        "Stale ",
        "session revocation",
        "membership link revocation",
        "branch suspension",
        "resource owner change",
        "grant revoked",
        "Published 2-hour server policy rejects",
        "Explicit BRANCH_WIDE",
        "16 simultaneous authorized issuers create exactly one",
        "Restricted worker cannot inspect session metadata",
        "Restricted worker cannot fabricate branch permissions",
        "No real identities, passwords, membership grants",
    ),
    "smoke": (
        "mysql:8.4@sha256:",
        "--network bridge -p '127.0.0.1::3306'",
        "openssl rand -hex",
        "c05_02_invitation_atomic_fixture.sql",
        "c05_02_issuer_scope_atomic_fixture.sql",
        "GRANT EXECUTE ON PROCEDURE",
        "issuer_scope_worker",
        "docker rm -f",
        "Crecisol.IssuerScope.MySqlTests.csproj",
        "-- --integration",
    ),
    "project": (
        "<TargetFramework>net10.0</TargetFramework>",
        "<TreatWarningsAsErrors>true</TreatWarningsAsErrors>",
        "Crecisol.Infrastructure.csproj",
    ),
    "doc": (
        "NO IMPLEMENTA autenticación criptográfica real",
        "C05-02 NO CERTIFICADA",
        "C05-03 BLOQUEADA",
        "identity.invitation.issue",
        "Rollback:",
        BASE,
    ),
    "workflow": (
        "contents: read",
        "persist-credentials: false",
        "dotnet-version: '10.0.x'",
        "c05_02_issuer_scope_audits.py A",
        "c05_02_issuer_scope_audits.py B",
        "c05_02_issuer_scope_audits.py C",
        "c05_02_issuer_scope_mysql_smoke.sh",
    ),
}

def need(condition: bool, reason: str) -> None:
    if not condition:
        raise AssertionError(reason)

def sources() -> dict[str, str]:
    return {key: (ROOT / path).read_text(encoding="utf-8")
            for key, path in FILES.items()}

def invariants(data: dict[str, str]) -> None:
    for kind, markers in GUARDS.items():
        for marker in markers:
            need(marker in data[kind], f"Missing {kind} scoped guard: {marker}")
    need("MapPost(" not in data["app"] and
         "HttpContext" not in data["app"] and
         "JWT" not in data["infra"] and
         "INSERT INTO users" not in data["infra"] and
         "InsertMembership" not in data["app"],
         "Scoped issue must not create browser auth/session/grants")
    need("GRANT ALL" not in data["smoke"] and
         "GRANT SELECT ON" not in data["smoke"] and
         "GRANT UPDATE ON" not in data["smoke"],
         "Restricted issuer worker must not read/write direct SQL tables")
    need("workflow_dispatch:" not in data["workflow"] and
         "id-token: write" not in data["workflow"] and
         "deploy" not in data["workflow"].lower(),
         "No manual deployment or privileged CI")
    api = (ROOT / "backend/src/Crecisol.Api/Program.cs").read_text(encoding="utf-8")
    routes = re.findall(r'app\.Map(?:Get|Post|Put|Patch|Delete)\(\s*"([^"]+)"',api)
    need(routes == ["/api/v1/public/branding/platform"],
         "No account or private invitation endpoint may be enabled")
    need("C05-02 NO CERTIFICADA" in data["doc"] and
         "NO IMPLEMENTA autenticación criptográfica real" in data["doc"],
         "Never confuse synthetic session metadata with cryptographic authentication")

def audit_a() -> None:
    predecessor_a()
    invariants(sources())
    print("PASS C05-02 issuer A: Word/C05, existing C04 trusted evidence and zero production auth")

def audit_b() -> None:
    original = sources()
    invariants(original)
    rejected = 0
    for kind, markers in GUARDS.items():
        for marker in markers:
            mutant = dict(original)
            mutant[kind] = mutant[kind].replace(marker, "[REMOVED_GUARD]")
            need(mutant[kind] != original[kind], "Mutation ineffective: "+marker)
            try:
                invariants(mutant)
            except AssertionError:
                rejected += 1
            else:
                raise AssertionError("Unsafe missing guard accepted: "+marker)
    need(rejected >= 95, "Issuer authorization mutation coverage below minimum")
    print(f"PASS C05-02 issuer B: {rejected} negative guard mutations rejected")
    print("NOTE: static mutations cannot replace real SQL concurrency/authorization tests")

def git(*args: str) -> str:
    return subprocess.check_output(["git",*args],cwd=ROOT,
        text=True,stderr=subprocess.DEVNULL).strip()

def audit_c() -> None:
    need(os.environ.get("GITHUB_ACTIONS") == "true" and
         os.environ.get("GITHUB_REPOSITORY") == REPO,
         "Only actual CRECISOL runner may attest SHA/run")
    sha, run = os.environ.get("GITHUB_SHA",""),os.environ.get("GITHUB_RUN_ID","")
    need(re.fullmatch(r"[a-f0-9]{40}",sha) is not None and
         re.fullmatch(r"\d+",run) is not None and
         git("rev-parse","HEAD")==sha,"Unverified commit SHA/run")
    for stage in ("A","B","dotnet","mysql"):
        p = EVIDENCE / stage
        need(p.is_file() and p.read_text(encoding="utf-8").strip() ==
             f"PASS {stage} sha={sha} run={run}",
             "Missing exact runtime evidence for "+stage)
    event = os.environ.get("GITHUB_EVENT_NAME")
    if event=="pull_request":
        path=Path(os.environ.get("GITHUB_EVENT_PATH",""))
        need(path.is_file(),"GitHub event payload missing")
        pr=json.loads(path.read_text(encoding="utf-8"))["pull_request"]
        base,head=pr["base"],pr["head"]
        need(base["ref"]=="dev" and head["ref"]==FEATURE and
             base["repo"]["full_name"]==REPO and
             head["repo"]["full_name"]==REPO,
             "PR not scoped to intended CRECISOL dev feature")
        need(git("rev-list","--parents","-n","1","HEAD").split() ==
             [sha,base["sha"],head["sha"]],
             "GitHub synthetic merge SHA parents mismatch")
        changed=git("diff","--name-only",base["sha"],head["sha"]).splitlines()
    else:
        need(event=="push" and os.environ.get("GITHUB_REF")=="refs/heads/dev",
             "Only dev postmerge run is allowed")
        changed=git("show","--format=","--name-only",sha).splitlines()
    need(bool(changed) and set(changed).issubset(ALLOWED) and
         FILES["app"] in changed and FILES["infra"] in changed and FILES["sql"] in changed,
         "Unauthorized or incomplete scope diff: "+repr(changed))
    invariants(sources())
    print(f"PASS C05-02 issuer C: exact A/B .NET10 MySQL8.4 SHA={sha} run={run}")
    print("NOT CERTIFIED: live signed session, real membership grant, real SMTP or C05-02")

if __name__ == "__main__":
    try:
        need(len(sys.argv)==2 and sys.argv[1] in ("A","B","C"),
             "Usage: c05_02_issuer_scope_audits.py A|B|C")
        {"A":audit_a,"B":audit_b,"C":audit_c}[sys.argv[1]]()
    except (AssertionError,OSError,ValueError,KeyError,
            subprocess.SubprocessError) as exc:
        print("FAIL C05-02 issuer scope audit: "+str(exc),file=sys.stderr)
        sys.exit(1)
