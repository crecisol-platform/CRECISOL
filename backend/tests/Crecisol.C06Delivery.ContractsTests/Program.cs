using System.Buffers.Binary;
using System.IO.Compression;
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
// Construct independent containers with valid CRCs to exercise the inner
// decompression budget, not just reject them at the outer checksum.
static byte[] TestChunk(string name, byte[] data)
{
    var chunk=new byte[12+data.Length];
    BinaryPrimitives.WriteInt32BigEndian(chunk.AsSpan(0,4),data.Length);
    System.Text.Encoding.ASCII.GetBytes(name).CopyTo(chunk,4);
    data.CopyTo(chunk,8);
    uint crc=0xffffffffu;
    foreach(var b in chunk.AsSpan(4,4+data.Length))
    {
        crc^=b;
        for(var i=0;i<8;i++) crc=(crc&1)!=0?(crc>>1)^0xedb88320u:crc>>1;
    }
    BinaryPrimitives.WriteUInt32BigEndian(chunk.AsSpan(8+data.Length,4),~crc);
    return chunk;
}
static byte[] Zip(byte[] bytes)
{
    using var output=new MemoryStream();
    using(var z=new ZLibStream(output,CompressionLevel.Fastest,true)) z.Write(bytes);
    return output.ToArray();
}
byte[] WithIdat(byte[] data) => png[..33].Concat(TestChunk("IDAT",data)).Concat(png[^12..]).ToArray();
var scan=new byte[64*257];
var invalidFilter=scan.ToArray();invalidFilter[0]=1;
var extraText=png[..^12].Concat(TestChunk("tEXt","C06_09_SYNTHETIC_METADATA"u8.ToArray())).Concat(png[^12..]).ToArray();
var hostileContainers=new[]{WithIdat(Zip(new byte[64*257+1])),WithIdat(Zip(new byte[64*257-1])),
    WithIdat(Zip(invalidFilter)),WithIdat(new byte[]{0x78,0x00,0xff}),extraText};
foreach(var malformed in hostileContainers)
{
    try { _=C06ProtectedContent.NormalizePhoto(malformed); Check(false,"inner malformed raster accepted"); }
    catch(InvalidOperationException ex) when(ex.Message=="Invalid protected content.") { count++; }
}
var expectedBlank=C06ProtectedContent.EncodePhoto(new byte[64*64*4]);
var reencoded=C06ProtectedContent.NormalizePhoto(WithIdat(Zip(scan)));
Check(reencoded.SequenceEqual(expectedBlank),"reconstruction strips original compressor representation");
var hidden="C06_09_SYNTHETIC_IDAT_TRAILING"u8.ToArray();
var hiddenInput=WithIdat(Zip(scan).Concat(hidden).ToArray());
try
{
    var clean=C06ProtectedContent.NormalizePhoto(hiddenInput);
    Check(clean.SequenceEqual(expectedBlank)&&!clean.AsSpan().SequenceEqual(hiddenInput),"trailing compressed bytes never forwarded");
}
catch(InvalidOperationException ex) when(ex.Message=="Invalid protected content.") { count++; }
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
