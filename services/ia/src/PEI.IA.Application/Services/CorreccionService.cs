using System.Text.Json;
using Microsoft.Extensions.Logging;
using PEI.IA.Application.DTOs;
using PEI.IA.Application.Interfaces;

namespace PEI.IA.Application.Services;

/// <summary>
/// Implementación del servicio de corrección automatizada.
/// En esta versión inicial (IA2-01) devuelve una respuesta mock.
/// Se completará con la integración real del LLM en IA2-02/IA2-03.
/// </summary>
public class CorreccionService : ICorreccionService
{
    private readonly ILogger<CorreccionService> _logger;

    public CorreccionService(ILogger<CorreccionService> logger)
    {
        _logger = logger;
    }

    public Task<CorregirRespuestaResponse> CorregirAsync(
        CorregirRespuestaRequest request,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Procesando corrección para pregunta: {Enunciado} (PuntajeMax: {PuntajeMax})",
            request.PreguntaEnunciado[..Math.Min(50, request.PreguntaEnunciado.Length)],
            request.PuntajeMaximo);

        // ── Mock: simula una corrección mientras no se integre el LLM real ──
        var notaMock = Math.Round(request.PuntajeMaximo * 0.7m, 1);

        var response = new CorregirRespuestaResponse
        {
            SolicitudId = Guid.NewGuid(),
            NotaSugerida = notaMock,
            Justificacion = "[MOCK] Corrección simulada — se reemplazará con integración LLM real en tareas IA2-02/IA2-03.",
            DesglosePorCriterio = new List<DesgloseCriterio>
            {
                new()
                {
                    Criterio = "Criterio de ejemplo (mock)",
                    PuntajeMaximo = request.PuntajeMaximo,
                    PuntajeOtorgado = notaMock,
                    Comentario = "Evaluación simulada pendiente de integración con el modelo de IA."
                }
            },
            Proveedor = "Mock",
            Modelo = "mock-v1",
            TokensEntrada = 0,
            TokensSalida = 0,
            LatenciaMs = 0
        };

        _logger.LogInformation(
            "Corrección mock completada: NotaSugerida={Nota}/{Max}",
            response.NotaSugerida, request.PuntajeMaximo);

        return Task.FromResult(response);
    }
}
