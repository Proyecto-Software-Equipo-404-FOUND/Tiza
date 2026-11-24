using System.ComponentModel;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;
using PEI.IA.Application.Interfaces;

namespace PEI.IA.McpServer.Tools;

/// <summary>
/// Resultado de la consulta de texto de un apunte para el LLM o agentes.
/// </summary>
public record ResultadoTextoApunte(
    bool Exito,
    Guid? MaterialId,
    string? Titulo,
    int CantidadPaginas,
    string? Texto,
    bool TieneTexto,
    string? Error)
{
    public static ResultadoTextoApunte Fallido(string error) =>
        new(false, null, null, 0, null, false, error);
}

/// <summary>
/// Tool MCP que permite a los agentes de IA obtener el texto de un apunte desde el microservicio Cursos.
/// </summary>
[McpServerToolType]
public class ObtenerTextoApunteTool
{
    private readonly ICursosClient _cursosClient;
    private readonly ILogger<ObtenerTextoApunteTool> _logger;

    public ObtenerTextoApunteTool(ICursosClient cursosClient, ILogger<ObtenerTextoApunteTool> logger)
    {
        _cursosClient = cursosClient;
        _logger = logger;
    }

    [McpServerTool, Description("Devuelve el texto extraído y metadatos de un apunte del curso a partir de su ID.")]
    public async Task<ResultadoTextoApunte> ObtenerTextoApunte(
        [Description("Identificador único del apunte (GUID).")] Guid materialId)
    {
        _logger.LogInformation("[MCP Tool] ObtenerTextoApunte solicitado para MaterialId: {MaterialId}", materialId);
        TextoMaterialCursoDto? material;
        try
        {
            material = await _cursosClient.ObtenerTextoMaterialAsync(materialId);
        }
        catch (Exception ex)
        {
            // 401/403 de Cursos o Cursos caído: la tool informa el error en vez de cortar el servidor MCP.
            _logger.LogWarning(ex, "[MCP Tool] No se pudo obtener el texto del apunte {MaterialId}.", materialId);
            return ResultadoTextoApunte.Fallido("No se pudo obtener el apunte desde Cursos.");
        }

        if (material == null)
        {
            return ResultadoTextoApunte.Fallido($"No se encontró el apunte {materialId} o no está disponible.");
        }

        if (!material.TieneTexto || string.IsNullOrWhiteSpace(material.Texto))
        {
            return new ResultadoTextoApunte(
                Exito: true,
                MaterialId: material.MaterialId,
                Titulo: material.Titulo,
                CantidadPaginas: material.CantidadPaginas,
                Texto: string.Empty,
                TieneTexto: false,
                Error: "El apunte no contiene texto legible (posible PDF escaneado)."
            );
        }

        return new ResultadoTextoApunte(
            Exito: true,
            MaterialId: material.MaterialId,
            Titulo: material.Titulo,
            CantidadPaginas: material.CantidadPaginas,
            Texto: material.Texto,
            TieneTexto: true,
            Error: null
        );
    }
}
