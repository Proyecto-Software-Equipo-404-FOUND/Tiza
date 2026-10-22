using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using PEI.Identidad.Api.Security;
using PEI.Identidad.Application.Plataforma;
using PEI.Identidad.Application.UseCases.Auth.Login;
using PEI.Identidad.Infrastructure.Security;

namespace PEI.Identidad.Api.Controllers;

[ApiController, Route("api/plataforma")]
public sealed class PlataformaController(PlataformaOptions options) : ControllerBase
{
    [AllowAnonymous, HttpPost("auth/login"), EnableRateLimiting("login")]
    public async Task<IActionResult> Login(LoginCommand command, CancellationToken ct)
    {
        if (!options.Enabled) return NotFound();
        return Ok(new { accessToken = await HttpContext.RequestServices.GetRequiredService<PlataformaHandlers>().LoginAsync(command, ct), tokenType = "Bearer" });
    }

    [Authorize(Policy = PlataformaSetup.Policy), HttpPost("instituciones")]
    public async Task<IActionResult> Crear(CrearInstitucionCommand command, CancellationToken ct)
    {
        if (!options.Enabled) return NotFound();
        if (!PlataformaJwtContract.TryRead(User, out var actor)) return Unauthorized();
        var resultado = await HttpContext.RequestServices.GetRequiredService<PlataformaHandlers>().CrearAsync(actor, command, ct);
        return StatusCode(201, resultado);
    }

    [Authorize(Policy = PlataformaSetup.Policy), HttpGet("instituciones")]
    public async Task<IActionResult> Listar([FromQuery] int pagina = 1, CancellationToken ct = default)
    {
        if (!options.Enabled) return NotFound();
        if (!PlataformaJwtContract.TryRead(User, out var actor)) return Unauthorized();
        return Ok(await HttpContext.RequestServices.GetRequiredService<IPlataformaDataAccess>().ListarInstitucionesAsync(actor, pagina, ct));
    }

    [AllowAnonymous, HttpPost("invitaciones/activar"), EnableRateLimiting("login")]
    public async Task<IActionResult> Activar(ActivarAdministradorCommand command, CancellationToken ct)
    {
        if (!options.Enabled) return NotFound();
        await HttpContext.RequestServices.GetRequiredService<PlataformaHandlers>().ActivarAsync(command, ct);
        return NoContent();
    }
}
