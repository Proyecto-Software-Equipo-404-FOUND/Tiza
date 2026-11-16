using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PEI.BuildingBlocks.MultiTenancy;
using PEI.Identidad.Application.UseCases.Usuarios.Importacion;
using PEI.Identidad.Domain.Security;

namespace PEI.Identidad.Api.Controllers;

[ApiController, Route("api/usuarios/alumnos/importacion")]
[Authorize(Roles = IdentityRoles.Administrador)]
public sealed class ImportacionAlumnosController(ImportarAlumnosHandler importar, ITenantContext tenant) : ControllerBase
{
    // Archivo >1 MiB: 400 si llega a la acción. Un cuerpo multipart que exceda
    // el límite total acotado por el servidor se rechaza antes con 413.
    [HttpPost, Consumes("multipart/form-data")]
    [RequestSizeLimit(CsvAlumnosParser.MaximoBytes + 65536)]
    [RequestFormLimits(MultipartBodyLengthLimit = CsvAlumnosParser.MaximoBytes + 65536)]
    [ProducesResponseType(typeof(ResultadoImportacionAlumnos), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status413PayloadTooLarge)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Importar([FromForm(Name = "archivo")] IFormFile? archivo, CancellationToken ct)
    {
        if (archivo is null) return BadRequest(new ProblemDetails { Status = 400, Title = "Debe enviar el archivo en el campo archivo." });
        if (archivo.Length > CsvAlumnosParser.MaximoBytes)
            return BadRequest(new ProblemDetails { Status = 400, Title = "El archivo supera el máximo de 1 MiB." });
        if (tenant.InstitucionId is not Guid institucionId || institucionId == Guid.Empty ||
            tenant.UsuarioId is not Guid administradorId || administradorId == Guid.Empty ||
            !tenant.EstaAutenticado || tenant.Rol != IdentityRoles.Administrador)
            return Forbid();

        try
        {
            await using var stream = archivo.OpenReadStream();
            var bytes = GC.AllocateUninitializedArray<byte>(checked((int)archivo.Length));
            await stream.ReadExactlyAsync(bytes.AsMemory(), ct);
            var filas = CsvAlumnosParser.Parse(bytes);
            return Ok(await importar.HandleAsync(administradorId, institucionId, filas, ct));
        }
        catch (CsvImportFormatException exception)
        {
            return BadRequest(new ProblemDetails { Status = 400, Title = exception.Message });
        }
    }
}
