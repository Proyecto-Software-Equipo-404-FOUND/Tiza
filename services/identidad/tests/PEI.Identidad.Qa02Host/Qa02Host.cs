using System.Text.Json;
using PEI.Identidad.UnitTests;

namespace PEI.Identidad.Qa02Host;

// Puente exclusivamente de pruebas: Newman -> loopback -> TestServer de la API real.
// No se agrega a la solución productiva ni se publica con la API.
public static class Qa02Host
{
    public static async Task Main()
    {
        using var fixture=new Qa02Fixture();
        var builder=WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName="Testing",Args=[] });
        builder.Configuration.Sources.Clear();
        builder.Configuration.AddInMemoryCollection();
        builder.WebHost.UseUrls("http://127.0.0.1:18502");
        builder.Logging.ClearProviders();
        var app=builder.Build();
        app.Run(async context=>
        {
            if(context.Request.Path=="/__qa02/audit" && context.Request.Method=="GET")
            {
                await context.Response.WriteAsJsonAsync(new {users=fixture.UserCount,platformWrites=fixture.PlatformWrites});return;
            }
            if(!context.Request.Path.StartsWithSegments("/api/auth") && !context.Request.Path.StartsWithSegments("/api/plataforma"))
            {context.Response.StatusCode=404;return;}
            using var request=new HttpRequestMessage(new HttpMethod(context.Request.Method),context.Request.Path+context.Request.QueryString);
            if(context.Request.ContentLength>0 || context.Request.ContentType!=null)
                request.Content=new StreamContent(context.Request.Body);
            foreach(var header in context.Request.Headers)
                if(!new[]{"Host","Connection","Transfer-Encoding","Content-Length"}.Contains(header.Key,StringComparer.OrdinalIgnoreCase))
                    if(!request.Headers.TryAddWithoutValidation(header.Key,header.Value.ToArray()))
                        request.Content?.Headers.TryAddWithoutValidation(header.Key,header.Value.ToArray());
            using var response=await fixture.Client.SendAsync(request);
            context.Response.StatusCode=(int)response.StatusCode;
            foreach(var header in response.Headers.Concat(response.Content.Headers))
                if(!header.Key.Equals("Transfer-Encoding",StringComparison.OrdinalIgnoreCase))context.Response.Headers[header.Key]=header.Value.ToArray();
            await response.Content.CopyToAsync(context.Response.Body);
        });
        await app.StartAsync();
        // IPC privado con el runner padre: no redirigir stdout a archivos/logs/CI.
        Console.WriteLine(JsonSerializer.Serialize(fixture.Variables));
        await app.WaitForShutdownAsync();
    }
}
