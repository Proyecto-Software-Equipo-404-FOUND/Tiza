using PEI.BuildingBlocks.Common;

namespace PEI.IA.Domain.Entities;

/// <summary>
/// Representa una calificación sugerida por el modelo de IA registrada a través de la herramienta MCP.
/// </summary>
public class CalificacionSugerida : Entity<Guid>
{
    /// <summary>Nota sugerida por el modelo (escala 0 a puntaje máximo).</summary>
    public decimal NotaSugerida { get; set; }

    /// <summary>Justificación pedagógica detallada de la calificación.</summary>
    public string Justificacion { get; set; } = string.Empty;

    /// <summary>Desglose de puntajes y comentarios por cada criterio de la rúbrica (en formato JSON).</summary>
    public string DesglosePorCriterioJson { get; set; } = "[]";

    /// <summary>Identificador de la entrega evaluada (referencia al microservicio de Evaluaciones, requerido por EV-06).</summary>
    public Guid EntregaId { get; set; }

    /// <summary>Identificador opcional de la pregunta evaluada.</summary>
    public Guid? PreguntaId { get; set; }

    /// <summary>Identificador opcional de la respuesta evaluada.</summary>
    public Guid? RespuestaId { get; set; }

    /// <summary>Momento en que se registró la sugerencia en formato UTC.</summary>
    public DateTime FechaRegistroUtc { get; set; } = DateTime.UtcNow;

    public CalificacionSugerida()
    {
        Id = Guid.NewGuid();
    }
}
