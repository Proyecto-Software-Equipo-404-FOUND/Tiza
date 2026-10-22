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
    DateTime? FechaRegistroUtc);

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
        [Description("Nota numérica sugerida (mayor o igual a cero).")] decimal notaSugerida,
        [Description("Justificación pedagógica que explica la calificación otorgada.")] string justificacion,
        [Description("Desglose detallado por criterio en formato JSON (opcional).")] string? desgloseJson = null)
    {
        if (notaSugerida < 0)
        {
            return new ResultadoRegistroCalificacion(
                Exito: false,
                Id: null,
                NotaSugerida: null,
                Mensaje: null,
                Error: "La nota sugerida no puede ser negativa.",
                FechaRegistroUtc: null);
        }

        if (string.IsNullOrWhiteSpace(justificacion))
        {
            return new ResultadoRegistroCalificacion(
                Exito: false,
                Id: null,
                NotaSugerida: null,
                Mensaje: null,
                Error: "La justificación pedagógica es obligatoria y no puede estar vacía.",
                FechaRegistroUtc: null);
        }

        var calificacion = new CalificacionSugerida
        {
            NotaSugerida = notaSugerida,
            Justificacion = justificacion.Trim(),
            DesglosePorCriterioJson = string.IsNullOrWhiteSpace(desgloseJson) ? "[]" : desgloseJson.Trim(),
            FechaRegistroUtc = DateTime.UtcNow
        };

        Registros[calificacion.Id.ToString()] = calificacion;

        return new ResultadoRegistroCalificacion(
            Exito: true,
            Id: calificacion.Id,
            NotaSugerida: calificacion.NotaSugerida,
            Mensaje: $"Calificación de {notaSugerida} puntos registrada correctamente.",
            Error: null,
            FechaRegistroUtc: calificacion.FechaRegistroUtc);
    }
}
