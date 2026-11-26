using System.Globalization;
using System.Net;
using System.Text.Json;
using Crecisol.CredentialSecurity;
using MySqlConnector;

namespace Crecisol.C06Delegation.FlowTests;

internal static class PrivacyRequestFixture
{
    internal static async Task<int> Run(DelegationWireFixture wire,MySqlConnection db,byte[] tenant,byte[] actor,
        byte[] factor,byte[] seed,string token,string secondSession,string otherToken)
    {
        int checks=0;
        void Check(bool ok,string label)
        {
            if(!ok)throw new InvalidOperationException("C06-11 PRIVACY FAIL "+label);
            checks++;Console.WriteLine("PASS C06-11 PRIVACY "+label);
        }
        async Task<object?> Scalar(string sql,params (string,object)[] ps)
        {
            await using var cmd=new MySqlCommand(sql,db);
            foreach(var (n,v) in ps)cmd.Parameters.AddWithValue("@"+n,v);
            return await cmd.ExecuteScalarAsync();
        }
        async Task<JsonElement> Allowed(string op,Dictionary<string,string> fields,string bearer,string[] shape,bool browser=false)
        {
            using var response=await wire.Send(op,fields,bearer,browser:browser);
            Check(response.StatusCode==HttpStatusCode.OK&&response.Headers.CacheControl is {NoStore:true,Private:true},
                "private HTTP success "+op);
            using var doc=JsonDocument.Parse(await response.Content.ReadAsByteArrayAsync());
            Check(doc.RootElement.EnumerateObject().Select(x=>x.Name).Order().SequenceEqual(shape.Order()),
                "closed shape "+op);
            return doc.RootElement.Clone();
        }
        async Task Denied(string op,Dictionary<string,string> fields,HttpStatusCode status,string? bearer=null,
            string host="delegation-a.example.test",string? raw=null,string? extraHeader=null,string? origin=null,bool browser=false,bool csrf=true)
        {
            using var response=await wire.Send(op,fields,bearer,host,raw,extraHeader,origin,browser,csrf);
            Check(response.StatusCode==status&&(await response.Content.ReadAsByteArrayAsync()).Length==0&&
                response.Headers.CacheControl is {NoStore:true,Private:true}&&!response.Headers.Contains("Set-Cookie"),
                "neutral "+op+" "+(int)status);
        }
        Dictionary<string,string> Copy(Dictionary<string,string> fields,string name,string value)=>
            new(fields,StringComparer.Ordinal){[name]=value};
        var list=new Dictionary<string,string>{{"cursor","start"}};
        string[] listShape=["items","nextCursor"], receiptShape=["requestId","right","category","submittedAtUtc","status","assessment"];
        var initial=await Allowed("privacy/requests/list",list,token,listShape,true);
        Check(initial.GetProperty("items").GetArrayLength()==0,"own empty inbox");
        var references=new HashSet<Guid>();string[] rights=["access","rectify","erase","object"];
        string[] categories=["identity","profile","case-records","security"];
        for(int i=0;i<rights.Length;i++)
        {
            // Four independent fresh TOTP codes; no counter resets or quota bypass.
            var accepted=Convert.ToInt64(await Scalar("SELECT last_accepted_step FROM c05_identity_mfa_factor WHERE account_id=@a AND factor_id=@f",
                ("a",actor),("f",factor)));
            while(TotpSha1Verifier.StepAt(DateTimeOffset.UtcNow)<=accepted)
                await Task.Delay(TimeSpan.FromMilliseconds(250));
            var body=new Dictionary<string,string>
            {
                ["requestKey"]=Guid.NewGuid().ToString("D"),["right"]=rights[i],["category"]=categories[i],
                ["reason"]="Synthetic own privacy request "+rights[i]
            };
            var prepared=await Allowed("privacy/requests/prepare",body,token,["challenge","expiresAtUtc"],true);
            var challenge=prepared.GetProperty("challenge").GetString()!;
            Check(challenge.Length==64,"one-use opaque privacy confirmation");
            var code=TotpSha1Verifier.ComputeCode(seed,TotpSha1Verifier.StepAt(DateTimeOffset.UtcNow));
            var confirmation=new Dictionary<string,string>(body){{"challenge",challenge},{"code",code}};
            if(i==0)
            {
                await Denied("privacy/requests/prepare",body,HttpStatusCode.Unauthorized,token);
                await Denied("privacy/requests/confirm",confirmation,HttpStatusCode.Unauthorized,secondSession);
                await Denied("privacy/requests/confirm",confirmation,HttpStatusCode.Unauthorized,otherToken);
                await Denied("privacy/requests/confirm",Copy(confirmation,"right","erase"),HttpStatusCode.Unauthorized,token);
                await Denied("privacy/requests/confirm",Copy(confirmation,"reason","Different synthetic purpose"),HttpStatusCode.Unauthorized,token);
                var wrong=Enumerable.Range(0,10).Select(x=>x.ToString("D6",CultureInfo.InvariantCulture)).First(x=>x!=code);
                await Denied("privacy/requests/confirm",Copy(confirmation,"code",wrong),HttpStatusCode.Unauthorized,token);
                Check(Convert.ToInt64(await Scalar("SELECT COUNT(*) FROM c06_privacy_request"))==0,
                    "denials create no privacy request");
            }
            var receipt=await Allowed("privacy/requests/confirm",confirmation,token,receiptShape,true);
            var id=receipt.GetProperty("requestId").GetGuid();
            Check(references.Add(id)&&receipt.GetProperty("right").GetString()==rights[i]&&
                receipt.GetProperty("category").GetString()==categories[i]&&receipt.GetProperty("status").GetString()=="received",
                "unique own receipt "+rights[i]);
            var assessment=receipt.GetProperty("assessment");
            Check(assessment.EnumerateObject().Count()==6&&assessment.GetProperty("identity").GetString()=="mfa-verified"&&
                new[]{"purpose","legalBasis","retention","legalHold","disposition"}.All(x=>assessment.GetProperty(x).GetString()=="pending"),
                "no fabricated legal or deletion decision "+rights[i]);
            var replay=await Allowed("privacy/requests/confirm",confirmation,token,receiptShape);
            Check(replay.GetProperty("requestId").GetGuid()==id&&
                Convert.ToInt64(await Scalar("SELECT COUNT(*) FROM c06_privacy_request"))==i+1,
                "exact idempotent retry creates no duplicate "+rights[i]);
            Check(Convert.ToInt64(await Scalar("SELECT COUNT(*) FROM c06_privacy_request WHERE tenant_id=@t AND account_id=@a AND LOCATE(@reason,protected_detail)>0",
                ("t",tenant),("a",actor),("reason",body["reason"])))==0,"request detail not persisted in clear");
        }
        var own=await Allowed("privacy/requests/list",list,token,listShape);
        Check(own.GetProperty("items").GetArrayLength()==4&&own.GetProperty("items").EnumerateArray()
            .Select(x=>x.GetProperty("requestId").GetGuid()).ToHashSet().SetEquals(references),"own four rights visible");
        var other=await Allowed("privacy/requests/list",list,otherToken,listShape,true);
        Check(other.GetProperty("items").GetArrayLength()==0,"other member cannot list subject requests");
        await Denied("privacy/requests/list",list,HttpStatusCode.Unauthorized,token,"delegation-b.example.test");
        await Denied("privacy/requests/list",Copy(list,"accountId",Guid.NewGuid().ToString("D")),HttpStatusCode.BadRequest,token);
        var shapeBody=new Dictionary<string,string>{{"requestKey",Guid.NewGuid().ToString("D")},{"right","access"},
            {"category","profile"},{"reason","Synthetic matrix purpose"}};
        var confirmShape=new Dictionary<string,string>(shapeBody){{"challenge",new string('A',64)},{"code","123456"}};
        foreach(var op in new[]{(Name:"privacy/requests/list",Fields:list),(Name:"privacy/requests/prepare",Fields:shapeBody),
                                 (Name:"privacy/requests/confirm",Fields:confirmShape)})
        {
            await Denied(op.Name,op.Fields,HttpStatusCode.Unauthorized);
            await Denied(op.Name,op.Fields,HttpStatusCode.Unauthorized,Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32)));
            await Denied(op.Name,op.Fields,HttpStatusCode.Unauthorized,token,"delegation-b.example.test");
            await Denied(op.Name,Copy(op.Fields,"tenantId",Guid.NewGuid().ToString("D")),HttpStatusCode.BadRequest,token);
            await Denied(op.Name,op.Fields,HttpStatusCode.BadRequest,token,extraHeader:"X-Tenant-Id");
            var first=op.Fields.First();
            var duplicate=JsonSerializer.Serialize(op.Fields).TrimEnd('}')+","+JsonSerializer.Serialize(first.Key)+":"+JsonSerializer.Serialize(first.Value)+"}";
            await Denied(op.Name,op.Fields,HttpStatusCode.BadRequest,token,raw:duplicate);
            await Denied(op.Name,op.Fields,HttpStatusCode.BadRequest,token,origin:"https://untrusted.example.test");
            await Denied(op.Name,op.Fields,HttpStatusCode.BadRequest,token,browser:true,csrf:false);
        }
        await Denied("privacy/requests/prepare",Copy(shapeBody,"right","purge"),HttpStatusCode.Unauthorized,token);
        await Denied("privacy/requests/prepare",Copy(shapeBody,"category","all-tenants"),HttpStatusCode.Unauthorized,token);
        await Denied("privacy/requests/prepare",Copy(shapeBody,"reason","short"),HttpStatusCode.Unauthorized,token);
        Check(Convert.ToInt64(await Scalar("SELECT COUNT(*) FROM c06_privacy_request"))==4&&
            Convert.ToInt64(await Scalar("SELECT COUNT(*) FROM c06_privacy_request WHERE identity_state='mfa-verified' AND purpose_state='pending' AND legal_basis_state='pending' AND retention_state='pending' AND legal_hold_state='pending' AND disposition_state='pending'"))==4,
            "all assessments remain pending with immutable receipts");
        Check(Convert.ToInt64(await Scalar("SELECT COUNT(*) FROM c05_identity_private_resource WHERE display_reference IN ('SYNTHETIC-A','SYNTHETIC-B','SYNTHETIC-C')"))==3,
            "erase request did not erase any underlying record");
        Console.WriteLine($"PASS C06-11 PRIVACY MYSQL checks={checks}; four own intake rights, no fulfilment or purge");
        return checks;
    }
}
