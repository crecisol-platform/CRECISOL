using System.Buffers.Binary;
using System.Text.Json;
using Crecisol.Api.Configuration;
using Crecisol.Api.Contracts;
using Crecisol.Application.Identity;
using Microsoft.Extensions.Logging;

int count=0;
void Check(bool ok,string label)
{
    if (!ok) throw new InvalidOperationException("C06-09 contract failed: "+label);
    count++;
}
var pixels=new byte[64*64*4];
new Random(20261125).NextBytes(pixels);
var png=C06ProtectedContent.EncodePhoto(pixels);
var base64=Convert.ToBase64String(png);
Check(C06ProtectedContent.NormalizePhoto(png).SequenceEqual(png),"canonical raster roundtrip");
foreach (var op in new[]{"profile/photo","platform/profile/photo"})
{
    Check(C06IdentityResponseProjection.TryApply(op,new(true,new { png=base64,mime="image/png",width=64,height=64 }),out var safe) &&
        safe.Allowed && JsonSerializer.Serialize(safe.Value).Contains(base64,StringComparison.Ordinal),"valid scoped raster "+op);
    var badCrc=png.ToArray(); badCrc[29]^=1;
    var badLength=png.ToArray(); BinaryPrimitives.WriteInt32BigEndian(badLength.AsSpan(8,4),int.MaxValue);
    var wrongDimensions=png.ToArray(); wrongDimensions[19]=65;
    foreach (var bad in new[]{ "AAAA","not base64!",base64+"\n",Convert.ToBase64String(png[..^1]),
        Convert.ToBase64String(png.Concat(new byte[]{1,2,3}).ToArray()),Convert.ToBase64String(badCrc),
        Convert.ToBase64String(badLength),Convert.ToBase64String(wrongDimensions),
        Convert.ToBase64String(new byte[24577]),Convert.ToBase64String("<svg onload='x'/>"u8.ToArray()) })
        Check(!C06IdentityResponseProjection.TryApply(op,new(true,new { png=bad,mime="image/png",width=64,height=64 }),out _),"malformed raster denied "+op);
    Check(!C06IdentityResponseProjection.TryApply(op,new(true,new { png=base64,mime="image/svg+xml",width=64,height=64 }),out _),"MIME spoofing denied "+op);
    Check(!C06IdentityResponseProjection.TryApply(op,new(true,new { png=base64,mime="image/png",width=65,height=64 }),out _),"dimension spoofing denied "+op);
}
foreach (var op in new[]{"private/read","platform/breakglass/read"})
{
    Check(C06IdentityResponseProjection.TryApply(op,new(true,new { reference="LOCAL-C06-001" }),out _),"display reference "+op);
    foreach(var bad in new[]{"","../private","https://example.test/x?sig=x","//example.test/a","data:text/html,x","file:///tmp/x","C:\\secret","a%2fb","a@b","a.b","x\r\ny",new string('a',121)})
        Check(!C06IdentityResponseProjection.TryApply(op,new(true,new { reference=bad }),out _),"URL/path reference denied "+op);
}
var now=DateTime.UtcNow;
foreach(var action in new[]{"login","profile/photo","PLATFORM_ASSISTED_REVIEW","fake-secret@example.test","https://example.test/token","platform/login"})
{
    Check(C06IdentityResponseProjection.TryApply("security/events",new(true,new { events=new[]{new { action,outcome=1,atUtc=now }} }),out var safe),"closed event label");
    var json=JsonSerializer.Serialize(safe.Value);
    if(action is "login" or "profile/photo" or "PLATFORM_ASSISTED_REVIEW")
        Check(json.Contains(action,StringComparison.Ordinal),"known action retained");
    else Check(json.Contains("security/event",StringComparison.Ordinal)&&!json.Contains(action,StringComparison.Ordinal),"unknown event neutralized");
}
Check(!C06IdentityResponseProjection.TryApply("security/events",new(true,new { events=new[]{new { action="login",outcome=255,atUtc=now }} }),out _),"invalid outcome");
using var writer=new StringWriter();
using var provider=new C06SafeRuntimeLoggerProvider(writer);
const string canary="C06-09-SYNTHETIC-PRIVATE-CANARY";
var logger=provider.CreateLogger(canary);
using(logger.BeginScope(canary))
    logger.Log(LogLevel.Error,new EventId(812,canary),canary,new InvalidOperationException(canary),
        (_,_)=>throw new InvalidOperationException("formatter must never run"));
provider.CreateLogger("Microsoft.AspNetCore.Hosting.Diagnostics").LogWarning(new EventId(17,canary),canary);
provider.CreateLogger(C06SafeRuntimeLoggerProvider.OutcomeCategory).LogInformation(new EventId(404,canary),canary);
var lines=writer.ToString().Split(Environment.NewLine,StringSplitOptions.RemoveEmptyEntries);
Check(lines.Length==3,"safe diagnostics remain operational");
Check(lines[0]=="CRECISOL_RUNTIME source=application level=4 event=812","arbitrary category/state/exception excluded");
Check(lines[1]=="CRECISOL_RUNTIME source=framework level=3 event=17","framework metadata only");
Check(lines[2]=="CRECISOL_RUNTIME source=http level=2 event=404","safe HTTP outcome");
Check(!writer.ToString().Contains(canary,StringComparison.Ordinal),"no raw diagnostic values");
Console.WriteLine("PASS C06-09 protected delivery contracts="+count);
