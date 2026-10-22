using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PEI.Identidad.Application.UseCases.Auth.Login;
using PEI.Identidad.Application.UseCases.Auth.Register;
using PEI.Identidad.Application.UseCases.Auth.Refresh;

namespace PEI.Identidad.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly RegisterHandler _registerHandler;
    private readonly LoginHandler _loginHandler;
    private readonly RefreshHandler _refreshHandler;
    private readonly LogoutHandler _logoutHandler;

    public AuthController(
        RegisterHandler registerHandler,
        LoginHandler loginHandler,
        RefreshHandler refreshHandler,
        LogoutHandler logoutHandler)
    {
        _registerHandler = registerHandler;
        _loginHandler = loginHandler;
        _refreshHandler = refreshHandler;
        _logoutHandler = logoutHandler;
    }


    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]

    [AllowAnonymous]
    [HttpPost("register")]
    public async Task<IActionResult> Register(RegisterCommand command)
    {
        try
        {
           var id = await _registerHandler.HandleAsync(command);

            return Created(
                $"/api/usuarios/{id}",
                new
                {
                    id,
                    message = "Usuario registrado correctamente."
                });
        }
        catch (InvalidOperationException ex)
        {
            var problem = new ProblemDetails
            {
                Status = StatusCodes.Status409Conflict,
                Title = "No se pudo registrar el usuario",
                Detail = ex.Message,
                Instance = HttpContext.Request.Path
            };

            return Conflict(problem);
        }
    }


    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    
    [AllowAnonymous]
    [HttpPost("login")]
    public async Task<IActionResult> Login(LoginCommand command)
    {
        try
        {
            var resultado = await _loginHandler.HandleAsync(command);

            return Ok(new
            {
                accessToken = resultado.AccessToken,
                refreshToken = resultado.RefreshToken,
                tokenType = "Bearer",
                accessTokenExpiresAt = resultado.AccessTokenExpiresAt,
                refreshTokenExpiresAt = resultado.RefreshTokenExpiresAt
            });
        }
        catch (UnauthorizedAccessException)
        {
            var problem = new ProblemDetails
            {
                Status = StatusCodes.Status401Unauthorized,
                Title = "No autorizado",
                Detail = "Credenciales inválidas.",
                Instance = HttpContext.Request.Path
            };

            return Unauthorized(problem);
        }
    }

    [AllowAnonymous]
    [HttpPost("refresh")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Refresh(RefreshCommand command)
    {
        try
        {
            var resultado = await _refreshHandler.HandleAsync(command);

            return Ok(new
            {
                accessToken = resultado.AccessToken,
                refreshToken = resultado.RefreshToken,
                tokenType = "Bearer",
                accessTokenExpiresAt = resultado.AccessTokenExpiresAt,
                refreshTokenExpiresAt = resultado.RefreshTokenExpiresAt
            });
        }
        catch (UnauthorizedAccessException)
        {
            return Unauthorized(new ProblemDetails
            {
                Status = StatusCodes.Status401Unauthorized,
                Title = "No autorizado",
                Detail = "El refresh token es inválido, venció o fue revocado.",
                Instance = HttpContext.Request.Path
            });
        }
    }

    [AllowAnonymous]
    [HttpPost("logout")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Logout(LogoutCommand command)
    {
        await _logoutHandler.HandleAsync(command);
        return NoContent();
    }

    
    [HttpGet("administrador")]
    [Authorize(Roles = "Administrador")]
    public IActionResult SoloAdministrador()
    {
        return Ok(new
        {
            mensaje = "Acceso autorizado para Administrador."
        });
    }

    [HttpGet("docente")]
    [Authorize(Roles = "Docente")]
    public IActionResult SoloDocente()
    {
        return Ok(new
        {
            mensaje = "Acceso autorizado para Docente."
        });
    }

    [HttpGet("alumno")]
    [Authorize(Roles = "Alumno")]
    public IActionResult SoloAlumno()
    {
        return Ok(new
        {
            mensaje = "Acceso autorizado para Alumno."
        });
    }



}