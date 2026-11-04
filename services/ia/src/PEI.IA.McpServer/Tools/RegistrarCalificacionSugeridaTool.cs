using System.Collections.Concurrent;
using System.ComponentModel;
using ModelContextProtocol.Server;
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
/// Tool MCP que permite al modelo de IA persistir la calificación sugerida,
/// la justificación pedagógica y el desglose de criterios.
/// </summary>
[McpServerToolType]
public static class RegistrarCalificacionSugeridaTool
{
    private static readonly ConcurrentDictionary<string, CalificacionSugerida> Registros = new();

    /// <summary>
    /// Acceso a los registros en memoria para consultas o aserciones de prueba.
    /// </summary>
    public static IReadOnlyDictionary<string, CalificacionSugerida> ObtenerRegistros() => Registros;

    /// <summary>
    /// Limpia los registros (útil para pruebas unitarias).
    /// </summary>
    public static void Limpiar() => Registros.Clear();

    [McpServerTool, Description("Registra la calificación sugerida por el modelo de IA tras evaluar la respuesta del alumno con la rúbrica.")]
    public static ResultadoRegistroCalificacion RegistrarCalificacion(
        [Description("Identificador único de la entrega a calificar (UUID requerido por EV-06).")] Guid entregaId,
        [Description("Nota numérica sugerida (>= 0 y <= puntajeMaximo).")] decimal notaSugerida,
        [Description("Puntaje máximo permitido según la rúbrica de la evaluación (debe ser > 0).")] decimal puntajeMaximo,
        [Description("Justificación pedagógica que explica la calificación otorgada.")] string justificacion,
        [Description("Desglose detallado por criterio en formato JSON (opcional).")] string? desgloseJson = null)
    {
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

        var calificacion = new CalificacionSugerida
        {
            EntregaId               = entregaId,
            NotaSugerida            = notaSugerida,
            Justificacion           = justificacion.Trim(),
            DesglosePorCriterioJson = string.IsNullOrWhiteSpace(desgloseJson) ? "[]" : desgloseJson.Trim(),
            FechaRegistroUtc        = DateTime.UtcNow
        };

        Registros[calificacion.Id.ToString()] = calificacion;

        return new ResultadoRegistroCalificacion(
            Exito: true,
            Id: calificacion.Id,
            NotaSugerida: calificacion.NotaSugerida,
            Mensaje: $"Calificación {notaSugerida}/{puntajeMaximo} registrada para entrega {entregaId}.",
            Error: null,
            FechaRegistroUtc: calificacion.FechaRegistroUtc);
    }
}
