using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using PEI.Identidad.Application.UseCases.Auth.Login;
using PEI.Identidad.Application.UseCases.Auth.Register;
using PEI.Identidad.Application.UseCases.Auth.Refresh;
using PEI.Identidad.Domain.Security;
using PEI.Identidad.Api.Contracts;
namespace PEI.Identidad.Api.Controllers;
[ApiController, Route("api/auth")]
public sealed class AuthController(InstitutionalSessionService sessions, RegisterHandler register, RefreshHandler refresh, LogoutHandler logout) : ControllerBase
{
    [AllowAnonymous, HttpPost("login"), EnableRateLimiting("login")]
    [ProducesResponseType(typeof(SessionResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> Login(LoginCommand command, CancellationToken ct) => TokenResponse(await sessions.LoginAsync(command, ct));
    [Authorize(Policy = "InstitutionAdmin"), HttpPost("register")]
    public async Task<IActionResult> Register(RegisterCommand command, CancellationToken ct) => StatusCode(StatusCodes.Status201Created, new { id = await register.HandleAsync(command, ct), message = "Usuario registrado correctamente." });

    [AllowAnonymous, HttpPost("refresh"), EnableRateLimiting("login")]
    [ProducesResponseType(typeof(SessionResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> Refresh(RefreshCommand command, CancellationToken ct) => TokenResponse(await refresh.HandleAsync(command, ct));

    [AllowAnonymous, HttpPost("logout"), EnableRateLimiting("login")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> Logout(LogoutCommand command, CancellationToken ct)
    {
        await logout.HandleAsync(command, ct);
        return NoContent();
    }

    [Authorize(Policy = "InstitutionAdmin"), HttpGet("administrador")]
    public IActionResult SoloAdministrador() => Ok(new { mensaje = "Acceso autorizado para Administrador." });

    [Authorize(Roles = IdentityRoles.Docente), HttpGet("docente")]
    public IActionResult SoloDocente() => Ok(new { mensaje = "Acceso autorizado para Docente." });

    [Authorize(Roles = IdentityRoles.Alumno), HttpGet("alumno")]
    public IActionResult SoloAlumno() => Ok(new { mensaje = "Acceso autorizado para Alumno." });

    private IActionResult TokenResponse(LoginResult resultado)
    {
        Response.Headers.CacheControl = "no-store";
        Response.Headers.Pragma = "no-cache";
        return Ok(SessionResponse.From(resultado));
    }
}
