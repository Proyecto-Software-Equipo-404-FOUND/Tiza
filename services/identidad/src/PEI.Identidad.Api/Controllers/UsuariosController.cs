using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PEI.Identidad.Application.UseCases.Usuarios;
using PEI.Identidad.Application.UseCases.Usuarios.Estado;
using PEI.Identidad.Domain.Security;

namespace PEI.Identidad.Api.Controllers;

// S2-ID-10 (#91). Contrato: contracts/openapi/identidad-v1.json, rutas /api/usuarios.
// Solo Administrador o Docente; un Alumno recibe 403 y sin token 401.
[ApiController, Route("api/usuarios")]
[Authorize(Roles = IdentityRoles.Administrador + "," + IdentityRoles.Docente)]
public sealed class UsuariosController(
    ListarUsuariosHandler listar,
    ObtenerUsuarioHandler obtener,
    DarBajaUsuarioHandler darBaja,
    ReactivarUsuarioHandler reactivar) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<UsuarioResumen>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Listar([FromQuery] string? rol, [FromQuery] bool incluirInactivos, CancellationToken ct)
    {
        // Solo el administrador ve a los dados de baja (para reactivarlos); el resto sigue viendo solo activos.
        var rolUsuario = User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value ?? User.FindFirst("role")?.Value;
        var verInactivos = incluirInactivos && string.Equals(rolUsuario, "Administrador", StringComparison.OrdinalIgnoreCase);
        return Ok(await listar.HandleAsync(rol, verInactivos, ct));
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(UsuarioResumen), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Obtener(Guid id, CancellationToken ct)
    {
        var usuario = await obtener.HandleAsync(id, ct);
        return usuario is null
            ? Problem(title: "Usuario no encontrado.", statusCode: StatusCodes.Status404NotFound)
            : Ok(usuario);
    }

    [HttpPost("{id:guid}/baja")]
    [Authorize(Policy = "InstitutionAdmin")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Baja(Guid id, CancellationToken ct)
    {
        var resultado = await darBaja.HandleAsync(new DarBajaUsuarioCommand(id), ct);
        return resultado switch
        {
            ModificarEstadoUsuarioResultado.Exitoso => NoContent(),
            ModificarEstadoUsuarioResultado.NoEncontrado => Problem(title: "Usuario no encontrado.", statusCode: StatusCodes.Status404NotFound),
            ModificarEstadoUsuarioResultado.RolNoPermitido => Problem(title: "Operacion no autorizada para el rol del usuario.", statusCode: StatusCodes.Status403Forbidden),
            _ => Problem(title: "Error inesperado.", statusCode: StatusCodes.Status500InternalServerError)
        };
    }

    [HttpPost("{id:guid}/reactivar")]
    [Authorize(Policy = "InstitutionAdmin")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Reactivar(Guid id, CancellationToken ct)
    {
        var resultado = await reactivar.HandleAsync(new ReactivarUsuarioCommand(id), ct);
        return resultado switch
        {
            ModificarEstadoUsuarioResultado.Exitoso => NoContent(),
            ModificarEstadoUsuarioResultado.NoEncontrado => Problem(title: "Usuario no encontrado.", statusCode: StatusCodes.Status404NotFound),
            ModificarEstadoUsuarioResultado.RolNoPermitido => Problem(title: "Operacion no autorizada para el rol del usuario.", statusCode: StatusCodes.Status403Forbidden),
            ModificarEstadoUsuarioResultado.PendienteActivacion => Problem(
                title: "Este alumno todavía no activó su cuenta: no tiene contraseña. Volvé a importarlo con una contraseña inicial.",
                type: "USUARIO_PENDIENTE_ACTIVACION",
                statusCode: StatusCodes.Status409Conflict),
            _ => Problem(title: "Error inesperado.", statusCode: StatusCodes.Status500InternalServerError)
        };
    }
}
