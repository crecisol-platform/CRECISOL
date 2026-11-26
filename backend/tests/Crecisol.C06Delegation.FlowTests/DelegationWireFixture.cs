using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Crecisol.Api.Modules.Identity;
using Crecisol.Application.Identity;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Crecisol.C06Delegation.FlowTests;

// Actual Kestrel loopback socket and production identity endpoint mapping,
// using the real MySQL flow of this disposable test. Not a mock of MFA/DB.
// The rest of Program.cs ingress middleware is exercised by historical E2E,
// not by this focused fixture; no outbox delivery or external service starts.
internal sealed class DelegationWireFixture : IAsyncDisposable
{
    private readonly WebApplication app;
    private readonly HttpClient client;
    private readonly byte[] tokenKey;
    private static readonly string[] EnvNames = ["CRECISOL_C05_KEY_VERSION","CRECISOL_C05_TOKEN_HMAC_KEY",
        "CRECISOL_C05_FACTOR_ENCRYPTION_KEY","CRECISOL_C05_LOGIN_LOOKUP_HMAC_KEY","CRECISOL_C05_MAILBOX_HMAC_KEY"];
    private DelegationWireFixture(WebApplication app,HttpClient client,byte[] key)
    {this.app=app;this.client=client;tokenKey=key.ToArray();}
    internal static async Task<DelegationWireFixture> Start(string dsn,IOperationalIdentityFlow flow,
        byte[] tokenKey,byte[] enc,byte[] login,byte[] mailbox)
    {
        var values=new[]{"1",Convert.ToHexString(tokenKey),Convert.ToHexString(enc),Convert.ToHexString(login),Convert.ToHexString(mailbox)};
        for(int i=0;i<EnvNames.Length;i++)Environment.SetEnvironmentVariable(EnvNames[i],values[i]);
        WebApplication? app=null;
        try
        {
            var builder=WebApplication.CreateBuilder(new WebApplicationOptions{Args=[],EnvironmentName="Production"});
            builder.Logging.ClearProviders();
            builder.WebHost.UseUrls("http://127.0.0.1:0");
            builder.Services.AddLocalOperationalIdentity(dsn);
            var outbox=builder.Services.Single(d=>d.ImplementationType?.FullName=="Crecisol.Api.Modules.Identity.LocalSensitiveProofDelivery");
            builder.Services.Remove(outbox);
            builder.Services.AddSingleton<IOperationalIdentityFlow>(flow);
            app=builder.Build();
            app.MapLocalOperationalIdentity();
            await app.StartAsync();
            var address=new Uri(app.Urls.Single());
            if(address.Host!="127.0.0.1" || address.Port==0)
                throw new InvalidOperationException("Wire fixture not isolated.");
            var client=new HttpClient(new HttpClientHandler{UseCookies=false,UseProxy=false,AllowAutoRedirect=false})
            {BaseAddress=address,Timeout=TimeSpan.FromSeconds(15)};
            return new(app,client,tokenKey);
        }
        catch
        {
            if(app is not null)await app.DisposeAsync();
            foreach(var name in EnvNames)Environment.SetEnvironmentVariable(name,null);
            throw;
        }
    }
    internal async Task<HttpResponseMessage> Send(string op,IReadOnlyDictionary<string,string> fields,
        string? bearer=null,string host="delegation-a.example.test",string? raw=null,
        string? extraHeader=null,string? origin=null,bool browser=false,bool csrf=true)
    {
        using var request=new HttpRequestMessage(HttpMethod.Post,"/api/v1/identity/"+op);
        request.Content=new StringContent(raw??JsonSerializer.Serialize(fields),Encoding.UTF8,"application/json");
        // The existing contract requires application/json exactly, not charset.
        request.Content.Headers.ContentType=new MediaTypeHeaderValue("application/json");
        request.Headers.Host=host;
        if(browser)
        {
            request.Headers.TryAddWithoutValidation("X-Crecisol-Browser","1");
            request.Headers.TryAddWithoutValidation("Origin",origin??"http://localhost:4200");
            if(bearer is not null)
            {
                request.Headers.TryAddWithoutValidation("Cookie","__Host-crecisol-c05="+bearer);
                if(csrf)request.Headers.TryAddWithoutValidation("X-Crecisol-CSRF",Convert.ToHexString(
                    HMACSHA256.HashData(tokenKey,Encoding.ASCII.GetBytes("C05/browser/csrf:__Host-crecisol-c05:"+bearer))));
            }
        }
        else
        {
            if(bearer is not null)request.Headers.Authorization=new AuthenticationHeaderValue("Bearer",bearer);
            if(origin is not null)request.Headers.TryAddWithoutValidation("Origin",origin);
        }
        if(extraHeader is not null)request.Headers.TryAddWithoutValidation(extraHeader,"untrusted");
        return await client.SendAsync(request);
    }
    public async ValueTask DisposeAsync()
    {
        client.Dispose();
        try {await app.StopAsync();await app.DisposeAsync();}
        finally
        {
            CryptographicOperations.ZeroMemory(tokenKey);
            foreach(var name in EnvNames)Environment.SetEnvironmentVariable(name,null);
        }
    }
}
