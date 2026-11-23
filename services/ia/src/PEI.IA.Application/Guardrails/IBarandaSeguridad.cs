namespace PEI.IA.Application.Guardrails;

/// <summary>
/// Resultado del análisis de seguridad sobre la entrada enviada por el estudiante.
/// </summary>
public record ResultadoSeguridadEntrada(
    bool EsSospechosa,
    string? Motivo);

/// <summary>
/// Resultado de la validación estructural y numérica de la corrección generada por el LLM.
/// </summary>
public record ResultadoValidacionSalida(
    bool EsValido,
    string? ErrorValidacion);

/// <summary>
/// Contrato del servicio de barandas para protección contra inyección de prompt
/// y validación determinística de respuestas del LLM.
/// </summary>
public interface IBarandaSeguridad
{
    /// <summary>
    /// Analiza la respuesta del estudiante en busca de intentos de manipulación o inyección de prompt.
    /// </summary>
    ResultadoSeguridadEntrada EvaluarEntrada(string? respuestaAlumno);

    /// <summary>
    /// Valida que el resultado parseado cumpla con las reglas de negocio, tipos y rangos de puntaje.
    /// </summary>
    ResultadoValidacionSalida ValidarSalida(
        decimal? notaSugerida,
        decimal puntajeMaximo,
        string? justificacion,
        IReadOnlyList<DTOs.DesgloseCriterio>? desglose);
}
