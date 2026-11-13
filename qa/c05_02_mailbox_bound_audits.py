#!/usr/bin/env python3
"""CRECISOL C05-02 mailbox+signed claim test-only A/B/C provenance gate."""
import json
import os
from pathlib import Path
import re
import subprocess
import sys
from c05_01_preparation_audits import ROOT
from c05_02_signed_recipient_audits import audit_a as predecessor_a

REPO = "crecisol-platform/CRECISOL"
FEATURE = "feature/c05-02-mailbox-proof-claim-gate-20261113"
BASE = "4616484e0a238f7accdeda803b5341d080d81c12"
EVIDENCE = ROOT / ".c05-02-mailbox-bound"
FILES = {
  "sql": "database/tests/fixtures/c05_02_mailbox_bound_claim_fixture.sql",
  "test": "backend/tests/Crecisol.MailboxBound.MySqlTests/Program.cs",
  "project": "backend/tests/Crecisol.MailboxBound.MySqlTests/Crecisol.MailboxBound.MySqlTests.csproj",
  "smoke": "database/tests/c05_02_mailbox_bound_mysql_smoke.sh",
  "doc": "docs/c05/PRUEBA_UNION_BUZON_IDENTIDAD_INVITACION_C05_02.md",
  "qa": "qa/c05_02_mailbox_bound_audits.py",
  "workflow": ".github/workflows/c05-02-mailbox-bound.yml"
}
ALLOWED = set(FILES.values())
MARKERS = {
  "sql": [
    "CREATE TABLE c05_02_mailbox_claim_channels (",
    "recipient_id BINARY(16) NOT NULL,",
    "PRIMARY KEY (tenant_id,recipient_id),",
    "FOREIGN KEY (tenant_id,recipient_id)",
    "REFERENCES c05_02_recipients(tenant_id,recipient_id)",
    "ON DELETE RESTRICT ON UPDATE RESTRICT",
    "CREATE TABLE c05_02_mailbox_claim_bindings (",
    "PRIMARY KEY (tenant_id,invitation_id),",
    "UNIQUE KEY uq_c05_02_one_intent (tenant_id,intent_id),",
    "FOREIGN KEY (tenant_id,invitation_id)",
    "REFERENCES c05_02_invitations(tenant_id,invitation_id)",
    "ON DELETE RESTRICT ON UPDATE RESTRICT,",
    "FOREIGN KEY (tenant_id,intent_id)",
    "REFERENCES c05_02_neutral_intents(tenant_id,intent_id)",
    "REFERENCES c05_02_mailbox_proofs(tenant_id,intent_id)",
    "FOREIGN KEY (tenant_id,session_id)",
    "REFERENCES c05_02_signed_recipient_sessions(tenant_id,session_id)",
    "CHECK ((lifecycle='USED' AND consumed_utc IS NOT NULL)",
    "OR (lifecycle<>'USED' AND consumed_utc IS NULL))",
    "CREATE PROCEDURE c05_02_claim_mailbox_bound_once(",
    "IN p_actor BINARY(16), IN p_recipient BINARY(16),",
    "IN p_signed_session BINARY(16), IN p_signed_revision BIGINT UNSIGNED,",
    "IN p_intent BINARY(16), IN p_signed_assertion VARBINARY(125)",
    "SQL SECURITY DEFINER",
    "UPDATE c05_02_invitations invitation",
    "JOIN c05_02_invite_scope_bindings binding_row",
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
    "JOIN c05_02_mailbox_claim_bindings mailbox_binding",
    "ON mailbox_binding.tenant_id=invitation.tenant_id",
    "AND mailbox_binding.invitation_id=invitation.invitation_id",
    "AND mailbox_binding.recipient_id=invitation.recipient_id",
    "AND mailbox_binding.session_id=p_signed_session",
    "AND mailbox_binding.intent_id=p_intent",
    "JOIN c05_02_mailbox_claim_channels mailbox_channel",
    "ON mailbox_channel.tenant_id=mailbox_binding.tenant_id",
    "AND mailbox_channel.recipient_id=mailbox_binding.recipient_id",
    "JOIN c05_02_neutral_intents intent",
    "ON intent.tenant_id=mailbox_binding.tenant_id",
    "AND intent.intent_id=mailbox_binding.intent_id",
    "JOIN c05_02_mailbox_proofs inbox_proof",
    "ON inbox_proof.tenant_id=intent.tenant_id",
    "AND inbox_proof.intent_id=intent.intent_id",
    "JOIN c05_02_signed_recipient_sessions signed_session",
    "ON signed_session.tenant_id=recipient.tenant_id",
    "AND signed_session.recipient_id=recipient.recipient_id",
    "AND signed_session.actor_id=recipient.actor_id",
    "AND signed_session.session_id=p_signed_session",
    "JOIN c05_02_consents consent ON consent.tenant_id=recipient.tenant_id",
    "AND consent.recipient_id=recipient.recipient_id",
    "AND consent.terms_version=invitation.terms_version",
    "SET invitation.lifecycle='CLAIMED',",
    "mailbox_binding.lifecycle='USED',",
    "mailbox_binding.consumed_utc=UTC_TIMESTAMP(6)",
    "AND invitation.invitation_id=p_invitation",
    "AND invitation.recipient_id=p_recipient",
    "AND invitation.token_digest=p_digest",
    "AND invitation.revision=1",
    "AND invitation.lifecycle='PENDING'",
    "AND invitation.issued_at_utc<=UTC_TIMESTAMP(6)",
    "AND UTC_TIMESTAMP(6)<invitation.expires_at_utc",
    "AND invitation.issuer_id<>p_actor",
    "AND recipient.actor_id=p_actor",
    "AND mailbox_binding.lifecycle='READY'",
    "AND mailbox_binding.assertion_digest=UNHEX(SHA2(p_signed_assertion,256))",
    "AND LENGTH(p_signed_assertion)=125",
    "AND mailbox_channel.lifecycle='ACTIVE'",
    "AND mailbox_channel.mailbox_digest=mailbox_binding.mailbox_digest",
    "AND intent.mailbox_digest=mailbox_binding.mailbox_digest",
    "AND intent.expires_at_utc>UTC_TIMESTAMP(6)",
    "AND inbox_proof.lifecycle='VERIFIED'",
    "AND inbox_proof.verified_utc IS NOT NULL",
    "AND inbox_proof.verified_utc<=UTC_TIMESTAMP(6)",
    "AND inbox_proof.expires_utc>UTC_TIMESTAMP(6)",
    "AND signed_session.lifecycle='ACTIVE'"
  ],
  "test": [
    "using MySqlConnector;",
    "throw new InvalidOperationException(\"Use --integration for isolated signed proof tests.\");",
    "var setupDsn=Environment.GetEnvironmentVariable(\"CRECISOL_C05_02_MAILBOX_ADMIN_DSN\")",
    "var workerDsn=Environment.GetEnvironmentVariable(\"CRECISOL_C05_02_MAILBOX_WORKER_DSN\")",
    "var masterHex=Environment.GetEnvironmentVariable(\"CRECISOL_C05_02_MAILBOX_MASTER_HEX\")",
    "var options=new MySqlConnectionStringBuilder(setupDsn);",
    "var testKey=HMACSHA256.HashData(master,",
    "\"CRECISOL/C05-02/RECIPIENT-ASSERTION-KEY/V1\"u8);",
    "\"CRECISOL/C05-02/RECIPIENT-ASSERTION/TENANT-CLAIM/V1\");",
    "void Pass(bool ok,string text)",
    "if(!ok) throw new InvalidOperationException(\"FAIL signed claim: \"+text);",
    "Console.WriteLine(\"PASS C05-02 signed claim: \"+text);",
    "await using var admin=new MySqlConnection(setupDsn);",
    "await using var command=new MySqlCommand(sql,admin);",
    "string Hex(Guid id)=>Convert.ToHexString(MySqlRfc4122Guid.Encode(id));",
    "INSERT INTO c05_02_signed_recipient_sessions",
    "using var mailboxVerifier = new MySqlDisposableEmailPossessionAdapter(",
    "var intentByInvite = new Dictionary<Guid,Guid>();",
    "MySqlRfc4122Guid.Encode(tenant.Value).CopyTo(proof,5);",
    "MySqlRfc4122Guid.Encode(actor.Value).CopyTo(proof,21);",
    "MySqlRfc4122Guid.Encode(channel.Value).CopyTo(proof,37);",
    "MySqlRfc4122Guid.Encode(session).CopyTo(proof,53);",
    "var tag=HMACSHA256.HashData(overrideKey??testKey,message);",
    "CryptographicOperations.ZeroMemory(tag);",
    "CryptographicOperations.ZeroMemory(message);",
    "var code=RandomNumberGenerator.GetBytes(32);",
    "var digest=InvitationTokenDigest.FromToken(code).CopyForProtectedPersistenceAdapter();",
    "token_digest,lifecycle,revision,terms_version,",
    "catch { CryptographicOperations.ZeroMemory(code); throw; }",
    "finally { CryptographicOperations.ZeroMemory(digest); }",
    "async Task<bool> Claim(TenantId tenant,InvitationId invitation,",
    "byte[] rawCode,byte[] assertion,Guid? intentOverride=null)",
    "if(rawCode.Length!=32 || assertion.Length!=125 || assertion[0]!=1)",
    "var issuedSeconds=BinaryPrimitives.ReadInt64BigEndian(assertion.AsSpan(77,8));",
    "var expirySeconds=BinaryPrimitives.ReadInt64BigEndian(assertion.AsSpan(85,8));",
    "var session=MySqlRfc4122Guid.Decode(assertion.AsSpan(53,16).ToArray());",
    "var actor=MySqlRfc4122Guid.Decode(assertion.AsSpan(21,16).ToArray());",
    "var channel=MySqlRfc4122Guid.Decode(assertion.AsSpan(37,16).ToArray());",
    "var recipientDigest=InvitationTokenDigest.FromToken(rawCode)",
    "await using var db=new MySqlConnection(workerDsn);",
    "await using var cmd=new MySqlCommand(\"c05_02_claim_mailbox_bound_once\",db)",
    "cmd.Parameters.Add(name,MySqlDbType.Binary).Value=MySqlRfc4122Guid.Encode(id);",
    "cmd.Parameters.Add(\"p_digest\",MySqlDbType.Binary).Value=recipientDigest;",
    "Id(\"p_signed_session\",session);",
    "cmd.Parameters.Add(\"p_signed_revision\",MySqlDbType.Int64).Value=",
    "BinaryPrimitives.ReadInt64BigEndian(assertion.AsSpan(69,8));",
    "cmd.Parameters.Add(\"p_signed_key_epoch\",MySqlDbType.Int32).Value=",
    "BinaryPrimitives.ReadInt32BigEndian(assertion.AsSpan(1,4));",
    "cmd.Parameters.Add(\"p_signed_issued\",MySqlDbType.DateTime).Value=",
    "cmd.Parameters.Add(\"p_signed_expires\",MySqlDbType.DateTime).Value=",
    "Id(\"p_intent\",intentOverride??intentByInvite[invitation.Value]);",
    "cmd.Parameters.Add(\"p_signed_assertion\",MySqlDbType.VarBinary).Value=assertion;",
    "finally { CryptographicOperations.ZeroMemory(recipientDigest); }",
    "var intentA=Guid.NewGuid();",
    "var intentA2=Guid.NewGuid();",
    "var intentB=Guid.NewGuid();",
    "var mailboxA=RandomNumberGenerator.GetBytes(32);",
    "var mailboxA2=RandomNumberGenerator.GetBytes(32);",
    "var mailboxB=RandomNumberGenerator.GetBytes(32);",
    "async Task SeedVerifiedMailbox(TenantId tenant,Guid intent,",
    "RecipientChannelBindingId recipient,byte[] mailboxDigest)",
    "var hex=Convert.ToHexString(mailboxDigest);",
    "var nonce=RandomNumberGenerator.GetBytes(12);",
    "var tag=RandomNumberGenerator.GetBytes(16);",
    "var ciphertext=RandomNumberGenerator.GetBytes(24);",
    "INSERT INTO c05_02_neutral_intents",
    "(tenant_id,intent_id,peer_digest,mailbox_digest,nonce,tag,",
    "VALUES(X'{Hex(tenant.Value)}',X'{Hex(intent)}',",
    "X'{Convert.ToHexString(RandomNumberGenerator.GetBytes(32))}',",
    "INSERT INTO c05_02_mailbox_claim_channels",
    "(tenant_id,recipient_id,mailbox_digest,lifecycle)",
    "CryptographicOperations.ZeroMemory(nonce);",
    "CryptographicOperations.ZeroMemory(ciphertext);",
    "var digest=EmailPossessionDigest.FromToken(tenant,intent,code);",
    "Pass(await mailboxVerifier.TryIssueOnceAsync(tenant,intent,digest,",
    "CancellationToken.None),\"Independent mailbox proof issue guarded by MySQL\");",
    "Pass(await mailboxVerifier.TryVerifyOnceAsync(tenant,intent,digest,",
    "DateTimeOffset.UtcNow,CancellationToken.None),",
    "\"Independent mailbox proof confirmed in protected MySQL\");",
    "await SeedVerifiedMailbox(tenantA,intentA,",
    "RecipientChannelBindingId.From(Guid.Parse(RA)),mailboxA);",
    "await SeedVerifiedMailbox(tenantA,intentA2,",
    "RecipientChannelBindingId.From(Guid.Parse(RA2)),mailboxA2);",
    "await SeedVerifiedMailbox(tenantB,intentB,",
    "RecipientChannelBindingId.From(Guid.Parse(RB)),mailboxB);",
    "async Task BindMailbox(InvitationId id,TenantId tenant,Guid intent,",
    "RecipientChannelBindingId recipient,Guid session,byte[] mailboxDigest,",
    "byte[] signedProof)",
    "INSERT INTO c05_02_mailbox_claim_bindings",
    "(tenant_id,invitation_id,recipient_id,intent_id,session_id,",
    "mailbox_digest,assertion_digest,lifecycle,consumed_utc)",
    "X'{Hex(intent)}',X'{Hex(session)}',X'{Convert.ToHexString(mailboxDigest)}',",
    "X'{Convert.ToHexString(SHA256.HashData(signedProof))}','READY',NULL);",
    "intentByInvite.Add(id.Value,intent);",
    "await BindMailbox(a.Id,tenantA,intentA,",
    "RecipientChannelBindingId.From(Guid.Parse(RA)),sessionA,mailboxA,proofA);",
    "await BindMailbox(a2.Id,tenantA,intentA2,",
    "RecipientChannelBindingId.From(Guid.Parse(RA2)),sessionA2,mailboxA2,proofA2);",
    "await BindMailbox(b.Id,tenantB,intentB,",
    "RecipientChannelBindingId.From(Guid.Parse(RB)),sessionB,mailboxB,proofB);"
  ],
  "smoke": [
    "IMAGE=\"mysql:8.4@sha256:a81d043930e51745522bd5c430961cf8286f0a23f0a9a2a6e0108f7ea71b0902\"",
    "NAME=\"crecisol-c0502-mailbox-$(openssl rand -hex 8)\"",
    "ROOT_PASSWORD=\"$(openssl rand -hex 24)\"",
    "ADMIN_PASSWORD=\"$(openssl rand -hex 24)\"",
    "WORKER_PASSWORD=\"$(openssl rand -hex 24)\"",
    "SIGNING_MASTER_HEX=\"$(openssl rand -hex 32)\"",
    "cleanup() { docker rm -f \"$NAME\" >/dev/null 2>&1 || true; }",
    "docker run --rm -d --name \"$NAME\" --network bridge -p '127.0.0.1::3306' \\",
    "database/tests/fixtures/c05_02_invitation_atomic_fixture.sql \\",
    "database/tests/fixtures/c05_02_issuer_scope_atomic_fixture.sql \\",
    "database/tests/fixtures/c05_02_issuer_bound_claim_fixture.sql \\",
    "database/tests/fixtures/c05_02_signed_recipient_claim_fixture.sql \\",
    "database/tests/fixtures/c05_02_neutral_registration_fixture.sql \\",
    "database/tests/fixtures/c05_02_mailbox_proof_atomic_fixture.sql \\",
    "database/tests/fixtures/c05_02_mailbox_bound_claim_fixture.sql; do",
    "GRANT EXECUTE ON PROCEDURE \\`$DB\\`.c05_02_mailbox_proof_issue TO 'issuer_scope_worker'@'%';",
    "GRANT EXECUTE ON PROCEDURE \\`$DB\\`.c05_02_mailbox_proof_verify TO 'issuer_scope_worker'@'%';",
    "GRANT EXECUTE ON PROCEDURE \\`$DB\\`.c05_02_claim_mailbox_bound_once TO 'issuer_scope_worker'@'%';\"",
    "export CRECISOL_C05_02_MAILBOX_MASTER_HEX=\"$SIGNING_MASTER_HEX\"",
    "dotnet run --no-build -c Release \\"
  ],
  "workflow": [
    "- 'database/tests/c05_02_mailbox_bound_mysql_smoke.sh'",
    "- 'qa/c05_02_mailbox_bound_audits.py'",
    "contents: read",
    "persist-credentials: false",
    "test ! -e .c05-02-mailbox-bound",
    "python3 -m py_compile qa/c05_02_mailbox_bound_audits.py",
    "python3 qa/c05_02_mailbox_bound_audits.py A",
    "mkdir -p .c05-02-mailbox-bound",
    "printf 'PASS A sha=%s run=%s\\n' \"$GITHUB_SHA\" \"$GITHUB_RUN_ID\" > .c05-02-mailbox-bound/A",
    "python3 qa/c05_02_mailbox_bound_audits.py B",
    "printf 'PASS B sha=%s run=%s\\n' \"$GITHUB_SHA\" \"$GITHUB_RUN_ID\" > .c05-02-mailbox-bound/B",
    "dotnet-version: '10.0.x'",
    "printf 'PASS dotnet sha=%s run=%s\\n' \"$GITHUB_SHA\" \"$GITHUB_RUN_ID\" > .c05-02-mailbox-bound/dotnet",
    "bash database/tests/c05_02_mailbox_bound_mysql_smoke.sh",
    "printf 'PASS mysql sha=%s run=%s\\n' \"$GITHUB_SHA\" \"$GITHUB_RUN_ID\" > .c05-02-mailbox-bound/mysql",
    "python3 qa/c05_02_mailbox_bound_audits.py C",
    "printf 'PASS C sha=%s run=%s\\n' \"$GITHUB_SHA\" \"$GITHUB_RUN_ID\" > .c05-02-mailbox-bound/C",
    "- name: Require A B C .NET10 MySQL8.4 same SHA and run",
    "grep -Fqx \"PASS $stage sha=$GITHUB_SHA run=$GITHUB_RUN_ID\" \".c05-02-mailbox-bound/$stage\""
  ],
  "project": [
    "<TargetFramework>net10.0</TargetFramework>",
    "<TreatWarningsAsErrors>true</TreatWarningsAsErrors>",
    "<ProjectReference Include=\"../../src/Crecisol.Infrastructure/Crecisol.Infrastructure.csproj\" />"
  ],
  "doc": [
    "ENSAYO TÉCNICO",
    "NO CERTIFICACIÓN FUNCIONAL",
    "C05-03 BLOQUEADA",
    "c05_02_mailbox_claim_channels",
    "c05_02_mailbox_claim_bindings",
    "c05_02_claim_mailbox_bound_once",
    "SHA256",
    "unique tenant+intent",
    "SQL worker",
    "ÚNICO UPDATE",
    "NO verifica por sí mismo",
    "Bloqueos P0/P1"
  ]
}

def require(ok, reason):
    if not ok:
        raise AssertionError(reason)

def source():
    return {k: (ROOT / p).read_text(encoding="utf-8")
            for k,p in FILES.items() if k != "qa"}

def invariants(data):
    for k, markers in MARKERS.items():
        for marker in markers:
            require(marker in data[k], "Missing security guard: " + k + ":" + marker)
    sql=data["sql"]
    require("ROW_COUNT()=2" in sql and
            "mailbox_binding.lifecycle='USED'" in sql and
            "invitation.lifecycle='CLAIMED'" in sql,
            "Both invitation and independent mailbox binding must be consumed")
    require("UNHEX(SHA2(p_signed_assertion,256))" in sql and
            "inbox_proof.lifecycle='VERIFIED'" in sql and
            "inbox_proof.expires_utc>UTC_TIMESTAMP(6)" in sql,
            "Signed bearer plus independent mailbox proof are mandatory")
    require("UNIQUE KEY uq_c05_02_one_intent (tenant_id,intent_id)" in sql,
            "One independent inbox proof can back only one invitation")
    require("GRANT ALL" not in data["smoke"] and
            "GRANT SELECT ON" not in data["smoke"] and
            "c05_02_claim_signed_once TO" not in data["smoke"],
            "No worker bypass or direct table grants")
    require("C05-02" in data["doc"] and
            "NO CERTIFICACIÓN FUNCIONAL" in data["doc"] and
            "C05-03 BLOQUEADA" in data["doc"] and
            "No es un directorio real" in data["doc"],
            "Do not falsify human identity or inbox proof")
    require("id-token: write" not in data["workflow"] and
            "workflow_dispatch:" not in data["workflow"] and
            "deploy" not in data["workflow"].lower(),
            "No privileged deployment or production workflow")
    api=(ROOT/"backend/src/Crecisol.Api/Program.cs").read_text(encoding="utf-8")
    paths=re.findall(r'app\.Map(?:Get|Post|Put|Patch|Delete)\(\s*"([^"]+)"',api)
    require(paths==["/api/v1/public/branding/platform"],
            "A private authentication or registration route was unexpectedly activated")

def audit_a():
    predecessor_a()
    invariants(source())
    print("PASS mailbox-bound A: master and C05 trust boundaries; no new private API")

def audit_b():
    original=source()
    invariants(original)
    rejected=0
    for k, markers in MARKERS.items():
        for marker in markers:
            mutant=dict(original)
            mutant[k]=original[k].replace(marker,"[REMOVED_C05_SECURITY_GUARD]")
            require(mutant[k]!=original[k],"Ineffective mutation")
            try:
                invariants(mutant)
            except AssertionError:
                rejected+=1
            else:
                raise AssertionError("Dangerous change accepted: "+k+":"+marker)
    require(rejected>=150,"Static mutation threshold insufficient")
    print(f"PASS mailbox-bound B: {rejected} text mutations rejected")
    print("STATIC TEXT MUTATIONS DO NOT SUBSTITUTE REAL MySQL SECURITY TESTS")

def git(*args):
    return subprocess.check_output(["git",*args],cwd=ROOT,
        text=True,stderr=subprocess.DEVNULL).strip()

def audit_c():
    require(os.getenv("GITHUB_ACTIONS")=="true" and
            os.getenv("GITHUB_REPOSITORY")==REPO,
            "Only CRECISOL GitHub Actions can produce this C audit")
    sha=os.getenv("GITHUB_SHA","")
    run=os.getenv("GITHUB_RUN_ID","")
    require(re.fullmatch("[a-f0-9]{40}",sha) is not None and
            re.fullmatch("[0-9]+",run) is not None and
            git("rev-parse","HEAD")==sha,
            "Exact SHA/run evidence required")
    for stage in ("A","B","dotnet","mysql"):
        p=EVIDENCE/stage
        require(p.is_file() and p.read_text(encoding="utf-8").strip()==
                f"PASS {stage} sha={sha} run={run}",
                "Missing same-run evidence: "+stage)
    event=os.getenv("GITHUB_EVENT_NAME")
    if event=="pull_request":
        ep=Path(os.getenv("GITHUB_EVENT_PATH",""))
        require(ep.is_file(),"PR payload missing")
        pr=json.loads(ep.read_text(encoding="utf-8"))["pull_request"]
        base,head=pr["base"],pr["head"]
        require(base["ref"]=="dev" and head["ref"]==FEATURE and
                base["repo"]["full_name"]==REPO and
                head["repo"]["full_name"]==REPO,
                "Wrong repo, branch or PR base")
        require(git("rev-list","--parents","-n","1","HEAD").split()==
                [sha,base["sha"],head["sha"]],
                "Synthetic PR SHA parents do not match")
        changed=git("diff","--name-only",base["sha"],head["sha"]).splitlines()
    else:
        require(event=="push" and os.getenv("GITHUB_REF")=="refs/heads/dev",
                "POSTMERGE only dev")
        changed=git("show","--format=","--name-only",sha).splitlines()
    require(bool(changed) and set(changed).issubset(ALLOWED) and
            FILES["sql"] in changed and FILES["test"] in changed,
            "Unauthorized change: "+repr(changed))
    invariants(source())
    print(f"PASS mailbox-bound C: exact A/B/.NET10/MySQL8.4 sha={sha} run={run}")
    print("NOT A HUMAN INBOX OR LOGIN: C05-02 FUNCTIONAL CERTIFICATION BLOCKED")

if __name__=="__main__":
    try:
        require(len(sys.argv)==2 and sys.argv[1] in ("A","B","C"),
                "Expected A|B|C")
        {"A":audit_a,"B":audit_b,"C":audit_c}[sys.argv[1]]()
    except (AssertionError,OSError,ValueError,KeyError,subprocess.SubprocessError) as e:
        print("FAIL C05-02 mailbox bound audit: "+str(e),file=sys.stderr)
        sys.exit(1)
