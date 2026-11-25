using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using PEI.Identidad.Application.UseCases.Auth.CambiarContrasena;
using PEI.Identidad.Application.UseCases.Auth.Login;
using PEI.Identidad.Application.UseCases.Auth.Register;
using PEI.Identidad.Application.UseCases.Auth.Refresh;
using PEI.Identidad.Domain.Security;
using PEI.Identidad.Api.Contracts;
namespace PEI.Identidad.Api.Controllers;
[ApiController, Route("api/auth")]
public sealed class AuthController(
    InstitutionalSessionService sessions,
    RegisterHandler register,
    RefreshHandler refresh,
    LogoutHandler logout,
    CambiarContrasenaHandler cambiarContrasena) : ControllerBase
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

    /// <summary>
    /// S3-ID-03: el usuario (administrador, docente o alumno) cambia su propia contraseña.
    /// Los errores de negocio son 400 con el código en type (no 401, para que el front no lo tome como sesión vencida).
    /// Al cambiarla se cierran todas sus sesiones: el front vuelve a pedir el ingreso con la contraseña nueva.
    /// </summary>
    [Authorize, HttpPost("cambiar-contrasena"), EnableRateLimiting("login")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> CambiarContrasena(CambiarContrasenaRequest peticion, CancellationToken ct)
    {
        var rol = User.FindFirst("role")?.Value;
        if (rol is not (IdentityRoles.Administrador or IdentityRoles.Docente or IdentityRoles.Alumno))
            return Problem(title: "Solo los usuarios con cuenta pueden cambiar su contraseña.", statusCode: StatusCodes.Status403Forbidden);

        if (!Guid.TryParse(User.FindFirst("sub")?.Value, out var usuarioId))
            return Problem(title: "No autenticado.", statusCode: StatusCodes.Status401Unauthorized);

        var resultado = await cambiarContrasena.HandleAsync(
            new CambiarContrasenaCommand(usuarioId, peticion.ContrasenaActual, peticion.ContrasenaNueva), ct);

        return resultado switch
        {
            CambiarContrasenaResultado.Exitoso => NoContent(),
            CambiarContrasenaResultado.ContrasenaActualIncorrecta => Problem(
                title: "La contraseña actual no es correcta.", type: "CONTRASENA_ACTUAL_INCORRECTA", statusCode: StatusCodes.Status400BadRequest),
            CambiarContrasenaResultado.ContrasenaNuevaInvalida => Problem(
                title: $"La contraseña nueva tiene que tener al menos {CambiarContrasenaHandler.Minima} caracteres (y no más de {CambiarContrasenaHandler.MaximaBytes} bytes).",
                type: "CONTRASENA_NUEVA_INVALIDA", statusCode: StatusCodes.Status400BadRequest),
            CambiarContrasenaResultado.ContrasenaIgualALaActual => Problem(
                title: "La contraseña nueva tiene que ser distinta de la actual.", type: "CONTRASENA_IGUAL_A_LA_ACTUAL", statusCode: StatusCodes.Status400BadRequest),
            _ => Problem(title: "No encontramos tu usuario.", statusCode: StatusCodes.Status401Unauthorized)
        };
    }

    public sealed record CambiarContrasenaRequest(string? ContrasenaActual, string? ContrasenaNueva);

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
