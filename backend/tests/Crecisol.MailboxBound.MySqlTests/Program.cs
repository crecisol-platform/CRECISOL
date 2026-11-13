using System.Buffers.Binary;
using System.Data;
using Crecisol.Application.Identity;
using System.Security.Cryptography;
using System.Text;
using Crecisol.Domain.Identity;
using Crecisol.Domain.Tenancy;
using Crecisol.Infrastructure.Identity;
using Crecisol.Infrastructure.Tenancy;
using MySqlConnector;

// Disposable MySQL 8.4: exact signed recipient + independent verified mailbox state.
// Synthetic mail proof VERIFIED is not a real external inbox or human identity.
if (args.Length!=1 || args[0]!="--integration")
    throw new InvalidOperationException("Use --integration for isolated signed proof tests.");

var setupDsn=Environment.GetEnvironmentVariable("CRECISOL_C05_02_MAILBOX_ADMIN_DSN")
    ?? throw new InvalidOperationException("Setup DSN required.");
var workerDsn=Environment.GetEnvironmentVariable("CRECISOL_C05_02_MAILBOX_WORKER_DSN")
    ?? throw new InvalidOperationException("Worker DSN required.");
var masterHex=Environment.GetEnvironmentVariable("CRECISOL_C05_02_MAILBOX_MASTER_HEX")
    ?? throw new InvalidOperationException("Test signing secret required.");
var master=Convert.FromHexString(masterHex);
if(master.Length!=32) throw new InvalidOperationException("Test signing key length required.");
var options=new MySqlConnectionStringBuilder(setupDsn);
if (options.Database!="c05_02_disposable" ||
    options.Server is not ("127.0.0.1" or "localhost" or "::1"))
    throw new InvalidOperationException("Only disposable loopback setup allowed.");

const string A="aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
const string B="bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";
const string IA="aaaaaaaaaaaaaaaaaaaaaaaaaaaa0001";
const string IB="bbbbbbbbbbbbbbbbbbbbbbbbbbbb0001";
const string OA="aaaaaaaaaaaaaaaaaaaaaaaaaaaa0004";
const string RA="aaaaaaaaaaaaaaaaaaaaaaaaaaaa0003";
const string RA2="aaaaaaaaaaaaaaaaaaaaaaaaaaaa0006";
const string RB="bbbbbbbbbbbbbbbbbbbbbbbbbbbb0003";
const string LA="aaaaaaaaaaaaaaaaaaaaaaaaaaaa0009";
const string LB="bbbbbbbbbbbbbbbbbbbbbbbbbbbb0009";
const string BRA="aaaaaaaaaaaaaaaaaaaaaaaaaaaa0010";
const string BRB="bbbbbbbbbbbbbbbbbbbbbbbbbbbb0010";
const string RESA="aaaaaaaaaaaaaaaaaaaaaaaaaaaa0011";
const string RESB="bbbbbbbbbbbbbbbbbbbbbbbbbbbb0011";

var tenantA=TenantId.From(Guid.Parse(A));
var tenantB=TenantId.From(Guid.Parse(B));
var actorA=TenantActorId.From(Guid.Parse(RA));
var actorA2=TenantActorId.From(Guid.Parse(RA2));
var actorB=TenantActorId.From(Guid.Parse(RB));
var sessionA=Guid.Parse("aaaaaaaaaaaaaaaaaaaaaaaaaaaa0040");
var sessionA2=Guid.Parse("aaaaaaaaaaaaaaaaaaaaaaaaaaaa0041");
var sessionB=Guid.Parse("bbbbbbbbbbbbbbbbbbbbbbbbbbbb0040");
var issued=DateTimeOffset.FromUnixTimeSeconds(DateTimeOffset.UtcNow.ToUnixTimeSeconds()-60);
var expires=issued.AddSeconds(300);
var testKey=HMACSHA256.HashData(master,
    "CRECISOL/C05-02/RECIPIENT-ASSERTION-KEY/V1"u8);
var purpose=Encoding.ASCII.GetBytes(
    "CRECISOL/C05-02/RECIPIENT-ASSERTION/TENANT-CLAIM/V1");

var checks=0;
void Pass(bool ok,string text)
{
    if(!ok) throw new InvalidOperationException("FAIL signed claim: "+text);
    checks++;
    Console.WriteLine("PASS C05-02 signed claim: "+text);
}
await using var admin=new MySqlConnection(setupDsn);
await admin.OpenAsync();
async Task Exec(string sql)
{
    await using var command=new MySqlCommand(sql,admin);
    await command.ExecuteNonQueryAsync();
}
async Task<int> Count(string sql)
{
    await using var command=new MySqlCommand(sql,admin);
    return Convert.ToInt32(await command.ExecuteScalarAsync());
}
await Exec($"""
  INSERT INTO c05_02_tenants VALUES
    (X'{A}','ACTIVE',42),(X'{B}','ACTIVE',42);
  INSERT INTO c05_02_issuers VALUES
    (X'{A}',X'{IA}','INVITE_EMPLOYEE',1),
    (X'{A}',X'{OA}','INVITE_EMPLOYEE',0),
    (X'{B}',X'{IB}','INVITE_CONSUMER',1);
  INSERT INTO c05_02_recipients VALUES
    (X'{A}',X'{RA}',X'{RA}',1,DATE_ADD(UTC_TIMESTAMP(6),INTERVAL 3 HOUR)),
    (X'{A}',X'{RA2}',X'{RA2}',1,DATE_ADD(UTC_TIMESTAMP(6),INTERVAL 3 HOUR)),
    (X'{B}',X'{RB}',X'{RB}',1,DATE_ADD(UTC_TIMESTAMP(6),INTERVAL 3 HOUR));
  INSERT INTO c05_02_consents VALUES
    (X'{A}',X'{RA}',42,UTC_TIMESTAMP(6)),
    (X'{A}',X'{RA2}',42,UTC_TIMESTAMP(6)),
    (X'{B}',X'{RB}',42,UTC_TIMESTAMP(6));
  INSERT INTO c05_02_auth_accounts VALUES
    (X'{A}',X'{IA}','ACTIVE',7),(X'{B}',X'{IB}','ACTIVE',7);
  INSERT INTO c05_02_auth_sessions VALUES
    (X'{A}',X'{IA}','ACTIVE',11,DATE_ADD(UTC_TIMESTAMP(6),INTERVAL 3 HOUR)),
    (X'{B}',X'{IB}','ACTIVE',11,DATE_ADD(UTC_TIMESTAMP(6),INTERVAL 3 HOUR));
  INSERT INTO c05_02_auth_links VALUES
    (X'{A}',X'{IA}',X'{LA}','ACTIVE',12,DATE_ADD(UTC_TIMESTAMP(6),INTERVAL 3 HOUR)),
    (X'{B}',X'{IB}',X'{LB}','ACTIVE',12,DATE_ADD(UTC_TIMESTAMP(6),INTERVAL 3 HOUR));
  INSERT INTO c05_02_auth_hosts VALUES
    (X'{A}','VERIFIED',13,14),(X'{B}','VERIFIED',13,14);
  INSERT INTO c05_02_auth_branches VALUES
    (X'{A}',X'{BRA}','ACTIVE',15),(X'{B}',X'{BRB}','ACTIVE',15);
  INSERT INTO c05_02_auth_resources VALUES
    (X'{A}',X'{RESA}',X'{BRA}',X'{IA}','ACTIVE',16),
    (X'{B}',X'{RESB}',X'{BRB}',X'{IB}','ACTIVE',16);
  INSERT INTO c05_02_auth_grants VALUES
    (X'{A}',X'{IA}',X'{BRA}','identity.invitation.issue','OWNER_ONLY',
     'ACTIVE',17,DATE_ADD(UTC_TIMESTAMP(6),INTERVAL 3 HOUR)),
    (X'{B}',X'{IB}',X'{BRB}','identity.invitation.issue','OWNER_ONLY',
     'ACTIVE',17,DATE_ADD(UTC_TIMESTAMP(6),INTERVAL 3 HOUR));
  INSERT INTO c05_02_auth_issue_policy VALUES
    (X'{A}','EMPLOYEE','identity.invitation.issue','ACTIVE',7200),
    (X'{B}','CONSUMER','identity.invitation.issue','ACTIVE',7200);
  """);

string Hex(Guid id)=>Convert.ToHexString(MySqlRfc4122Guid.Encode(id));
var sidA=Hex(sessionA);
var sidA2=Hex(sessionA2);
var sidB=Hex(sessionB);
await Exec($"""
  INSERT INTO c05_02_signed_recipient_sessions
    (tenant_id,session_id,recipient_id,actor_id,
     lifecycle,revision,key_epoch,issued_utc,expires_utc)
  VALUES
    (X'{A}',X'{sidA}',X'{RA}',X'{RA}','ACTIVE',3,1,
       '{issued.UtcDateTime:yyyy-MM-dd HH:mm:ss}',
       '{expires.UtcDateTime:yyyy-MM-dd HH:mm:ss}'),
    (X'{A}',X'{sidA2}',X'{RA2}',X'{RA2}','ACTIVE',3,1,
       '{issued.UtcDateTime:yyyy-MM-dd HH:mm:ss}',
       '{expires.UtcDateTime:yyyy-MM-dd HH:mm:ss}'),
    (X'{B}',X'{sidB}',X'{RB}',X'{RB}','ACTIVE',3,1,
       '{issued.UtcDateTime:yyyy-MM-dd HH:mm:ss}',
       '{expires.UtcDateTime:yyyy-MM-dd HH:mm:ss}');
  """);
using var mailboxVerifier = new MySqlDisposableEmailPossessionAdapter(
    workerDsn,master,1,new RejectingDeliverySink());
var intentByInvite = new Dictionary<Guid,Guid>();
byte[] SignTestOnly(TenantId tenant,TenantActorId actor,
    RecipientChannelBindingId channel,Guid session,
    long revision=3,int keyEpoch=1,long? issueTime=null,long? expiryTime=null,
    byte[]? overrideKey=null)
{
    var proof=new byte[125];
    proof[0]=1;
    BinaryPrimitives.WriteInt32BigEndian(proof.AsSpan(1,4),keyEpoch);
    MySqlRfc4122Guid.Encode(tenant.Value).CopyTo(proof,5);
    MySqlRfc4122Guid.Encode(actor.Value).CopyTo(proof,21);
    MySqlRfc4122Guid.Encode(channel.Value).CopyTo(proof,37);
    MySqlRfc4122Guid.Encode(session).CopyTo(proof,53);
    BinaryPrimitives.WriteInt64BigEndian(proof.AsSpan(69,8),revision);
    BinaryPrimitives.WriteInt64BigEndian(proof.AsSpan(77,8),
        issueTime??issued.ToUnixTimeSeconds());
    BinaryPrimitives.WriteInt64BigEndian(proof.AsSpan(85,8),
        expiryTime??expires.ToUnixTimeSeconds());
    var message=new byte[purpose.Length+93];
    purpose.CopyTo(message,0);
    proof.AsSpan(0,93).CopyTo(message.AsSpan(purpose.Length));
    var tag=HMACSHA256.HashData(overrideKey??testKey,message);
    tag.CopyTo(proof,93);
    CryptographicOperations.ZeroMemory(tag);
    CryptographicOperations.ZeroMemory(message);
    return proof;
}
async Task<(InvitationId Id,byte[] Code)> AddInvite(
    string tenant,string issuer,string channel,string profile,
    string link,string branch,string resource)
{
    var id=InvitationId.From(Guid.NewGuid());
    var code=RandomNumberGenerator.GetBytes(32);
    var digest=InvitationTokenDigest.FromToken(code).CopyForProtectedPersistenceAdapter();
    try
    {
        await Exec($"""
          INSERT INTO c05_02_invitations
            (tenant_id,invitation_id,issuer_id,recipient_id,profile_scope,
             token_digest,lifecycle,revision,terms_version,
             issued_at_utc,expires_at_utc,claimed_at_utc)
          VALUES(X'{tenant}',X'{Hex(id.Value)}',X'{issuer}',X'{channel}', '{profile}',
             X'{Convert.ToHexString(digest)}','PENDING',1,42,
             DATE_SUB(UTC_TIMESTAMP(6),INTERVAL 30 SECOND),
             DATE_ADD(UTC_TIMESTAMP(6),INTERVAL 20 MINUTE),NULL);
          INSERT INTO c05_02_invite_scope_bindings
           (tenant_id,invitation_id,issuer_id,issuer_link_id,branch_id,
            resource_id,action_key,scope_mode,account_revision,session_revision,
            link_revision,host_revision,catalog_revision,branch_revision,
            resource_revision,grant_revision)
          VALUES(X'{tenant}',X'{Hex(id.Value)}',X'{issuer}',X'{link}',X'{branch}',
            X'{resource}','identity.invitation.issue','OWNER_ONLY',7,11,
            12,13,14,15,16,17);
          """);
        return (id,code);
    }
    catch { CryptographicOperations.ZeroMemory(code); throw; }
    finally { CryptographicOperations.ZeroMemory(digest); }
}
async Task<bool> Claim(TenantId tenant,InvitationId invitation,
    byte[] rawCode,byte[] assertion,Guid? intentOverride=null)
{
    if(rawCode.Length!=32 || assertion.Length!=125 || assertion[0]!=1)
        return false;
    var now=DateTimeOffset.UtcNow.ToUnixTimeSeconds();
    var issuedSeconds=BinaryPrimitives.ReadInt64BigEndian(assertion.AsSpan(77,8));
    var expirySeconds=BinaryPrimitives.ReadInt64BigEndian(assertion.AsSpan(85,8));
    if(issuedSeconds>now || issuedSeconds<now-300 ||
       expirySeconds<=now || expirySeconds>now+300 ||
       expirySeconds-issuedSeconds is <30 or >300)
        return false;
    var session=MySqlRfc4122Guid.Decode(assertion.AsSpan(53,16).ToArray());
    var actor=MySqlRfc4122Guid.Decode(assertion.AsSpan(21,16).ToArray());
    var channel=MySqlRfc4122Guid.Decode(assertion.AsSpan(37,16).ToArray());
    var recipientDigest=InvitationTokenDigest.FromToken(rawCode)
        .CopyForProtectedPersistenceAdapter();
    try
    {
        await using var db=new MySqlConnection(workerDsn);
        await db.OpenAsync();
        await using var cmd=new MySqlCommand("c05_02_claim_mailbox_bound_once",db)
          { CommandType=CommandType.StoredProcedure };
        void Id(string name,Guid id)=>
            cmd.Parameters.Add(name,MySqlDbType.Binary).Value=MySqlRfc4122Guid.Encode(id);
        Id("p_tenant",tenant.Value);
        Id("p_invitation",invitation.Value);
        Id("p_actor",actor);
        Id("p_recipient",channel);
        cmd.Parameters.Add("p_digest",MySqlDbType.Binary).Value=recipientDigest;
        Id("p_signed_session",session);
        cmd.Parameters.Add("p_signed_revision",MySqlDbType.Int64).Value=
            BinaryPrimitives.ReadInt64BigEndian(assertion.AsSpan(69,8));
        cmd.Parameters.Add("p_signed_key_epoch",MySqlDbType.Int32).Value=
            BinaryPrimitives.ReadInt32BigEndian(assertion.AsSpan(1,4));
        cmd.Parameters.Add("p_signed_issued",MySqlDbType.DateTime).Value=
            DateTimeOffset.FromUnixTimeSeconds(issuedSeconds).UtcDateTime;
        cmd.Parameters.Add("p_signed_expires",MySqlDbType.DateTime).Value=
            DateTimeOffset.FromUnixTimeSeconds(expirySeconds).UtcDateTime;
        Id("p_intent",intentOverride??intentByInvite[invitation.Value]);
        cmd.Parameters.Add("p_signed_assertion",MySqlDbType.VarBinary).Value=assertion;
        var result=await cmd.ExecuteScalarAsync();
        return result is "CLAIMED";
    }
    finally { CryptographicOperations.ZeroMemory(recipientDigest); }
}

var a=await AddInvite(A,IA,RA,"EMPLOYEE",LA,BRA,RESA);
var a2=await AddInvite(A,IA,RA2,"EMPLOYEE",LA,BRA,RESA);
var b=await AddInvite(B,IB,RB,"CONSUMER",LB,BRB,RESB);
var proofA=SignTestOnly(tenantA,actorA,
    RecipientChannelBindingId.From(Guid.Parse(RA)),sessionA);
var proofA2=SignTestOnly(tenantA,actorA2,
    RecipientChannelBindingId.From(Guid.Parse(RA2)),sessionA2);
var proofB=SignTestOnly(tenantB,actorB,
    RecipientChannelBindingId.From(Guid.Parse(RB)),sessionB);
// The intent and recipient-channel bindings are added by a privileged TEST
// fixture, never by an unauthenticated route. The challenge digest is issued
// and verified through the real C05-02 MySQL email-possession stored routines.
var intentA=Guid.NewGuid();
var intentA2=Guid.NewGuid();
var intentB=Guid.NewGuid();
var mailboxA=RandomNumberGenerator.GetBytes(32);
var mailboxA2=RandomNumberGenerator.GetBytes(32);
var mailboxB=RandomNumberGenerator.GetBytes(32);
var proofSecrets=new List<byte[]>();
async Task SeedVerifiedMailbox(TenantId tenant,Guid intent,
    RecipientChannelBindingId recipient,byte[] mailboxDigest)
{
    var hex=Convert.ToHexString(mailboxDigest);
    var nonce=RandomNumberGenerator.GetBytes(12);
    var tag=RandomNumberGenerator.GetBytes(16);
    var ciphertext=RandomNumberGenerator.GetBytes(24);
    try
    {
        await Exec($"""
          INSERT INTO c05_02_neutral_intents
            (tenant_id,intent_id,peer_digest,mailbox_digest,nonce,tag,
             ciphertext,key_version,captured_at_utc,expires_at_utc)
          VALUES(X'{Hex(tenant.Value)}',X'{Hex(intent)}',
            X'{Convert.ToHexString(RandomNumberGenerator.GetBytes(32))}',
            X'{hex}',X'{Convert.ToHexString(nonce)}',
            X'{Convert.ToHexString(tag)}',X'{Convert.ToHexString(ciphertext)}',
            1,DATE_SUB(UTC_TIMESTAMP(6),INTERVAL 30 SECOND),
            DATE_ADD(UTC_TIMESTAMP(6),INTERVAL 30 MINUTE));
          INSERT INTO c05_02_mailbox_claim_channels
            (tenant_id,recipient_id,mailbox_digest,lifecycle)
          VALUES(X'{Hex(tenant.Value)}',X'{Hex(recipient.Value)}',
            X'{hex}','ACTIVE');
          """);
    }
    finally
    {
        CryptographicOperations.ZeroMemory(nonce);
        CryptographicOperations.ZeroMemory(tag);
        CryptographicOperations.ZeroMemory(ciphertext);
    }
    var code=RandomNumberGenerator.GetBytes(32);
    proofSecrets.Add(code);
    var digest=EmailPossessionDigest.FromToken(tenant,intent,code);
    Pass(await mailboxVerifier.TryIssueOnceAsync(tenant,intent,digest,
        DateTimeOffset.UtcNow,DateTimeOffset.UtcNow.AddMinutes(5),
        CancellationToken.None),"Independent mailbox proof issue guarded by MySQL");
    Pass(await mailboxVerifier.TryVerifyOnceAsync(tenant,intent,digest,
        DateTimeOffset.UtcNow,CancellationToken.None),
        "Independent mailbox proof confirmed in protected MySQL");
}
await SeedVerifiedMailbox(tenantA,intentA,
    RecipientChannelBindingId.From(Guid.Parse(RA)),mailboxA);
await SeedVerifiedMailbox(tenantA,intentA2,
    RecipientChannelBindingId.From(Guid.Parse(RA2)),mailboxA2);
await SeedVerifiedMailbox(tenantB,intentB,
    RecipientChannelBindingId.From(Guid.Parse(RB)),mailboxB);

async Task BindMailbox(InvitationId id,TenantId tenant,Guid intent,
    RecipientChannelBindingId recipient,Guid session,byte[] mailboxDigest,
    byte[] signedProof)
{
    await Exec($"""
       INSERT INTO c05_02_mailbox_claim_bindings
         (tenant_id,invitation_id,recipient_id,intent_id,session_id,
          mailbox_digest,assertion_digest,lifecycle,consumed_utc)
       VALUES(X'{Hex(tenant.Value)}',X'{Hex(id.Value)}',X'{Hex(recipient.Value)}',
         X'{Hex(intent)}',X'{Hex(session)}',X'{Convert.ToHexString(mailboxDigest)}',
         X'{Convert.ToHexString(SHA256.HashData(signedProof))}','READY',NULL);
       """);
    intentByInvite.Add(id.Value,intent);
}
await BindMailbox(a.Id,tenantA,intentA,
    RecipientChannelBindingId.From(Guid.Parse(RA)),sessionA,mailboxA,proofA);
await BindMailbox(a2.Id,tenantA,intentA2,
    RecipientChannelBindingId.From(Guid.Parse(RA2)),sessionA2,mailboxA2,proofA2);
await BindMailbox(b.Id,tenantB,intentB,
    RecipientChannelBindingId.From(Guid.Parse(RB)),sessionB,mailboxB,proofB);
Pass(await Count("SELECT COUNT(*) FROM c05_02_mailbox_claim_bindings")==3,
    "Each invitation has exactly one signed-session + mailbox-intent binding");

try
{
    Pass(!await Claim(tenantB,a.Id,a.Code,proofA),
        "Cryptographically valid tenant A proof cannot claim as tenant B");
    Pass(!await Claim(tenantA,a.Id,a.Code,proofB),
        "Signed tenant B recipient cannot claim tenant A invitation");
    Pass(!await Claim(tenantA,b.Id,b.Code,proofA),
        "Tenant B invitation ID cannot resolve under tenant A");
    Pass(!await Claim(tenantA,a.Id,RandomNumberGenerator.GetBytes(32),proofA),
        "Valid recipient signature cannot replace invitation capability");
    Pass(!await Claim(tenantA,a.Id,new byte[31],proofA),
        "Malformed invitation capability fails closed");
    Pass(!await Claim(tenantA,a.Id,a.Code,Array.Empty<byte>()),
        "No signed assertion means no claim");
    foreach(var bit in new[]{0,1,5,21,37,53,69,77,85,93,124})
    {
        var corrupted=(byte[])proofA.Clone();
        corrupted[bit]^=0x40;
        try { Pass(!await Claim(tenantA,a.Id,a.Code,corrupted),
            "Tampered signature or signed field denied at offset "+bit); }
        finally { CryptographicOperations.ZeroMemory(corrupted); }
    }
    var alienKey=RandomNumberGenerator.GetBytes(32);
    var foreign=SignTestOnly(tenantA,actorA,
        RecipientChannelBindingId.From(Guid.Parse(RA)),sessionA,
        overrideKey:alienKey);
    try { Pass(!await Claim(tenantA,a.Id,a.Code,foreign),
        "Proof signed with foreign key rejected"); }
    finally { CryptographicOperations.ZeroMemory(foreign);
        CryptographicOperations.ZeroMemory(alienKey); }
    var rotated=SignTestOnly(tenantA,actorA,
        RecipientChannelBindingId.From(Guid.Parse(RA)),sessionA,keyEpoch:2);
    try { Pass(!await Claim(tenantA,a.Id,a.Code,rotated),
        "Incorrect signing key epoch rejected"); }
    finally { CryptographicOperations.ZeroMemory(rotated); }
    var stale=SignTestOnly(tenantA,actorA,
        RecipientChannelBindingId.From(Guid.Parse(RA)),sessionA,revision:4);
    try { Pass(!await Claim(tenantA,a.Id,a.Code,stale),
        "Signed session revision must match current SQL revision"); }
    finally { CryptographicOperations.ZeroMemory(stale); }
    var expired=SignTestOnly(tenantA,actorA,
        RecipientChannelBindingId.From(Guid.Parse(RA)),sessionA,
        issueTime:issued.ToUnixTimeSeconds()-400,
        expiryTime:issued.ToUnixTimeSeconds()-100);
    try { Pass(!await Claim(tenantA,a.Id,a.Code,expired),
        "HMAC-valid expired session proof denied"); }
    finally { CryptographicOperations.ZeroMemory(expired); }
    var extreme=SignTestOnly(tenantA,actorA,
        RecipientChannelBindingId.From(Guid.Parse(RA)),sessionA,
        issueTime:long.MinValue,expiryTime:long.MaxValue);
    try { Pass(!await Claim(tenantA,a.Id,a.Code,extreme),
        "HMAC-valid extreme signed timestamps fail closed without overflow"); }
    finally { CryptographicOperations.ZeroMemory(extreme); }
    var overlong=SignTestOnly(tenantA,actorA,
        RecipientChannelBindingId.From(Guid.Parse(RA)),sessionA,
        issueTime:issued.ToUnixTimeSeconds(),
        expiryTime:expires.ToUnixTimeSeconds()+200);
    try { Pass(!await Claim(tenantA,a.Id,a.Code,overlong),
        "HMAC-valid proof over five-minute policy window denied"); }
    finally { CryptographicOperations.ZeroMemory(overlong); }
    var future=SignTestOnly(tenantA,actorA,
        RecipientChannelBindingId.From(Guid.Parse(RA)),sessionA,
        issueTime:expires.ToUnixTimeSeconds()+10,
        expiryTime:expires.ToUnixTimeSeconds()+100);
    try { Pass(!await Claim(tenantA,a.Id,a.Code,future),
        "Future-dated proof denied despite valid signature"); }
    finally { CryptographicOperations.ZeroMemory(future); }

    async Task RevokedCase(string label,string change,string restore)
    {
        await Exec(change);
        try {
            Pass(!await Claim(tenantA,a.Id,a.Code,proofA),
                "Atomic SQL refuses "+label+" after signature has been issued");
        }
        finally { await Exec(restore); }
        Pass(await Count($"""
          SELECT COUNT(*) FROM c05_02_invitations
          WHERE tenant_id=X'{A}' AND invitation_id=X'{Hex(a.Id.Value)}'
            AND lifecycle='PENDING' AND revision=1
          """)==1,"Denied "+label+" leaves invitation intact");
    }
    await RevokedCase("mailbox possession proof revoked",
        $"UPDATE c05_02_mailbox_proofs SET lifecycle='REVOKED',verified_utc=NULL WHERE tenant_id=X'{A}' AND intent_id=X'{Hex(intentA)}'",
        $"UPDATE c05_02_mailbox_proofs SET lifecycle='VERIFIED',verified_utc=UTC_TIMESTAMP(6) WHERE tenant_id=X'{A}' AND intent_id=X'{Hex(intentA)}'");
    await RevokedCase("mailbox channel suspended",
        $"UPDATE c05_02_mailbox_claim_channels SET lifecycle='SUSPENDED' WHERE tenant_id=X'{A}' AND recipient_id=X'{RA}'",
        $"UPDATE c05_02_mailbox_claim_channels SET lifecycle='ACTIVE' WHERE tenant_id=X'{A}' AND recipient_id=X'{RA}'");
    await RevokedCase("mailbox digest no longer matches recipient",
        $"UPDATE c05_02_mailbox_claim_channels SET mailbox_digest=X'{Convert.ToHexString(RandomNumberGenerator.GetBytes(32))}' WHERE tenant_id=X'{A}' AND recipient_id=X'{RA}'",
        $"UPDATE c05_02_mailbox_claim_channels SET mailbox_digest=X'{Convert.ToHexString(mailboxA)}' WHERE tenant_id=X'{A}' AND recipient_id=X'{RA}'");
    await RevokedCase("mailbox claim binding revoked",
        $"UPDATE c05_02_mailbox_claim_bindings SET lifecycle='REVOKED' WHERE tenant_id=X'{A}' AND invitation_id=X'{Hex(a.Id.Value)}'",
        $"UPDATE c05_02_mailbox_claim_bindings SET lifecycle='READY' WHERE tenant_id=X'{A}' AND invitation_id=X'{Hex(a.Id.Value)}'");
    Pass(!await Claim(tenantA,a.Id,a.Code,proofA,intentA2),
        "Verified mailbox intent for another recipient cannot claim invitation");
    Pass(!await Claim(tenantA,a.Id,a.Code,proofA,intentB),
        "Verified mailbox intent from tenant B cannot claim tenant A invitation");
    await RevokedCase("recipient signed-session revocation",
        $"UPDATE c05_02_signed_recipient_sessions SET lifecycle='REVOKED' WHERE tenant_id=X'{A}' AND session_id=X'{sidA}'",
        $"UPDATE c05_02_signed_recipient_sessions SET lifecycle='ACTIVE' WHERE tenant_id=X'{A}' AND session_id=X'{sidA}'");
    await RevokedCase("recipient signed-session revision change",
        $"UPDATE c05_02_signed_recipient_sessions SET revision=4 WHERE tenant_id=X'{A}' AND session_id=X'{sidA}'",
        $"UPDATE c05_02_signed_recipient_sessions SET revision=3 WHERE tenant_id=X'{A}' AND session_id=X'{sidA}'");
    await RevokedCase("recipient signed-session key rotation",
        $"UPDATE c05_02_signed_recipient_sessions SET key_epoch=2 WHERE tenant_id=X'{A}' AND session_id=X'{sidA}'",
        $"UPDATE c05_02_signed_recipient_sessions SET key_epoch=1 WHERE tenant_id=X'{A}' AND session_id=X'{sidA}'");
    await RevokedCase("recipient signed-session actor reassignment",
        $"UPDATE c05_02_signed_recipient_sessions SET actor_id=X'{IA}' WHERE tenant_id=X'{A}' AND session_id=X'{sidA}'",
        $"UPDATE c05_02_signed_recipient_sessions SET actor_id=X'{RA}' WHERE tenant_id=X'{A}' AND session_id=X'{sidA}'");
    await RevokedCase("recipient signed-session expiry",
        $"UPDATE c05_02_signed_recipient_sessions SET expires_utc=DATE_SUB(UTC_TIMESTAMP(6),INTERVAL 1 SECOND) WHERE tenant_id=X'{A}' AND session_id=X'{sidA}'",
        $"UPDATE c05_02_signed_recipient_sessions SET expires_utc='{expires.UtcDateTime:yyyy-MM-dd HH:mm:ss}' WHERE tenant_id=X'{A}' AND session_id=X'{sidA}'");
    await RevokedCase("issuer session revoked",
        $"UPDATE c05_02_auth_sessions SET lifecycle='REVOKED' WHERE tenant_id=X'{A}'",
        $"UPDATE c05_02_auth_sessions SET lifecycle='ACTIVE' WHERE tenant_id=X'{A}'");
    await RevokedCase("original issuer action revoked",
        $"UPDATE c05_02_auth_grants SET lifecycle='REVOKED' WHERE tenant_id=X'{A}'",
        $"UPDATE c05_02_auth_grants SET lifecycle='ACTIVE' WHERE tenant_id=X'{A}'");
    await RevokedCase("tenant suspension",
        $"UPDATE c05_02_tenants SET lifecycle='SUSPENDED' WHERE tenant_id=X'{A}'",
        $"UPDATE c05_02_tenants SET lifecycle='ACTIVE' WHERE tenant_id=X'{A}'");
    await RevokedCase("recipient channel revoked",
        $"UPDATE c05_02_recipients SET channel_verified=0 WHERE tenant_id=X'{A}' AND recipient_id=X'{RA}'",
        $"UPDATE c05_02_recipients SET channel_verified=1 WHERE tenant_id=X'{A}' AND recipient_id=X'{RA}'");
    await RevokedCase("recipient legal consent invalidated",
        $"UPDATE c05_02_consents SET accepted_at_utc=DATE_ADD(UTC_TIMESTAMP(6),INTERVAL 1 DAY) WHERE tenant_id=X'{A}' AND recipient_id=X'{RA}'",
        $"UPDATE c05_02_consents SET accepted_at_utc=UTC_TIMESTAMP(6) WHERE tenant_id=X'{A}' AND recipient_id=X'{RA}'");

    Pass(await Claim(tenantA,a.Id,a.Code,proofA),
        "Valid signed session and original issuer scope claim exactly once");
    Pass(!await Claim(tenantA,a.Id,a.Code,proofA),
        "Signed recipient session does not permit invitation replay");
    var races=await Task.WhenAll(Enumerable.Range(0,16)
        .Select(_=>Claim(tenantA,a2.Id,a2.Code,proofA2)));
    Pass(races.Count(v=>v)==1,"16 signed concurrent recipient claims yield one winner");
    Pass(races.Count(v=>!v)==15,"15 signed concurrent replays fail closed");
    Pass(await Claim(tenantB,b.Id,b.Code,proofB),
        "Tenant B independently claims only its own signed recipient invitation");
    Pass(await Count("""
      SELECT COUNT(*) FROM c05_02_invitations WHERE lifecycle='CLAIMED' AND revision=2
      """)==3,"Exactly 3 separate invites consumed across isolated tenants");
    Pass(await Count("SELECT COUNT(*) FROM c05_02_mailbox_claim_bindings WHERE lifecycle='USED' AND consumed_utc IS NOT NULL")==3,
        "One-time mailbox attestations consumed atomically with all three invitations");
    Pass(!await Claim(tenantA,a.Id,a.Code,proofA),
        "Even after email proof VERIFIED, consumed mailbox binding cannot be reused");


    await using var worker=new MySqlConnection(workerDsn);
    await worker.OpenAsync();
    async Task DenySql(string query,string label)
    {
        await using var cmd=new MySqlCommand(query,worker);
        try { await cmd.ExecuteNonQueryAsync(); }
        catch(MySqlException e) when(e.Number is 1142 or 1143)
        { Pass(true,label); return; }
        throw new InvalidOperationException("Unexpected direct SQL worker privilege.");
    }
    await DenySql("SELECT * FROM c05_02_mailbox_claim_bindings",
        "SQL worker cannot read or forge authenticated mailbox binding");
    await DenySql("UPDATE c05_02_mailbox_claim_channels SET lifecycle='ACTIVE'",
        "SQL worker cannot forge trusted recipient-to-mailbox association");
    await DenySql("SELECT * FROM c05_02_signed_recipient_sessions",
        "Worker cannot read recipient session metadata");
    await DenySql("UPDATE c05_02_signed_recipient_sessions SET lifecycle='ACTIVE'",
        "Worker cannot forge signed session state");
    await DenySql("UPDATE c05_02_invitations SET lifecycle='CLAIMED'",
        "Worker cannot consume invitation outside guarded procedure");
    Pass(await Count("""
       SELECT COUNT(*) FROM information_schema.tables
       WHERE table_schema='c05_02_disposable'
         AND table_name IN ('users','memberships','sessions','account_credentials')
       """)==0,"No real account, tenant membership or login session was created");
    Console.WriteLine($"PASS C05-02 mailbox-bound signed claim MySQL8.4: {checks} real assertions; " +
        "16 signed concurrent claims one winner; NO real inbox or login.");
}
finally
{
    foreach(var secret in proofSecrets)
        CryptographicOperations.ZeroMemory(secret);
    foreach(var secret in new[]{mailboxA,mailboxA2,mailboxB})
        CryptographicOperations.ZeroMemory(secret);
    foreach(var secret in new[]{a.Code,a2.Code,b.Code,proofA,proofA2,proofB,master,testKey})
        CryptographicOperations.ZeroMemory(secret);
}

internal sealed class RejectingDeliverySink : ITrustedLocalProofMailboxSink
{
    public Task DeliverInTestOnlyAsync(string storedCanonicalMailbox,
        ReadOnlyMemory<byte> capability,CancellationToken cancellationToken)=>
        throw new InvalidOperationException("No SMTP delivery in this isolated SQL conjunction test.");
}
