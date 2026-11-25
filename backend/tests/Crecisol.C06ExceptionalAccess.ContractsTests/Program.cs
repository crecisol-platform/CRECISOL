using Crecisol.Domain.Authorization;

var count = 0;
void Check(bool result, string name)
{
    if (!result) throw new InvalidOperationException("C06-06 FAIL " + name);
    count++;
}
var actor = Guid.NewGuid();
var session = Guid.NewGuid();
var now = new DateTimeOffset(2026,11,24,12,0,0,TimeSpan.Zero);
var e = new C06ExceptionalReadEvidence(true,true,true,true,true,true,true,true,false,
    actor,actor,session,session,1,1,now.AddMinutes(-2),now.AddMinutes(-1),now.AddMinutes(8),now);
Check(C06ExceptionalAccessPolicy.AllowsRead(e),"exact active exception");
foreach (var bad in new[] {
    e with { ActorVerified=false }, e with { CurrentSession=false }, e with { FreshFactorVerified=false },
    e with { RequestGrantActive=false }, e with { ReviewGrantActive=false }, e with { ReviewerActive=false },
    e with { ResourceActive=false }, e with { IndependentApproval=false }, e with { Closed=true },
    e with { Actor=Guid.NewGuid() }, e with { Actor=Guid.Empty,Requester=Guid.Empty },
    e with { Session=Guid.NewGuid() }, e with { Session=Guid.Empty,RequestSession=Guid.Empty },
    e with { SecurityRevision=2 }, e with { SecurityRevision=0,RequestRevision=0 },
    e with { Now=e.ExpiresAt }, e with { ExpiresAt=now.AddMinutes(-1) },
    e with { ApprovedAt=now.AddSeconds(1) }, e with { RequestedAt=now },
    e with { ExpiresAt=e.RequestedAt.AddMinutes(10).AddTicks(1) },
    e with { Now=now.ToOffset(TimeSpan.FromHours(-6)) },
    e with { ApprovedAt=e.ApprovedAt.ToOffset(TimeSpan.FromHours(-6)) },
    e with { RequestedAt=e.RequestedAt.ToOffset(TimeSpan.FromHours(-6)) },
    e with { ExpiresAt=e.ExpiresAt.ToOffset(TimeSpan.FromHours(-6)) } })
    Check(!C06ExceptionalAccessPolicy.AllowsRead(bad),"negative evidence " + count);
Check(C06ExceptionalAccessPolicy.IndependentReview(actor,Guid.NewGuid()),"independent reviewer");
Check(!C06ExceptionalAccessPolicy.IndependentReview(actor,actor),"self review");
Check(!C06ExceptionalAccessPolicy.IndependentReview(Guid.Empty,actor),"unknown requester");
Check(!C06ExceptionalAccessPolicy.IndependentReview(actor,Guid.Empty),"unknown reviewer");
foreach(var op in new[]{"platform/breakglass/open","platform/breakglass/approve","platform/breakglass/read","platform/breakglass/close"})
    Check(C06ExceptionalAccessPolicy.IsOperation(op),"exact operation " + op);
foreach(var op in new string?[]{null,"","platform/breakglass","platform/breakglass/read/extra","PLATFORM/breakglass/read","private/read","platform/session"})
    Check(!C06ExceptionalAccessPolicy.IsOperation(op),"unregistered operation");
foreach(var bad in new string?[]{null,"","       ","------------","just short","motivo de revisión\ninválido",new string('X',513)})
    Check(!C06ExceptionalAccessPolicy.ValidPurpose(bad),"invalid reason");
Check(C06ExceptionalAccessPolicy.ValidPurpose("Justificación de incidente"),"valid reason");
Check(C06ExceptionalAccessPolicy.ValidPurpose(new string('X',512)),"max reason");
foreach(var bad in new string?[]{null,"","00000","0000000","12345x","１２３４５６",new string('g',32)})
    Check(!C06ExceptionalAccessPolicy.ValidProofShape(bad),"invalid factor form");
Check(C06ExceptionalAccessPolicy.ValidProofShape("123456"),"totp form");
Check(C06ExceptionalAccessPolicy.ValidProofShape(new string('a',32)),"backup form");
foreach(var bad in new string?[]{null,"","invalid",Guid.Empty.ToString(),actor.ToString("N")})
    Check(!C06ExceptionalAccessPolicy.ValidIdentifier(bad),"bad selector");
Check(C06ExceptionalAccessPolicy.ValidIdentifier(actor.ToString("D")),"exact selector");
Console.WriteLine($"PASS C06-06 policy contracts={count}; pure evidence tests, not cryptographic proof");
