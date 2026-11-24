using System.ComponentModel;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;
using PEI.IA.Application.DTOs;

namespace PEI.IA.McpServer.Tools;

/// <summary>
/// Tool MCP para revisar una corrección propuesta por un primer agente y detectar discrepancias pedagógicas o numéricas.
/// </summary>
[McpServerToolType]
public class RevisarCorreccionTool
{
    private readonly ILogger<RevisarCorreccionTool> _logger;

    public RevisarCorreccionTool(ILogger<RevisarCorreccionTool> logger)
    {
        _logger = logger;
    }

    [McpServerTool, Description("Revisa la corrección y nota asignada por un evaluador comparándola con la rúbrica y calcula discrepancias para alertar al docente.")]
    public Task<RevisionCorreccionResultado> Revisar(
        [Description("Enunciado de la consigna evaluada.")] string enunciado,
        [Description("Nota sugerida por el primer agente.")] decimal notaPrimerAgente,
        [Description("Puntaje máximo posible.")] decimal puntajeMaximo,
        [Description("Nota estimada por el revisor.")] decimal notaRevisor,
        [Description("Justificación u observaciones del revisor.")] string observaciones)
    {
        _logger.LogInformation("[MCP Tool] RevisarCorreccion ejecutada: Agente1={Nota1}, Revisor={Nota2}, Max={Max}",
            notaPrimerAgente, notaRevisor, puntajeMaximo);

        var diferencia = Math.Abs(notaPrimerAgente - notaRevisor);
        var umbralDiscrepancia = puntajeMaximo * 0.15m; // 15% de tolerancia
        var requiereRevision = diferencia > umbralDiscrepancia;

        var resultado = new RevisionCorreccionResultado
        {
            EsConsistente = !requiereRevision,
            DiferenciaPuntaje = diferencia,
            NotaSugeridaAgente1 = notaPrimerAgente,
            NotaSugeridaRevisor = notaRevisor,
            Observaciones = observaciones,
            RequiereRevisionDocente = requiereRevision,
            MotivoRevision = requiereRevision
                ? $"Diferencia de {diferencia} puntos entre evaluador y revisor supera el umbral permitido ({umbralDiscrepancia})."
                : null
        };

        return Task.FromResult(resultado);
    }
}
