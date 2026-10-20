
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;

namespace PEI.Identidad.Api.Controllers;

[ApiController]
[Route("api/v1/tokens")]
public class TokensController : ControllerBase
{
    private readonly IConfiguration _configuration;
    private readonly IWebHostEnvironment _environment;

    public TokensController(
        IConfiguration configuration,
        IWebHostEnvironment environment)
    {
        _configuration = configuration;
        _environment = environment;
    }

    [HttpPost("prueba")]
    public IActionResult GenerarTokenPrueba(
        [FromBody] TokenPruebaRequest request)
    {
        // Este endpoint solo debe funcionar en desarrollo.
        if (!_environment.IsDevelopment())
            return NotFound();

        if (request.InstitucionId == Guid.Empty ||
            request.UsuarioId == Guid.Empty ||
            string.IsNullOrWhiteSpace(request.Rol))
        {
            return BadRequest(new
            {
                error = "InstitucionId, UsuarioId y Rol son obligatorios."
            });
        }

        var secret = _configuration["Jwt:Secret"];
        var issuer = _configuration["Jwt:Issuer"];
        var audience = _configuration["Jwt:Audience"];

        if (string.IsNullOrWhiteSpace(secret) ||
            Encoding.UTF8.GetByteCount(secret) < 32 ||
            string.IsNullOrWhiteSpace(issuer) ||
            string.IsNullOrWhiteSpace(audience))
        {
            throw new InvalidOperationException(
                "La configuración JWT es inválida.");
        }

        var rolesPermitidos = new[]
        {
            "Alumno",
            "Docente",
            "Administrador"
        };

        if (!rolesPermitidos.Contains(
                request.Rol,
                StringComparer.OrdinalIgnoreCase))
        {
            return BadRequest(new
            {
                error = "El rol debe ser Alumno, Docente o Administrador."
            });
        }

        var claims = new[]
        {
            new Claim(
                "InstitucionId",
                request.InstitucionId.ToString()),

            new Claim(
                "UsuarioId",
                request.UsuarioId.ToString()),

            new Claim(
                ClaimTypes.NameIdentifier,
                request.UsuarioId.ToString()),

            new Claim(
                ClaimTypes.Role,
                request.Rol)
        };

        var key = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(secret));

        var credentials = new SigningCredentials(
            key,
            SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            notBefore: DateTime.UtcNow,
            expires: DateTime.UtcNow.AddMinutes(15),
            signingCredentials: credentials);

        return Ok(new
        {
            access_token = new JwtSecurityTokenHandler()
                .WriteToken(token),
            token_type = "Bearer",
            expires_in = 900
        });
    }
}

public sealed record TokenPruebaRequest(
    Guid InstitucionId,
    Guid UsuarioId,
    string Rol);
