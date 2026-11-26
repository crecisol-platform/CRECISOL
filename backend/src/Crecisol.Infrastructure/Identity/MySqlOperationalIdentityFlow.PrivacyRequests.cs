using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Crecisol.Application.Identity;
using Crecisol.Domain.Authorization;

namespace Crecisol.Infrastructure.Identity;

public sealed partial class MySqlOperationalIdentityFlow
{
    private sealed record PrivacySelection(Guid RequestKey,string Right,string Category,string Reason,string? Challenge,string? Code)
    {
        public override string ToString()=>"PrivacySelection[redacted]";
    }
    private static PrivacySelection? ParsePrivacySelection(OperationalIdentityRequest r)
    {
        bool confirming=r.Operation=="privacy/requests/confirm";
        string[] names=["requestKey","right","category","reason"];
        if(r.Fields.Count!=names.Length+(confirming?2:0) ||
           names.Any(n=>!r.Fields.TryGetValue(n,out var v)||v is null||v.Length<1||v.Length>240) ||
           (confirming&&(!r.Fields.TryGetValue("challenge",out var challenge)||challenge is not {Length:64}||
               !challenge.All(Uri.IsHexDigit)||!r.Fields.TryGetValue("code",out var code)||code is not {Length:6}||
               !code.All(char.IsAsciiDigit))) ||
           !Guid.TryParseExact(r.Fields["requestKey"],"D",out var key))return null;
        var reason=r.Fields["reason"];
        if(!C06PrivacyRequestPolicy.MaySubmit(key,r.Fields["right"],r.Fields["category"],reason))return null;
        try { _=new UTF8Encoding(false,true).GetByteCount(reason); reason=reason.Trim().Normalize(NormalizationForm.FormC); }
        catch(ArgumentException){return null;}
        if(!C06PrivacyRequestPolicy.MaySubmit(key,r.Fields["right"],r.Fields["category"],reason))return null;
        return new(key,r.Fields["right"],r.Fields["category"],reason,
            confirming?r.Fields["challenge"].ToUpperInvariant():null,confirming?r.Fields["code"]:null);
    }
    private string PrivacyContext(Unit u,byte[] tenant,byte[] actor,Dictionary<string,object> account,
        Dictionary<string,object> member,Dictionary<string,object> session,Dictionary<string,object> factor) =>
        string.Join(":", "privacy-requests/v1",Hex(tenant),Hex(actor),Hex(Bytes(member,"membership_id")),
            Hex(Bytes(session,"session_id")),Hex(Bytes(factor,"factor_id")),
            Number(account,"security_revision").ToString(CultureInfo.InvariantCulture),
            Number(member,"approval_revision").ToString(CultureInfo.InvariantCulture),
            Number(session,"session_revision").ToString(CultureInfo.InvariantCulture),
            Number(factor,"factor_revision").ToString(CultureInfo.InvariantCulture),
            Number(u.VerifiedHost!,"host_revision").ToString(CultureInfo.InvariantCulture),
            Number(u.VerifiedHost!,"catalog_revision").ToString(CultureInfo.InvariantCulture));

    private byte[] PrivacyBinding(PrivacySelection s,string context,Dictionary<string,object> stepFactor)
    {
        using var stream=new MemoryStream();
        using var writer=new BinaryWriter(stream,Encoding.UTF8,leaveOpen:true);
        writer.Write("CRECISOL/C06/privacy/intent/v1");
        writer.Write(context);
        writer.Write(s.RequestKey.ToByteArray(true));writer.Write(s.Right);writer.Write(s.Category);
        writer.Write(Bytes(stepFactor,"factor_id"));writer.Write(Number(stepFactor,"factor_revision"));writer.Write(s.Reason);
        writer.Flush();
        var data=stream.ToArray();
        try{return HMACSHA256.HashData(tokenKey,data);}
        finally{CryptographicOperations.ZeroMemory(data);if(stream.TryGetBuffer(out var buffer))CryptographicOperations.ZeroMemory(buffer.AsSpan());}
    }

    private static C06PrivacyReceipt PrivacyReceipt(Dictionary<string,object> row)=>
        new(DbId(Bytes(row,"request_id")),Text(row,"right_code"),Text(row,"category_code"),Utc(row,"submitted_at_utc"));

    private async Task<OperationalIdentityResult> C06OwnPrivacyRequest(Unit u,byte[] tenant,byte[] actor,
        Dictionary<string,object> account,Dictionary<string,object> member,Dictionary<string,object> session,
        Dictionary<string,object> factor,int idle,OperationalIdentityRequest request,CancellationToken ct)
    {
        // Independent opt-in. Rights intake is deliberately not conditional on
        // the commercial delegation entitlement or acceptance of new terms.
        if(Environment.GetEnvironmentVariable("CRECISOL_C06_PRIVACY_REQUESTS_LAB_ENABLED")!="true" ||
           !request.Host.EndsWith(".example.test",StringComparison.Ordinal)||!u.VerifiedActor||u.VerifiedHost is null)
            return new(false);
        var clock=await u.One("SELECT UTC_TIMESTAMP(6) AS trusted_now_utc");
        if(clock is null)return new(false);
        var now=Utc(clock,"trusted_now_utc");
        _=ResolveC06TenantContextFromLockedRows(u.VerifiedHost,tenant,account,member,session,factor,idle,now);
        var context=PrivacyContext(u,tenant,actor,account,member,session,factor);
        if(request.Operation=="privacy/requests/list")
        {
            if(request.Fields.Count!=1||!request.Fields.TryGetValue("cursor",out var cursor)||cursor is null)
                return new(false);
            var first=cursor=="start";var after=DateTime.UnixEpoch;var last=Guid.Empty;
            if(!first&&!C06PendingCursor.TryDecode(tokenKey,context,cursor,now,out after,out last))
                return new(false);
            var rows=await u.All("""
                SELECT request_id,right_code,category_code,submitted_at_utc
                FROM c06_privacy_request WHERE tenant_id=@t AND account_id=@a
                  AND (@first=1 OR (submitted_at_utc,request_id)<(@after,@last))
                ORDER BY submitted_at_utc DESC,request_id DESC LIMIT 21 FOR SHARE
                """,("t",tenant),("a",actor),("first",first?1:0),("after",after),("last",last.ToByteArray(true)));
            var items=rows.Take(20).Select(PrivacyReceipt).ToArray();
            var next=rows.Count>20?C06PendingCursor.Encode(tokenKey,context,items[^1].SubmittedAtUtc.UtcDateTime,items[^1].RequestId,now):null;
            return new(true,new C06PrivacyRequestPage(items,next));
        }
        if(request.Operation is not ("privacy/requests/prepare" or "privacy/requests/confirm"))return new(false);
        var selection=ParsePrivacySelection(request);
        if(selection is null)return new(false);
        var key=selection.RequestKey.ToByteArray(true);
        var membership=Bytes(member,"membership_id");var sessionId=Bytes(session,"session_id");
        var stepFactor=await u.One("""
            SELECT * FROM c05_identity_mfa_factor WHERE account_id=@a AND factor_kind=1 AND factor_state=1
              AND revoked_at_utc IS NULL FOR UPDATE
            """,("a",actor));
        if(stepFactor is null)return new(false);
        var binding=PrivacyBinding(selection,context,stepFactor);
        try
        {
            var existing=await u.One("""
                SELECT session_id,membership_id,challenge_hmac,binding_hmac,expires_at_utc,consumed_at_utc
                FROM c06_privacy_request_intent WHERE tenant_id=@t AND account_id=@a AND request_key=@k FOR UPDATE
                """,("t",tenant),("a",actor),("k",key));
            if(request.Operation=="privacy/requests/prepare")
            {
                if(existing is not null)return new(false);
                var pending=await u.One("""
                    SELECT COUNT(*) AS pending FROM c06_privacy_request_intent
                    WHERE tenant_id=@t AND account_id=@a AND session_id=@s AND consumed_at_utc IS NULL
                      AND expires_at_utc>UTC_TIMESTAMP(6)
                    """,("t",tenant),("a",actor),("s",sessionId));
                if(pending is null||Number(pending,"pending")>=3)return new(false);
                var until=new[]{now.AddMinutes(5),Utc(member,"expires_at_utc"),Utc(session,"expires_at_utc")}.Min();
                if(until<=now)return new(false);
                var challenge=Token();var h=Digest("c06-privacy-challenge",challenge);
                try
                {
                    if(await u.Run("""
                        INSERT INTO c06_privacy_request_intent
                        (tenant_id,account_id,membership_id,session_id,request_key,challenge_hmac,binding_hmac,issued_at_utc,expires_at_utc)
                        VALUES(@t,@a,@m,@s,@k,@h,@b,@now,@until)
                        """,("t",tenant),("a",actor),("m",membership),("s",sessionId),("k",key),
                        ("h",h),("b",binding),("now",now.UtcDateTime),("until",until.UtcDateTime))!=1)
                        throw new InvalidOperationException("Privacy preparation not appended.");
                    return new(true,new C06PrivacyPreparation(challenge,until));
                }
                finally{CryptographicOperations.ZeroMemory(h);}
            }
            var hash=Digest("c06-privacy-challenge",selection.Challenge!);
            try
            {
                if(existing is null||!Bytes(existing,"session_id").SequenceEqual(sessionId)||
                   !Bytes(existing,"membership_id").SequenceEqual(membership)||
                   !CryptographicOperations.FixedTimeEquals(Bytes(existing,"challenge_hmac"),hash)||
                   !CryptographicOperations.FixedTimeEquals(Bytes(existing,"binding_hmac"),binding))return new(false);
                if(existing["consumed_at_utc"] is not DBNull)
                {
                    // Idempotent exact receipt only, same current subject and
                    // original consumed proof. No second MFA or request insert.
                    var prior=await u.One("""
                        SELECT request_id,right_code,category_code,submitted_at_utc
                        FROM c06_privacy_request WHERE tenant_id=@t AND account_id=@a AND request_key=@k FOR SHARE
                        """,("t",tenant),("a",actor),("k",key));
                    return prior is not null&&Text(prior,"right_code")==selection.Right&&Text(prior,"category_code")==selection.Category
                        ? new(true,PrivacyReceipt(prior)):new(false);
                }
                if(Utc(existing,"expires_at_utc")<=now||!await Reserve(tenant,"stepup:"+Hex(actor),8,ct)||
                   !await AcceptTotp(u,actor,stepFactor,selection.Code!,activate:false))return new(false);
                if(await u.Run("""
                    UPDATE c06_privacy_request_intent SET consumed_at_utc=@now
                    WHERE tenant_id=@t AND account_id=@a AND request_key=@k AND session_id=@s
                      AND challenge_hmac=@h AND consumed_at_utc IS NULL AND expires_at_utc>UTC_TIMESTAMP(6)
                    """,("now",now.UtcDateTime),("t",tenant),("a",actor),("k",key),("s",sessionId),("h",hash))!=1)
                    throw new InvalidOperationException("Privacy confirmation changed.");
                var id=Id();
                var plain=Encoding.UTF8.GetBytes(selection.Reason);byte[] encrypted;
                try{encrypted=Protect(plain,"c06-privacy-detail:"+Hex(tenant)+Hex(actor)+Hex(id));}
                finally{CryptographicOperations.ZeroMemory(plain);}
                try
                {
                    if(await u.Run("""
                        INSERT INTO c06_privacy_request
                        (tenant_id,account_id,request_id,request_key,membership_id,right_code,category_code,protected_detail,submitted_at_utc)
                        VALUES(@t,@a,@id,@k,@m,@right,@category,@detail,@now)
                        """,("t",tenant),("a",actor),("id",id),("k",key),("m",membership),
                        ("right",selection.Right),("category",selection.Category),("detail",encrypted),("now",now.UtcDateTime))!=1)
                        throw new InvalidOperationException("Privacy request not appended.");
                }
                finally{CryptographicOperations.ZeroMemory(encrypted);}
                // A request is not an export, deletion or legal decision.
                u.Subject=actor;
                return new(true,new C06PrivacyReceipt(DbId(id),selection.Right,selection.Category,now));
            }
            finally{CryptographicOperations.ZeroMemory(hash);}
        }
        finally{CryptographicOperations.ZeroMemory(binding);}
    }
}
