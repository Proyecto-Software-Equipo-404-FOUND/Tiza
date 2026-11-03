using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PEI.Identidad.Application.UseCases.Usuarios;
using PEI.Identidad.Domain.Security;

namespace PEI.Identidad.Api.Controllers;

// S2-ID-10 (#91). Contrato: contracts/openapi/identidad-v1.json, rutas /api/usuarios.
// Solo Administrador o Docente; un Alumno recibe 403 y sin token 401.
[ApiController, Route("api/usuarios")]
[Authorize(Roles = IdentityRoles.Administrador + "," + IdentityRoles.Docente)]
public sealed class UsuariosController(ListarUsuariosHandler listar, ObtenerUsuarioHandler obtener) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<UsuarioResumen>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Listar([FromQuery] string? rol, CancellationToken ct) =>
        Ok(await listar.HandleAsync(rol, ct));

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
}
