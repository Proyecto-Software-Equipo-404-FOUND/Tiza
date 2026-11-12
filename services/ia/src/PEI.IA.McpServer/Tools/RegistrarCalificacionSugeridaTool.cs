using System.ComponentModel;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;
using PEI.IA.Application.Interfaces;
using PEI.IA.Domain.Entities;

namespace PEI.IA.McpServer.Tools;

/// <summary>
/// Resultado tipado devuelto por la tool MCP de calificación sugerida.
/// </summary>
public record ResultadoRegistroCalificacion(
    bool Exito,
    Guid? Id,
    decimal? NotaSugerida,
    string? Mensaje,
    string? Error,
    DateTime? FechaRegistroUtc)
{
    /// <summary>Crea un resultado de error de forma concisa.</summary>
    public static ResultadoRegistroCalificacion Fallido(string mensaje) =>
        new(false, null, null, null, mensaje, null);
}

/// <summary>
/// Tool MCP que permite al modelo de IA persistir la calificación sugerida
/// en el microservicio de Evaluaciones (S2-IA-03).
/// El callback hacia Evaluaciones ocurre aquí, antes de retornar, para que
/// el worker de Evaluaciones interprete el 200 OK de IA como confirmación
/// de que la nota ya quedó registrada.
/// </summary>
[McpServerToolType]
public class RegistrarCalificacionSugeridaTool
{
    private readonly IEvaluacionesClient _evaluacionesClient;
    private readonly ILogger<RegistrarCalificacionSugeridaTool> _logger;

    public RegistrarCalificacionSugeridaTool(
        IEvaluacionesClient evaluacionesClient,
        ILogger<RegistrarCalificacionSugeridaTool> logger)
    {
        _evaluacionesClient = evaluacionesClient;
        _logger = logger;
    }

    /// <summary>
    /// Registra la calificación sugerida por la IA y la persiste en Evaluaciones
    /// vía el callback interno antes de retornar la respuesta al LLM.
    /// </summary>
    [McpServerTool, Description("Registra la calificación sugerida por el modelo de IA tras evaluar la respuesta del alumno con la rúbrica. Persiste la nota en Evaluaciones antes de retornar.")]
    public async Task<ResultadoRegistroCalificacion> RegistrarCalificacion(
        [Description("Identificador único de la entrega a calificar (UUID requerido por EV-06).")] Guid entregaId,
        [Description("Identificador de la pregunta dentro de la entrega (obligatorio para evitar que notas de preguntas distintas se pisen).")] Guid preguntaId,
        [Description("Nota numérica sugerida (>= 0 y <= puntajeMaximo).")] decimal notaSugerida,
        [Description("Puntaje máximo permitido según la rúbrica de la evaluación (debe ser > 0).")] decimal puntajeMaximo,
        [Description("Justificación pedagógica que explica la calificación otorgada.")] string justificacion,
        [Description("Desglose detallado por criterio en formato JSON (opcional).")] string? desgloseJson = null)
    {
        // ── Validaciones de dominio ──
        if (notaSugerida < 0)
            return ResultadoRegistroCalificacion.Fallido("La nota sugerida no puede ser negativa.");

        if (puntajeMaximo <= 0)
            return ResultadoRegistroCalificacion.Fallido("El puntaje máximo debe ser mayor que cero.");

        if (notaSugerida > puntajeMaximo)
            return ResultadoRegistroCalificacion.Fallido(
                $"La nota sugerida ({notaSugerida}) excede el puntaje máximo de la rúbrica ({puntajeMaximo}).");

        if (string.IsNullOrWhiteSpace(justificacion))
            return ResultadoRegistroCalificacion.Fallido(
                "La justificación pedagógica es obligatoria y no puede estar vacía.");

        if (justificacion.Length > 4000)
            return ResultadoRegistroCalificacion.Fallido(
                "La justificación no puede superar los 4000 caracteres.");

        var desgloseNormalizado = string.IsNullOrWhiteSpace(desgloseJson) ? "[]" : desgloseJson.Trim();

        // ── Callback hacia Evaluaciones (ANTES de retornar — crítico) ──
        // El worker de Evaluaciones interpreta el 200 OK de IA como confirmación de nota registrada.
        // Si este callback falla, el worker marcará la entrega como CorreccionManual.
        _logger.LogInformation(
            "[RegistrarCalificacionSugeridaTool] Callback hacia Evaluaciones: EntregaId={EntregaId}, PreguntaId={PreguntaId}, Nota={Nota}/{Max}",
            entregaId, preguntaId, notaSugerida, puntajeMaximo);

        bool callbackOk;
        try
        {
            callbackOk = await _evaluacionesClient.RegistrarSugerenciaAsync(
                entregaId: entregaId,
                notaSugerida: notaSugerida,
                justificacion: justificacion.Trim(),
                desgloseJson: desgloseNormalizado,
                preguntaId: preguntaId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "[RegistrarCalificacionSugeridaTool] Excepción al llamar callback de Evaluaciones. EntregaId={EntregaId}",
                entregaId);
            callbackOk = false;
        }

        if (!callbackOk)
        {
            _logger.LogWarning(
                "[RegistrarCalificacionSugeridaTool] Callback fallido para EntregaId={EntregaId}, PreguntaId={PreguntaId}. " +
                "Evaluaciones marcará la entrega como CorreccionManual.",
                entregaId, preguntaId);

            return ResultadoRegistroCalificacion.Fallido(
                $"No se pudo registrar la calificación en Evaluaciones para la entrega {entregaId} " +
                $"(pregunta {preguntaId}). La entrega pasará a corrección manual.");
        }

        // ── Registro local de auditoría ──
        var calificacion = new CalificacionSugerida
        {
            EntregaId               = entregaId,
            NotaSugerida            = notaSugerida,
            Justificacion           = justificacion.Trim(),
            DesglosePorCriterioJson = desgloseNormalizado,
            FechaRegistroUtc        = DateTime.UtcNow
        };

        _logger.LogInformation(
            "[RegistrarCalificacionSugeridaTool] Calificación persistida OK. Id={Id}, Nota={Nota}/{Max}",
            calificacion.Id, notaSugerida, puntajeMaximo);

        return new ResultadoRegistroCalificacion(
            Exito: true,
            Id: calificacion.Id,
            NotaSugerida: calificacion.NotaSugerida,
            Mensaje: $"Calificación {notaSugerida}/{puntajeMaximo} registrada para entrega {entregaId} (pregunta {preguntaId}).",
            Error: null,
            FechaRegistroUtc: calificacion.FechaRegistroUtc);
    }
}

