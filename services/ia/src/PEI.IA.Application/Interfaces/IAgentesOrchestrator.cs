using PEI.IA.Application.DTOs;

namespace PEI.IA.Application.Interfaces;

/// <summary>
/// Orquestador de agentes de IA (Generador de Preguntas y Revisor de Corrección).
/// Coordina la obtención de contexto mediante MCP, ejecución de LLM y validaciones con barandas.
/// </summary>
public interface IAgentesOrchestrator
{
    /// <summary>
    /// Orquesta la generación de preguntas de evaluación sugeridas a partir del texto de un apunte.
    /// Valida que el apunte tenga texto, aplica barandas anti-manipulación y ejecuta al agente revisor.
    /// </summary>
    Task<SugerirPreguntasResponse> GenerarPreguntasAsync(
        SugerirPreguntasRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Orquesta la revisión pedagógica de una corrección entre dos agentes.
    /// </summary>
    Task<RevisionCorreccionResultado> RevisarCorreccionAsync(
        RevisarCorreccionRequest request,
        CancellationToken cancellationToken = default);
}
