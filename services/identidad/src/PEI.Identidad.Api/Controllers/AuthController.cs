using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PEI.Identidad.Application.UseCases.Auth.Login;
using PEI.Identidad.Application.UseCases.Auth.Register;

namespace PEI.Identidad.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly RegisterHandler _registerHandler;
    private readonly LoginHandler _loginHandler;

    public AuthController(
        RegisterHandler registerHandler,
        LoginHandler loginHandler)
    {
        _registerHandler = registerHandler;
        _loginHandler = loginHandler;
    }


    [ProducesResponseType(StatusCodes.Status200Created)]
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
            var token = await _loginHandler.HandleAsync(command);

            return Ok(new
            {
                accessToken = token,
                tokenType = "Bearer"
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
}