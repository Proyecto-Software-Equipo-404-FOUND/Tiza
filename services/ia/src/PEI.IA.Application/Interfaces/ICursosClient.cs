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
    Task<TextoMaterialCursoDto?> ObtenerTextoMaterialAsync(Guid materialId, CancellationToken ct = default);
}
