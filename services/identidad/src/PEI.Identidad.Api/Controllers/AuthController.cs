using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using PEI.Identidad.Application.UseCases.Auth.Login;
using PEI.Identidad.Application.UseCases.Auth.Register;
namespace PEI.Identidad.Api.Controllers;
[ApiController, Route("api/auth")]
public sealed class AuthController(LoginHandler login, RegisterHandler register) : ControllerBase
{
    [AllowAnonymous, HttpPost("login"), EnableRateLimiting("login")]
    public async Task<IActionResult> Login(LoginCommand command) => Ok(new { accessToken = await login.HandleAsync(command), tokenType = "Bearer" });
    [Authorize(Policy = "InstitutionAdmin"), HttpPost("register")]
    public async Task<IActionResult> Register(RegisterCommand command) => StatusCode(StatusCodes.Status201Created, new { id = await register.HandleAsync(command), message = "Usuario registrado correctamente." });
}
