using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Options;
using PEI.Identidad.Domain.Entities;
using PEI.Identidad.Infrastructure.Security;
namespace PEI.Identidad.Api.Security;
// Herramienta de consola: retorna antes de construir o publicar un servidor HTTP.
public static class DevelopmentTokenCommand
{
    public static void ValidateEnvironment(IConfiguration configuration, string environment)
    {
        if (environment != Environments.Development || !configuration.GetValue<bool>("DevelopmentTools:EnableTestTokens"))
            throw new InvalidOperationException("Tokens locales deshabilitados fuera de Development explicitamente habilitado.");
    }
    public static JwtOptions ReadOptions(IConfiguration configuration)
    {
        var dev = configuration.GetSection("DevelopmentTools:Jwt").Get<JwtOptions>() ?? new();
        if (!dev.IsValid() || dev.SecretKey == configuration["Jwt:SecretKey"] || dev.Issuer == configuration["Jwt:Issuer"])
            throw new InvalidOperationException("Los tokens locales requieren clave e issuer separados del emisor ordinario.");
        return dev;
    }
    public static void ValidateLocalBindings(IEnumerable<string> urls)
    {
        if (!urls.Any() || urls.Any(url => !Uri.TryCreate(url, UriKind.Absolute, out var uri) ||
            !(uri.Host == "localhost" || IPAddress.TryParse(uri.Host.Trim('[', ']'), out var ip) && IPAddress.IsLoopback(ip))))
            throw new InvalidOperationException("La confianza de tokens locales requiere URLs de escucha exclusivamente loopback explicitas.");
    }
    public static void ValidateConfiguredBindings(IConfiguration configuration)
    {
        var endpoints = configuration.GetSection("Kestrel:Endpoints").GetChildren().ToArray();
        ValidateLocalBindings(endpoints.Length > 0
            ? endpoints.Select(e => e["Url"] ?? "")
            : (configuration["urls"] ?? "").Split(';', StringSplitOptions.RemoveEmptyEntries));
    }
    public static string Generate(IConfiguration configuration, string environment)
    {
        ValidateEnvironment(configuration, environment);
        var dev = ReadOptions(configuration);
        if (!Guid.TryParse(configuration["DevelopmentTools:UsuarioId"], out var usuario) ||
            !Guid.TryParse(configuration["DevelopmentTools:InstitucionId"], out var institucion))
            throw new ArgumentException("Configurar GUIDs locales de usuario e institucion.");
        var token = new JwtTokenService(Options.Create(dev)).GenerateToken(new Usuario
        { Id = usuario, InstitucionId = institucion, Rol = configuration["DevelopmentTools:Rol"] ?? "", Email = "" });
        return JsonSerializer.Serialize(new { access_token = token, token_type = "Bearer", expires_in = dev.AccessTokenMinutes * 60 });
    }
}
