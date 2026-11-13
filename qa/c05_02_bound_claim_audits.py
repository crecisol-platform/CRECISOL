#!/usr/bin/env python3
"""CRECISOL C05-02: bound invitation claim A/B/C, exact SHA/run."""
import json
import os
from pathlib import Path
import re
import subprocess
import sys
from c05_01_preparation_audits import ROOT
from c05_02_issuer_scope_audits import audit_a as predecessor_a

REPO = "crecisol-platform/CRECISOL"
FEATURE = "feature/c05-02-issuer-recheck-on-claim-20261113"
BASE = "4eca74b7c675043dc87f5c195f233503ae815518"
EVIDENCE = ROOT / ".c05-02-bound-claim"
FILES = {
 "app":"backend/src/Crecisol.Application/Identity/ClaimBoundInternalInvitation.cs",
 "infra":"backend/src/Crecisol.Infrastructure/Identity/MySqlDisposableBoundInvitationLedger.cs",
 "sql":"database/tests/fixtures/c05_02_issuer_bound_claim_fixture.sql",
 "test":"backend/tests/Crecisol.BoundClaim.MySqlTests/Program.cs",
 "project":"backend/tests/Crecisol.BoundClaim.MySqlTests/Crecisol.BoundClaim.MySqlTests.csproj",
 "smoke":"database/tests/c05_02_bound_claim_mysql_smoke.sh",
 "doc":"docs/c05/INTEGRACION_REVALIDACION_INVITANTE_AL_CONSUMIR_C05_02.md",
 "qa":"qa/c05_02_bound_claim_audits.py",
 "workflow":".github/workflows/c05-02-bound-claim.yml",
}
ALLOWED = set(FILES.values())
MARKERS = {
 "app": "interface ITrustedBoundInvitationLedger|ITrustedScopedInvitationLedger|class ClaimBoundInternalInvitation|ResolveVerifiedTenantHost|ITrustedCurrentRecipientChannel|rawToken.Length != 32|ReadCurrentDirectHostAsync(|catalog.State != TenantLifecycleState.Active|catalog.AddressMode != verified.AddressMode|!recipient.IsValidAtUtc(first)|currentDirect != direct|refreshedHost != verified|refreshedCatalog != catalog|refreshedRecipient != recipient|final < first|InvitationTokenDigest.FromToken(rawToken.Span)|TryClaimBoundOnceAtomicallyAsync(",
 "infra": "class MySqlDisposableBoundInvitationLedger|settings.Database != \"c05_02_disposable\"|settings.AllowLoadLocalInfile|Pooling = false|c05_02_issue_bound_once|c05_02_claim_bound_once|pending.State != InvitationLifecycleState.Pending|pending.TenantId != issuer.TenantId|pending.IssuedBy != issuer.ActorId|scope.TenantId != issuer.TenantId|scope.RequiredAction.Value != RequiredAction|issuer.SessionRevision != scope.SessionRevision|issuer.LinkRevision != scope.MembershipRevision|issuer.HostRevision != scope.HostRevision|issuer.TenantCatalogRevision != scope.TenantCatalogRevision|scope.BranchGrantRevision <= 0|pending.ExpiresUtc - pending.IssuedUtc > TimeSpan.FromDays(3)|AddId(command, \"p_tenant\"|AddId(command, \"p_invitation\"|AddId(command, \"p_issuer\"|AddId(command, \"p_link\"|AddId(command, \"p_recipient\"|AddLong(command, \"p_grant_rev\"|BranchGrantScope.OwnerOnly|BranchGrantScope.BranchWide|CryptographicOperations.ZeroMemory(digest)|CryptographicOperations.ZeroMemory(protectedDigest)",
 "sql": "CREATE TABLE c05_02_invite_scope_bindings|PRIMARY KEY (tenant_id, invitation_id)|FOREIGN KEY (tenant_id, invitation_id)|FOREIGN KEY (tenant_id, issuer_id)|FOREIGN KEY (tenant_id, branch_id)|FOREIGN KEY (tenant_id, resource_id)|CREATE PROCEDURE c05_02_issue_bound_once|CREATE PROCEDURE c05_02_claim_bound_once|SQL SECURITY DEFINER|START TRANSACTION|FOR UPDATE|DECLARE EXIT HANDLER FOR SQLEXCEPTION|ROLLBACK|COMMIT|INSERT INTO c05_02_invitations|INSERT INTO c05_02_invite_scope_bindings|JOIN c05_02_invite_scope_bindings binding_row|binding_row.invitation_id=invitation.invitation_id|binding_row.issuer_id=invitation.issuer_id|branch_row.branch_id=binding_row.branch_id|resource_row.resource_id=binding_row.resource_id|grant_row.action_key=binding_row.action_key|link_row.link_id=binding_row.issuer_link_id|account_row.revision=binding_row.account_revision|session_row.revision=binding_row.session_revision|link_row.revision=binding_row.link_revision|host_row.host_revision=binding_row.host_revision|host_row.catalog_revision=binding_row.catalog_revision|branch_row.revision=binding_row.branch_revision|resource_row.revision=binding_row.resource_revision|grant_row.revision=binding_row.grant_revision|grant_row.scope=binding_row.scope_mode|binding_row.action_key='identity.invitation.issue'|grant_row.lifecycle='ACTIVE'|session_row.lifecycle='ACTIVE'|session_row.expires_utc>UTC_TIMESTAMP(6)|link_row.lifecycle='ACTIVE'|link_row.expires_utc>UTC_TIMESTAMP(6)|account_row.lifecycle='ACTIVE'|host_row.lifecycle='VERIFIED'|branch_row.lifecycle='ACTIVE'|resource_row.lifecycle='ACTIVE'|recipient.channel_verified=1|recipient.verified_until_utc>UTC_TIMESTAMP(6)|consent.accepted_at_utc<=UTC_TIMESTAMP(6)|tenant.lifecycle='ACTIVE'|tenant.terms_version=invitation.terms_version|issuer.enabled=1|policy_row.lifecycle='ACTIVE'|invitation.token_digest=p_digest|invitation.revision=1|invitation.lifecycle='PENDING'|invitation.issuer_id<>p_actor|recipient.actor_id=p_actor|SELECT IF(ROW_COUNT()=1, 'CLAIMED', 'DENIED')",
 "test": "MySqlDisposableBoundInvitationLedger|INSERT INTO c05_02_consents|TryIssueAuthorizedOnceAsync(|TryClaimBoundOnceAtomicallyAsync(|Legacy invitation without atomically persisted scope cannot be claimed|Wrong tenant cannot claim|Recipient channel B cannot consume A|The issuer cannot claim its own invitation|Incorrect 256-bit capability cannot claim|issuer account suspension|session revocation|session revision|link revocation|link expiry|host revision|branch suspension|resource owner replaced|scope broadened after issuance|original required action replaced|current terms changed|recipient proof revoked|consent removed|resource transferred to another authorized branch|16 simultaneous MySQL claim attempts have exactly one winner|15 simultaneous claim replays fail closed|Worker cannot read confidential issuer scope binding|Worker cannot forge issuance-time revision binding|CryptographicOperations.ZeroMemory(rawA)",
 "smoke": "mysql:8.4@sha256:|openssl rand -hex|--network bridge -p '127.0.0.1::3306'|c05_02_issuer_bound_claim_fixture.sql|GRANT EXECUTE ON PROCEDURE|c05_02_issue_bound_once|c05_02_claim_bound_once|docker rm -f|Crecisol.BoundClaim.MySqlTests.csproj|CRECISOL_C05_02_BOUND_WORKER_DSN",
 "doc": "C05-02 NO CERTIFICADA|C05-03 BLOQUEADA|METADATOS SINTÉTICOS|identity.invitation.issue|c05_02_issue_bound_once|c05_02_claim_bound_once|Rollback:|4eca74b7c675043dc87f5c195f233503ae815518",
 "workflow": "contents: read|persist-credentials: false|dotnet-version: '10.0.x'|c05_02_bound_claim_audits.py A|c05_02_bound_claim_audits.py B|c05_02_bound_claim_audits.py C|c05_02_bound_claim_mysql_smoke.sh",
 "project": "<TargetFramework>net10.0</TargetFramework>|<TreatWarningsAsErrors>true</TreatWarningsAsErrors>|Crecisol.Infrastructure.csproj"
}
MARKERS = {k: v.split("|") for k,v in MARKERS.items()}

def require(condition, message):
    if not condition:
        raise AssertionError(message)

def source():
    return {k:(ROOT / path).read_text(encoding="utf-8") for k,path in FILES.items()}

def invariants(data):
    for category,markers in MARKERS.items():
        for marker in markers:
            require(marker in data[category],
                "Missing guarded code " + category + ": " + marker)
    require("GRANT ALL" not in data["smoke"] and
            "GRANT SELECT ON" not in data["smoke"],
            "Test worker must have EXECUTE only")
    require("MapPost(" not in data["app"] and
            "JwtBearer" not in data["infra"] and
            "HttpContext" not in data["app"] and
            "InsertMembership" not in data["app"],
            "No private HTTP, login, session, or membership grant")
    require("workflow_dispatch:" not in data["workflow"] and
            "id-token: write" not in data["workflow"] and
            "deploy" not in data["workflow"].lower(),
            "No deployment or privileged token")
    api = (ROOT / "backend/src/Crecisol.Api/Program.cs").read_text(encoding="utf-8")
    paths = re.findall(r'app\.Map(?:Get|Post|Put|Patch|Delete)\(\s*"([^"]+)"', api)
    require(paths == ["/api/v1/public/branding/platform"],
            "Private invitation endpoint may not be enabled")
    require("NO IMPLEMENTA identidad ni autenticación criptográfica real" in data["doc"],
            "Must explicitly disclose lack of authenticated identity")

def audit_a():
    predecessor_a()
    invariants(source())
    print("PASS bound claim A: authoritative C05 sources and exact immutable issuer scope")

def audit_b():
    original=source()
    invariants(original)
    rejected=0
    for category,markers in MARKERS.items():
        for marker in markers:
            mutant=dict(original)
            mutant[category]=mutant[category].replace(marker,"[REMOVED_SECURITY_GUARD]")
            require(mutant[category] != original[category], "Ineffective mutation")
            try:
                invariants(mutant)
            except AssertionError:
                rejected+=1
            else:
                raise AssertionError("Unsafe removed guard accepted: "+marker)
    require(rejected >= 110,"Insufficient negative source guard mutations")
    print(f"PASS bound claim B: {rejected} static negative mutations rejected")
    print("NOTE: mutation of text is NOT a substitute for real MySQL security tests")

def git(*args):
    return subprocess.check_output(["git",*args],cwd=ROOT,
        text=True,stderr=subprocess.DEVNULL).strip()

def audit_c():
    require(os.getenv("GITHUB_ACTIONS") == "true" and
            os.getenv("GITHUB_REPOSITORY") == REPO,
            "C requires CRECISOL GitHub CI execution")
    sha=os.getenv("GITHUB_SHA","")
    run=os.getenv("GITHUB_RUN_ID","")
    require(re.fullmatch(r"[a-f0-9]{40}",sha) is not None and
            re.fullmatch(r"\d+",run) is not None and
            git("rev-parse","HEAD")==sha,
            "Exact SHA/run not verified")
    for stage in ("A","B","dotnet","mysql"):
        p=EVIDENCE/stage
        require(p.is_file() and p.read_text(encoding="utf-8").strip()==
                f"PASS {stage} sha={sha} run={run}",
                "Missing or stale evidence: "+stage)
    event=os.getenv("GITHUB_EVENT_NAME")
    if event=="pull_request":
        path=Path(os.getenv("GITHUB_EVENT_PATH",""))
        require(path.is_file(),"PR event payload absent")
        pr=json.loads(path.read_text(encoding="utf-8"))["pull_request"]
        base,head=pr["base"],pr["head"]
        require(base["ref"]=="dev" and head["ref"]==FEATURE
                and base["repo"]["full_name"]==REPO
                and head["repo"]["full_name"]==REPO,
                "Wrong repository, feature branch or PR base")
        require(git("rev-list","--parents","-n","1","HEAD").split()==
                [sha,base["sha"],head["sha"]],
                "PR synthetic merge parents mismatched")
        changed=git("diff","--name-only",base["sha"],head["sha"]).splitlines()
    else:
        require(event=="push" and os.getenv("GITHUB_REF")=="refs/heads/dev",
                "Only dev POSTMERGE valid")
        changed=git("show","--format=","--name-only",sha).splitlines()
    require(bool(changed) and set(changed).issubset(ALLOWED)
            and FILES["app"] in changed and FILES["sql"] in changed
            and FILES["infra"] in changed,
            "Unauthorized/out-of-scope diff: "+repr(changed))
    invariants(source())
    print(f"PASS bound claim C: same SHA/run A+B+.NET10+MySQL8.4 sha={sha} run={run}")
    print("NO FUNCTIONAL C05-02 CERTIFICATION OR PRODUCTION SESSION/GRANT")

if __name__=="__main__":
    try:
        require(len(sys.argv)==2 and sys.argv[1] in ("A","B","C"),
                "Usage: c05_02_bound_claim_audits.py A|B|C")
        {"A":audit_a,"B":audit_b,"C":audit_c}[sys.argv[1]]()
    except (AssertionError,OSError,ValueError,KeyError,
            subprocess.SubprocessError) as error:
        print("FAIL C05-02 bound claim audit: "+str(error),file=sys.stderr)
        sys.exit(1)
