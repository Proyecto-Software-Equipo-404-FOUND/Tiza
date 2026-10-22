using PEI.IA.Application.DTOs;

namespace PEI.IA.Application.Interfaces;

/// <summary>
/// Servicio de aplicación que orquesta el flujo de corrección automatizada.
/// </summary>
public interface ICorreccionService
{
    /// <summary>
    /// Corrige una respuesta abierta usando IA basada en la rúbrica del docente.
    /// </summary>
    /// <param name="request">Datos de la respuesta a corregir.</param>
    /// <param name="cancellationToken">Token de cancelación.</param>
    /// <returns>Resultado de la corrección con nota sugerida, justificación y métricas.</returns>
    Task<CorregirRespuestaResponse> CorregirAsync(
        CorregirRespuestaRequest request,
        CancellationToken cancellationToken = default);
}
