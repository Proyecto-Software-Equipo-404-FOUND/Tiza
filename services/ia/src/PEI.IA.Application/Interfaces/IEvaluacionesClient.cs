namespace PEI.IA.Application.Interfaces;

/// <summary>
/// Abstracción del cliente HTTP hacia el microservicio de Evaluaciones.
/// Permite mockear en tests y desacoplar la infraestructura de la aplicación.
/// </summary>
public interface IEvaluacionesClient
{
    /// <summary>
    /// Envía la calificación sugerida por IA al endpoint interno de Evaluaciones.
    /// </summary>
    /// <param name="entregaId">Identificador de la entrega a calificar.</param>
    /// <param name="notaSugerida">Nota sugerida (debe ser >= 0 y &lt;= puntajeMaximo).</param>
    /// <param name="justificacion">Justificación pedagógica no vacía.</param>
    /// <param name="desgloseJson">Desglose por criterio en JSON (puede ser "[]").</param>
    /// <param name="ct">Token de cancelación.</param>
    /// <returns>
    /// True si Evaluaciones aceptó la sugerencia exitosamente;
    /// false ante cualquier error HTTP o de red (el llamador decide el fallback).
    /// </returns>
    Task<bool> RegistrarSugerenciaAsync(
        Guid entregaId,
        decimal notaSugerida,
        string justificacion,
        string desgloseJson,
        CancellationToken ct = default);
}
