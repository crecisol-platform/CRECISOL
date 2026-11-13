#!/usr/bin/env python3
"""CRECISOL C05-02: test-only signed recipient HMAC + MySQL8.4 A/B/C."""
import json
import os
from pathlib import Path
import re
import subprocess
import sys
from c05_01_preparation_audits import ROOT
from c05_02_bound_claim_audits import audit_a as predecessor_a

REPO = "crecisol-platform/CRECISOL"
FEATURE = "feature/c05-02-signed-recipient-claim-20261113"
BASE = "cd63c7ba87bed110f4844ada26201258649facae"
EVIDENCE = ROOT / ".c05-02-signed-recipient"
FILES = {
  "app": "backend/src/Crecisol.Application/Identity/ClaimWithSignedRecipientAssertion.cs",
  "infra": "backend/src/Crecisol.Infrastructure/Identity/MySqlDisposableSignedRecipientClaimLedger.cs",
  "sql": "database/tests/fixtures/c05_02_signed_recipient_claim_fixture.sql",
  "test": "backend/tests/Crecisol.SignedRecipient.MySqlTests/Program.cs",
  "project": "backend/tests/Crecisol.SignedRecipient.MySqlTests/Crecisol.SignedRecipient.MySqlTests.csproj",
  "smoke": "database/tests/c05_02_signed_recipient_mysql_smoke.sh",
  "doc": "docs/c05/INTEGRACION_PRUEBA_FIRMADA_DESTINATARIO_C05_02.md",
  "qa": "qa/c05_02_signed_recipient_audits.py",
  "workflow": ".github/workflows/c05-02-signed-recipient.yml"
}
ALLOWED = set(FILES.values())
MARKERS = {
  "infra": [
    "private const int BodyLength = 93;",
    "private const int ProofLength = 125;",
    "private const byte FormatVersion = 1;",
    "private readonly int activeKeyEpoch;",
    "string? serverHeldLocalTestDsn, byte[] serverHeldSecret, int keyEpoch)",
    "if (serverHeldSecret.Length != 32 || keyEpoch < 1 ||",
    "if (options.Database != RequiredDatabase ||",
    "options.Server is not (\"127.0.0.1\" or \"localhost\" or \"::1\") ||",
    "activeKeyEpoch = keyEpoch;",
    "signingKey = HMACSHA256.HashData(serverHeldSecret,",
    "public async Task<bool> TryClaimWithVerifiedAssertionOnceAsync(",
    "ReadOnlyMemory<byte> rawInvitationCapability,",
    "ReadOnlyMemory<byte> signedRecipientAssertion,",
    "if (rawInvitationCapability.Length != 32 ||",
    "!TryVerify(signedRecipientAssertion.Span, trustedNowUtc, out var proof) ||",
    "return false;",
    "await using var cmd = new MySqlCommand(\"c05_02_claim_signed_once\", db)",
    "CommandType = CommandType.StoredProcedure",
    "Id(cmd,\"p_signed_session\",proof.Session);",
    "cmd.Parameters.Add(\"p_signed_revision\",MySqlDbType.Int64).Value=proof.Revision;",
    "cmd.Parameters.Add(\"p_signed_key_epoch\",MySqlDbType.Int32).Value=proof.KeyEpoch;",
    "cmd.Parameters.Add(\"p_signed_issued\",MySqlDbType.DateTime).Value=proof.IssuedUtc;",
    "cmd.Parameters.Add(\"p_signed_expires\",MySqlDbType.DateTime).Value=proof.ExpiresUtc;",
    "return result switch",
    "CryptographicOperations.ZeroMemory(protectedDigest);",
    "private bool TryVerify(ReadOnlySpan<byte> input,",
    "if (input.Length != ProofLength || input[0] != FormatVersion)",
    "var keyEpoch = BinaryPrimitives.ReadInt32BigEndian(input.Slice(1,4));",
    "if (keyEpoch != activeKeyEpoch)",
    "var data = new byte[Purpose.Length+BodyLength];",
    "input[..BodyLength].CopyTo(data.AsSpan(Purpose.Length));",
    "expected = HMACSHA256.HashData(signingKey,data);",
    "if (!CryptographicOperations.FixedTimeEquals(",
    "expected,input.Slice(BodyLength,32)))",
    "CryptographicOperations.ZeroMemory(data);",
    "CryptographicOperations.ZeroMemory(expected);",
    "var tenant = MySqlRfc4122Guid.Decode(input.Slice(5,16).ToArray());",
    "var actor = MySqlRfc4122Guid.Decode(input.Slice(21,16).ToArray());",
    "var recipient = MySqlRfc4122Guid.Decode(input.Slice(37,16).ToArray());",
    "var session = MySqlRfc4122Guid.Decode(input.Slice(53,16).ToArray());",
    "var revision = BinaryPrimitives.ReadInt64BigEndian(input.Slice(69,8));",
    "var issued = BinaryPrimitives.ReadInt64BigEndian(input.Slice(77,8));",
    "var expires = BinaryPrimitives.ReadInt64BigEndian(input.Slice(85,8));",
    "if (tenant==Guid.Empty || actor==Guid.Empty || recipient==Guid.Empty ||",
    "session==Guid.Empty || revision<1 ||",
    "expires<=issued || expires-issued is <30 or >300)",
    "issued>nowSeconds || issued<nowSeconds-300 ||",
    "tenant,actor,recipient,session,revision,keyEpoch,",
    "private static void Id(MySqlCommand cmd,string name,Guid id) =>",
    "cmd.Parameters.Add(name,MySqlDbType.Binary).Value=MySqlRfc4122Guid.Encode(id);",
    "if (disposed) return;",
    "CryptographicOperations.ZeroMemory(signingKey);",
    "disposed=true;",
    "long Revision,int KeyEpoch,DateTime IssuedUtc,DateTime ExpiresUtc);"
  ],
  "sql": [
    "CREATE TABLE c05_02_signed_recipient_sessions (",
    "PRIMARY KEY (tenant_id,session_id),",
    "FOREIGN KEY (tenant_id,recipient_id)",
    "CHECK (revision>0 AND key_epoch>0),",
    "CREATE PROCEDURE c05_02_claim_signed_once(",
    "IN p_signed_session BINARY(16), IN p_signed_revision BIGINT UNSIGNED,",
    "SQL SECURITY DEFINER",
    "JOIN c05_02_invite_scope_bindings binding_row",
    "ON binding_row.tenant_id=invitation.tenant_id",
    "AND binding_row.invitation_id=invitation.invitation_id",
    "AND binding_row.issuer_id=invitation.issuer_id",
    "JOIN c05_02_tenants tenant ON tenant.tenant_id=invitation.tenant_id",
    "JOIN c05_02_issuers issuer ON issuer.tenant_id=binding_row.tenant_id",
    "AND issuer.actor_id=binding_row.issuer_id",
    "JOIN c05_02_auth_accounts account_row ON account_row.tenant_id=issuer.tenant_id",
    "AND account_row.actor_id=issuer.actor_id",
    "JOIN c05_02_auth_sessions session_row ON session_row.tenant_id=issuer.tenant_id",
    "AND session_row.actor_id=issuer.actor_id",
    "JOIN c05_02_auth_links link_row ON link_row.tenant_id=issuer.tenant_id",
    "AND link_row.actor_id=issuer.actor_id",
    "AND link_row.link_id=binding_row.issuer_link_id",
    "JOIN c05_02_auth_hosts host_row ON host_row.tenant_id=tenant.tenant_id",
    "JOIN c05_02_auth_branches branch_row ON branch_row.tenant_id=tenant.tenant_id",
    "AND branch_row.branch_id=binding_row.branch_id",
    "JOIN c05_02_auth_resources resource_row ON resource_row.tenant_id=branch_row.tenant_id",
    "AND resource_row.branch_id=branch_row.branch_id",
    "AND resource_row.resource_id=binding_row.resource_id",
    "JOIN c05_02_auth_grants grant_row ON grant_row.tenant_id=issuer.tenant_id",
    "AND grant_row.actor_id=issuer.actor_id",
    "AND grant_row.branch_id=binding_row.branch_id",
    "AND grant_row.action_key=binding_row.action_key",
    "JOIN c05_02_auth_issue_policy policy_row ON policy_row.tenant_id=tenant.tenant_id",
    "AND policy_row.profile_scope=invitation.profile_scope",
    "AND policy_row.action_key=binding_row.action_key",
    "JOIN c05_02_recipients recipient ON recipient.tenant_id=invitation.tenant_id",
    "AND recipient.recipient_id=invitation.recipient_id",
    "JOIN c05_02_signed_recipient_sessions signed_session",
    "ON signed_session.tenant_id=recipient.tenant_id",
    "AND signed_session.recipient_id=recipient.recipient_id",
    "AND signed_session.actor_id=recipient.actor_id",
    "AND signed_session.session_id=p_signed_session",
    "JOIN c05_02_consents consent ON consent.tenant_id=recipient.tenant_id",
    "AND consent.recipient_id=recipient.recipient_id",
    "AND consent.terms_version=invitation.terms_version",
    "SET invitation.lifecycle='CLAIMED',",
    "WHERE invitation.tenant_id=p_tenant",
    "AND invitation.invitation_id=p_invitation",
    "AND invitation.recipient_id=p_recipient",
    "AND invitation.token_digest=p_digest",
    "AND invitation.revision=1",
    "AND invitation.lifecycle='PENDING'",
    "AND invitation.issued_at_utc<=UTC_TIMESTAMP(6)",
    "AND UTC_TIMESTAMP(6)<invitation.expires_at_utc",
    "AND invitation.issuer_id<>p_actor",
    "AND recipient.actor_id=p_actor",
    "AND signed_session.lifecycle='ACTIVE'",
    "AND signed_session.revision=p_signed_revision",
    "AND signed_session.key_epoch=p_signed_key_epoch",
    "AND signed_session.issued_utc=p_signed_issued",
    "AND signed_session.expires_utc=p_signed_expires",
    "AND signed_session.issued_utc<=UTC_TIMESTAMP(6)",
    "AND signed_session.expires_utc>UTC_TIMESTAMP(6)",
    "AND TIMESTAMPDIFF(SECOND,signed_session.issued_utc,signed_session.expires_utc)",
    "BETWEEN 30 AND 300"
  ],
  "test": [
    "?? throw new InvalidOperationException(\"Test signing secret required.\");",
    "if(master.Length!=32) throw new InvalidOperationException(\"Test signing key length required.\");",
    "var testKey=HMACSHA256.HashData(master,",
    "void Pass(bool ok,string text)",
    "INSERT INTO c05_02_tenants VALUES",
    "INSERT INTO c05_02_issuers VALUES",
    "INSERT INTO c05_02_recipients VALUES",
    "INSERT INTO c05_02_consents VALUES",
    "INSERT INTO c05_02_auth_accounts VALUES",
    "INSERT INTO c05_02_auth_sessions VALUES",
    "INSERT INTO c05_02_auth_links VALUES",
    "INSERT INTO c05_02_auth_hosts VALUES",
    "INSERT INTO c05_02_auth_branches VALUES",
    "INSERT INTO c05_02_auth_resources VALUES",
    "INSERT INTO c05_02_auth_grants VALUES",
    "INSERT INTO c05_02_auth_issue_policy VALUES",
    "INSERT INTO c05_02_signed_recipient_sessions",
    "byte[] SignTestOnly(TenantId tenant,TenantActorId actor,",
    "var tag=HMACSHA256.HashData(overrideKey??testKey,message);",
    "CryptographicOperations.ZeroMemory(tag);",
    "CryptographicOperations.ZeroMemory(message);",
    "var code=RandomNumberGenerator.GetBytes(32);",
    "INSERT INTO c05_02_invitations",
    "INSERT INTO c05_02_invite_scope_bindings",
    "catch { CryptographicOperations.ZeroMemory(code); throw; }",
    "finally { CryptographicOperations.ZeroMemory(digest); }",
    "async Task<bool> Claim(TenantId tenant,InvitationId invitation,",
    "await adapter.TryClaimWithVerifiedAssertionOnceAsync(tenant,invitation,",
    "var proofA=SignTestOnly(tenantA,actorA,",
    "var proofA2=SignTestOnly(tenantA,actorA2,",
    "var proofB=SignTestOnly(tenantB,actorB,",
    "Pass(!await Claim(tenantB,a.Id,a.Code,proofA),",
    "Pass(!await Claim(tenantA,a.Id,a.Code,proofB),",
    "Pass(!await Claim(tenantA,b.Id,b.Code,proofA),",
    "Pass(!await Claim(tenantA,a.Id,RandomNumberGenerator.GetBytes(32),proofA),",
    "Pass(!await Claim(tenantA,a.Id,new byte[31],proofA),",
    "Pass(!await Claim(tenantA,a.Id,a.Code,Array.Empty<byte>()),",
    "try { Pass(!await Claim(tenantA,a.Id,a.Code,corrupted),",
    "\"Tampered signature or signed field denied at offset \"+bit); }",
    "finally { CryptographicOperations.ZeroMemory(corrupted); }",
    "var alienKey=RandomNumberGenerator.GetBytes(32);",
    "var foreign=SignTestOnly(tenantA,actorA,",
    "try { Pass(!await Claim(tenantA,a.Id,a.Code,foreign),",
    "finally { CryptographicOperations.ZeroMemory(foreign);",
    "CryptographicOperations.ZeroMemory(alienKey); }",
    "var rotated=SignTestOnly(tenantA,actorA,",
    "try { Pass(!await Claim(tenantA,a.Id,a.Code,rotated),",
    "finally { CryptographicOperations.ZeroMemory(rotated); }",
    "var stale=SignTestOnly(tenantA,actorA,",
    "try { Pass(!await Claim(tenantA,a.Id,a.Code,stale),",
    "finally { CryptographicOperations.ZeroMemory(stale); }",
    "var expired=SignTestOnly(tenantA,actorA,",
    "try { Pass(!await Claim(tenantA,a.Id,a.Code,expired),",
    "finally { CryptographicOperations.ZeroMemory(expired); }",
    "var future=SignTestOnly(tenantA,actorA,",
    "try { Pass(!await Claim(tenantA,a.Id,a.Code,future),",
    "finally { CryptographicOperations.ZeroMemory(future); }"
  ],
  "app": [
    "public interface ITrustedSignedRecipientClaimLedger",
    "Task<bool> TryClaimWithVerifiedAssertionOnceAsync(",
    "public sealed class ClaimWithSignedRecipientAssertion",
    "this.directHost = directHost ?? throw new ArgumentNullException(nameof(directHost));",
    "this.hostResolver = hostResolver ?? throw new ArgumentNullException(nameof(hostResolver));",
    "this.tenants = tenants ?? throw new ArgumentNullException(nameof(tenants));",
    "this.ledger = ledger ?? throw new ArgumentNullException(nameof(ledger));",
    "this.clock = clock ?? throw new ArgumentNullException(nameof(clock));",
    "public async Task<InternalInvitationClaimReceipt> ExecuteAsync(",
    "if (rawInvitationCapability.Length != 32 || signedRecipientAssertion.Length != 125)",
    "throw new UnauthorizedAccessException(\"Invitation claim unavailable.\");",
    "var direct = await directHost.ReadCurrentDirectHostAsync(cancellationToken);",
    "if (direct is null)",
    "var host = await hostResolver.ResolveAsync(direct, cancellationToken);",
    "var catalog = await tenants.FindByIdAsync(host.TenantId, cancellationToken);",
    "if (catalog is null || catalog.TenantId != host.TenantId ||",
    "var currentDirect = await directHost.ReadCurrentDirectHostAsync(cancellationToken);",
    "var freshHost = await hostResolver.ResolveAsync(direct, cancellationToken);",
    "var freshCatalog = await tenants.FindByIdAsync(host.TenantId, cancellationToken);",
    "if (currentDirect != direct || freshHost != host || freshCatalog != catalog)",
    "if (finalNow < firstNow || catalog.UpdatedAtUtc > finalNow)",
    "if (!await ledger.TryClaimWithVerifiedAssertionOnceAsync(",
    "return new InternalInvitationClaimReceipt(invitationId);"
  ],
  "smoke": [
    "IMAGE=\"mysql:8.4@sha256:a81d043930e51745522bd5c430961cf8286f0a23f0a9a2a6e0108f7ea71b0902\"",
    "NAME=\"crecisol-c0502-signed-$(openssl rand -hex 8)\"",
    "ROOT_PASSWORD=\"$(openssl rand -hex 24)\"",
    "ADMIN_PASSWORD=\"$(openssl rand -hex 24)\"",
    "WORKER_PASSWORD=\"$(openssl rand -hex 24)\"",
    "SIGNING_MASTER_HEX=\"$(openssl rand -hex 32)\"",
    "cleanup() { docker rm -f \"$NAME\" >/dev/null 2>&1 || true; }",
    "echo \"::add-mask::$secret\"",
    "docker run --rm -d --name \"$NAME\" --network bridge -p '127.0.0.1::3306' \\",
    "database/tests/fixtures/c05_02_invitation_atomic_fixture.sql \\",
    "database/tests/fixtures/c05_02_issuer_scope_atomic_fixture.sql \\",
    "database/tests/fixtures/c05_02_issuer_bound_claim_fixture.sql \\",
    "database/tests/fixtures/c05_02_signed_recipient_claim_fixture.sql; do",
    "GRANT EXECUTE ON PROCEDURE \\`$DB\\`.c05_02_claim_signed_once TO 'issuer_scope_worker'@'%';\"",
    "export CRECISOL_C05_02_SIGNED_MASTER_HEX=\"$SIGNING_MASTER_HEX\"",
    "dotnet run --no-build -c Release \\"
  ],
  "project": [
    "<TargetFramework>net10.0</TargetFramework>",
    "<TreatWarningsAsErrors>true</TreatWarningsAsErrors>",
    "<ProjectReference Include=\"../../src/Crecisol.Infrastructure/Crecisol.Infrastructure.csproj\" />"
  ],
  "doc": [],
  "workflow": [
    "- 'database/tests/c05_02_signed_recipient_mysql_smoke.sh'",
    "- '.github/workflows/c05-02-signed-recipient.yml'",
    "contents: read",
    "persist-credentials: false",
    "test ! -e .c05-02-signed-recipient",
    "python3 qa/c05_02_signed_recipient_audits.py A",
    "mkdir -p .c05-02-signed-recipient",
    "printf 'PASS A sha=%s run=%s\\n' \"$GITHUB_SHA\" \"$GITHUB_RUN_ID\" > .c05-02-signed-recipient/A",
    "python3 qa/c05_02_signed_recipient_audits.py B",
    "printf 'PASS B sha=%s run=%s\\n' \"$GITHUB_SHA\" \"$GITHUB_RUN_ID\" > .c05-02-signed-recipient/B",
    "dotnet-version: '10.0.x'",
    "printf 'PASS dotnet sha=%s run=%s\\n' \"$GITHUB_SHA\" \"$GITHUB_RUN_ID\" > .c05-02-signed-recipient/dotnet",
    "bash database/tests/c05_02_signed_recipient_mysql_smoke.sh",
    "printf 'PASS mysql sha=%s run=%s\\n' \"$GITHUB_SHA\" \"$GITHUB_RUN_ID\" > .c05-02-signed-recipient/mysql",
    "python3 qa/c05_02_signed_recipient_audits.py C",
    "printf 'PASS C sha=%s run=%s\\n' \"$GITHUB_SHA\" \"$GITHUB_RUN_ID\" > .c05-02-signed-recipient/C",
    "- name: Require A B C .NET10 MySQL8.4 same SHA and run",
    "for stage in A B C dotnet mysql; do",
    "grep -Fqx \"PASS $stage sha=$GITHUB_SHA run=$GITHUB_RUN_ID\" \".c05-02-signed-recipient/$stage\""
  ]
}

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
    require("FIRMA CRIPTOGRÁFICA REAL EN ENSAYO NO EQUIVALE A AUTENTICACIÓN HUMANA REAL" in data["doc"],
            "Must explicitly disclose lack of authenticated identity")

def audit_a():
    predecessor_a()
    invariants(source())
    print("PASS signed recipient A: authoritative C05 sources and exact immutable issuer scope")

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
    require(rejected >= 140,"Insufficient negative source guard mutations")
    print(f"PASS signed recipient B: {rejected} static negative mutations rejected")
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
    print(f"PASS signed recipient C: same SHA/run A+B+.NET10+MySQL8.4 sha={sha} run={run}")
    print("NO FUNCTIONAL C05-02 CERTIFICATION OR PRODUCTION SESSION/GRANT")

if __name__=="__main__":
    try:
        require(len(sys.argv)==2 and sys.argv[1] in ("A","B","C"),
                "Usage: c05_02_signed_recipient_audits.py A|B|C")
        {"A":audit_a,"B":audit_b,"C":audit_c}[sys.argv[1]]()
    except (AssertionError,OSError,ValueError,KeyError,
            subprocess.SubprocessError) as error:
        print("FAIL C05-02 signed recipient audit: "+str(error),file=sys.stderr)
        sys.exit(1)
