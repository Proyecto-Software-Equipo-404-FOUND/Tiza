namespace PEI.IA.Application.Interfaces;

/// <summary>
/// DTO con la respuesta del endpoint interno de Cursos /api/v1/materiales/{id}/texto.
/// </summary>
public record TextoMaterialCursoDto(
    Guid MaterialId,
    string Titulo,
    int CantidadPaginas,
    string Texto,
    bool TieneTexto
);

/// <summary>
/// Cliente HTTP interno para comunicarse con el microservicio Cursos desde IA y MCP Server.
/// </summary>
public interface ICursosClient
{
    /// <summary>
    /// Devuelve el texto del apunte, o null si Cursos responde 404 (inexistente o de otra institución).
    /// Lanza <see cref="CursosAccesoDenegadoException"/> si Cursos responde 401/403 (sin token o sin permiso)
    /// y <c>HttpRequestException</c> si Cursos no responde o falla.
    /// </summary>
    Task<TextoMaterialCursoDto?> ObtenerTextoMaterialAsync(Guid materialId, CancellationToken ct = default);
}

/// <summary>
/// S3-IA-02: Cursos rechazó el pedido del texto (401 sin token válido, 403 sin permiso, por ejemplo un alumno).
/// Se corta antes de llamar al modelo, así no se gasta una consulta que el usuario no podía hacer.
/// </summary>
public class CursosAccesoDenegadoException : Exception
{
    public int StatusCode { get; }

    public CursosAccesoDenegadoException(int statusCode)
        : base($"Cursos rechazó el pedido del texto del apunte ({statusCode}).")
    {
        StatusCode = statusCode;
    }
}
