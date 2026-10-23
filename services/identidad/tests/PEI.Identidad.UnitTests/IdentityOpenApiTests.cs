using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace PEI.Identidad.UnitTests;

public sealed class IdentityOpenApiTests
{
    public static TheoryData<string, string, string, string, string, string> Operations => new()
    {
        { "/api/auth/login", "post", "200", "SessionResponse", "", "200,400,401,415,429,500" },
        { "/api/auth/refresh", "post", "200", "SessionResponse", "", "200,400,401,415,429,500" },
        { "/api/auth/logout", "post", "204", "", "", "204,400,415,429,500" },
        { "/api/auth/register", "post", "201", "RegistrationResponseDocumentation", "Bearer", "201,400,401,403,409,415,500" },
        { "/api/auth/administrador", "get", "200", "RoleResponseDocumentation", "Bearer", "200,401,403,500" },
        { "/api/auth/docente", "get", "200", "RoleResponseDocumentation", "Bearer", "200,401,403,500" },
        { "/api/auth/alumno", "get", "200", "RoleResponseDocumentation", "Bearer", "200,401,403,500" },
        { "/api/plataforma/auth/login", "post", "200", "PlatformLoginResponseDocumentation", "", "200,400,401,404,415,429,500" },
        { "/api/plataforma/instituciones", "post", "201", "InstitucionCreada", "PlatformBearer", "201,400,401,403,404,409,415,500" },
        { "/api/plataforma/instituciones", "get", "200", "array", "PlatformBearer", "200,400,401,403,404,500" },
        { "/api/plataforma/invitaciones/activar", "post", "204", "", "", "204,400,401,404,415,429,500" }
    };

    [Theory, MemberData(nameof(Operations))]
    public async Task TodasLasOperacionesDescribenContratoYSeguridad(string path, string method, string success, string schema, string security, string codes)
    {
        using var factory = new IdentityHttpFactory(hostEnvironment: "Development");
        using var client = factory.Client();
        var doc = await client.GetFromJsonAsync<JsonElement>("/swagger/v1/swagger.json");
        var op = doc.GetProperty("paths").GetProperty(path).GetProperty(method);
        var responses = op.GetProperty("responses");
        Assert.Equal(codes.Split(','), responses.EnumerateObject().Select(p => p.Name).OrderBy(x => x));
        if (security == "") Assert.False(op.TryGetProperty("security", out var empty) && empty.GetArrayLength() > 0);
        else Assert.Equal(security, Assert.Single(op.GetProperty("security").EnumerateArray()).EnumerateObject().Single().Name);
        if (schema == "") Assert.False(responses.GetProperty(success).TryGetProperty("content", out _));
        else
        {
            var body = responses.GetProperty(success).GetProperty("content").GetProperty("application/json").GetProperty("schema");
            if (schema == "array")
            {
                Assert.Equal("array", body.GetProperty("type").GetString());
                Assert.Equal("#/components/schemas/InstitucionResumen", body.GetProperty("items").GetProperty("$ref").GetString());
            }
            else Assert.Equal("#/components/schemas/" + schema, body.GetProperty("$ref").GetString());
        }
        foreach (var code in codes.Split(',').Where(c => int.Parse(c) >= 400))
        {
            var body = responses.GetProperty(code).GetProperty("content").GetProperty("application/problem+json").GetProperty("schema");
            if (code == "400")
                Assert.Equal(new[] { "#/components/schemas/ProblemDetails", "#/components/schemas/ValidationProblemDetails" }, body.GetProperty("anyOf").EnumerateArray().Select(s => s.GetProperty("$ref").GetString()));
            else Assert.Equal("#/components/schemas/ProblemDetails", body.GetProperty("$ref").GetString());
        }
        if (path.StartsWith("/api/plataforma")) Assert.Contains("Platform:Enabled", op.GetProperty("description").GetString());
        if (security != "") Assert.Contains("Rol autorizado:", op.GetProperty("description").GetString());
    }

    [Fact]
    public async Task PlataformaConservaJsonEnChallengeYEsquemaGlobalSinRefresh()
    {
        using var factory = new PlatformHttpFactory();
        using var client = factory.Client();
        var denied = await client.GetAsync("/api/plataforma/instituciones");
        Assert.Equal(HttpStatusCode.Unauthorized, denied.StatusCode);
        Assert.Equal("application/json", denied.Content.Headers.ContentType?.MediaType);
        var doc = await client.GetFromJsonAsync<JsonElement>("/swagger/v1/swagger.json");
        var error = doc.GetProperty("paths").GetProperty("/api/plataforma/instituciones").GetProperty("get").GetProperty("responses").GetProperty("401");
        Assert.Equal("#/components/schemas/ProblemDetails", error.GetProperty("content").GetProperty("application/json").GetProperty("schema").GetProperty("$ref").GetString());
        var response = await client.PostAsJsonAsync("/api/plataforma/auth/login", new { Email = factory.Store.Cuenta.Email, Password = "Global123!" });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var properties = doc.GetProperty("components").GetProperty("schemas").GetProperty("PlatformLoginResponseDocumentation").GetProperty("properties");
        Assert.Equal(body.EnumerateObject().Select(p => p.Name).OrderBy(x => x), properties.EnumerateObject().Select(p => p.Name).OrderBy(x => x));
        Assert.False(body.TryGetProperty("refreshToken", out _));
    }

    [Fact]
    public async Task PlataformaDeshabilitadaDevuelve404EnAnonimos()
    {
        using var factory = new IdentityHttpFactory();
        using var client = factory.Client();
        var response = await client.PostAsJsonAsync("/api/plataforma/auth/login", new { Email = "global@test.invalid", Password = "Sintetica123!" });
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task MetadatosCoincidenConCuerposHttpYErroresReales()
    {
        using var factory = new IdentityHttpFactory();
        using var client = factory.Client(factory.Token());
        var registered = await client.PostAsJsonAsync("/api/auth/register", new { Email = "id07@test.invalid", Password = "Sintetica123!", Nombre = "ID07", Apellido = "Prueba", Rol = "Alumno" });
        Assert.Equal(HttpStatusCode.Created, registered.StatusCode);
        var json = await registered.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(json.GetProperty("id").TryGetGuid(out _));
        Assert.Equal("Usuario registrado correctamente.", json.GetProperty("message").GetString());
        var role = await client.GetFromJsonAsync<JsonElement>("/api/auth/administrador");
        Assert.True(role.TryGetProperty("mensaje", out _));
        using var anonymous = factory.Client();
        var unsupported = await anonymous.PostAsync("/api/auth/login", new StringContent("texto", Encoding.UTF8, "text/plain"));
        Assert.Equal(HttpStatusCode.UnsupportedMediaType, unsupported.StatusCode);
        Assert.Equal("application/problem+json", unsupported.Content.Headers.ContentType?.MediaType);
        var invalid = await anonymous.PostAsJsonAsync("/api/auth/login", new { });
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        Assert.True((await invalid.Content.ReadFromJsonAsync<JsonElement>()).TryGetProperty("errors", out _));
        var forbiddenUser = new PEI.Identidad.Domain.Entities.Usuario { InstitucionId = factory.Store.Admin.InstitucionId, Email = "doc@test.invalid", Rol = "Docente" };
        factory.Store.Users[forbiddenUser.Id] = forbiddenUser;
        using var forbiddenClient = factory.Client(factory.Token(forbiddenUser));
        var forbidden = await forbiddenClient.GetAsync("/api/auth/administrador");
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
        Assert.Equal("application/problem+json", forbidden.Content.Headers.ContentType?.MediaType);
    }
}
