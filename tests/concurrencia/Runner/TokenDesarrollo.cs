using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace QA07;

// S3-EV-02 (#223): Evaluaciones ya no acepta X-Institucion-Id; lee institución, usuario y rol del JWT.
// QA-07 genera un token de Docente de desarrollo firmado con la misma clave que usa la API en
// Development (Jwt:SecretKey de appsettings.Development.json). Se puede pisar con QA07_JWT_SECRET.
public static class TokenDesarrollo
{
    private const string ClaveDesarrollo = "DesarrolloLocalSecretoCompartidoPEI2026_ClaveSegura32BytesMinimo!";

    public static string Docente(Guid institucionId, Guid docenteId)
    {
        var secreto = Environment.GetEnvironmentVariable("QA07_JWT_SECRET") ?? ClaveDesarrollo;
        var ahora = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        var encabezado = Base64Url(JsonSerializer.SerializeToUtf8Bytes(new Dictionary<string, object>
        {
            ["alg"] = "HS256",
            ["typ"] = "JWT"
        }));
        var datos = Base64Url(JsonSerializer.SerializeToUtf8Bytes(new Dictionary<string, object>
        {
            ["sub"] = docenteId.ToString(),
            ["institucion_id"] = institucionId.ToString(),
            ["role"] = "Docente",
            ["email"] = "qa07@tiza.test",
            ["jti"] = Guid.NewGuid().ToString(),
            ["iat"] = ahora,
            ["nbf"] = ahora,
            ["exp"] = ahora + 3600,
            ["iss"] = "PEI.Identidad",
            ["aud"] = "PEI"
        }));

        var sinFirma = $"{encabezado}.{datos}";
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secreto));
        var firma = Base64Url(hmac.ComputeHash(Encoding.ASCII.GetBytes(sinFirma)));
        return $"{sinFirma}.{firma}";
    }

    private static string Base64Url(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
