namespace PEI.IA.Application.DTOs;

/// <summary>
/// DTO de salida con el resultado de la corrección automatizada.
/// </summary>
public class CorregirRespuestaResponse
{
    /// <summary>Identificador único de la solicitud de corrección.</summary>
    public Guid SolicitudId { get; set; }

    /// <summary>Nota sugerida por el modelo de IA (0 a PuntajeMaximo).</summary>
    public decimal NotaSugerida { get; set; }

    /// <summary>Justificación pedagógica generada por el modelo.</summary>
    public string Justificacion { get; set; } = string.Empty;

    /// <summary>
    /// Desglose detallado de la calificación por cada criterio de la rúbrica.
    /// </summary>
    public List<DesgloseCriterio>? DesglosePorCriterio { get; set; }

    /// <summary>Proveedor de IA utilizado (Groq, OpenRouter, Local).</summary>
    public string Proveedor { get; set; } = string.Empty;

    /// <summary>Modelo específico utilizado.</summary>
    public string Modelo { get; set; } = string.Empty;

    /// <summary>Tokens de entrada consumidos.</summary>
    public int TokensEntrada { get; set; }

    /// <summary>Tokens de salida generados.</summary>
    public int TokensSalida { get; set; }

    /// <summary>Latencia de la llamada al LLM en milisegundos.</summary>
    public long LatenciaMs { get; set; }

    /// <summary>Indica si la corrección requiere revisión del docente.</summary>
    public bool RequiereRevisionDocente { get; set; }

    /// <summary>Indica si se detectó sospecha de engaño o inyección de prompt.</summary>
    public bool SospechaEngano { get; set; }

    /// <summary>Motivo detallado si requiere revisión.</summary>
    public string? MotivoRevision { get; set; }
}

/// <summary>
/// Detalle de la calificación para un criterio individual de la rúbrica.
/// </summary>
public class DesgloseCriterio
{
    /// <summary>Descripción del criterio evaluado.</summary>
    public string Criterio { get; set; } = string.Empty;

    /// <summary>Puntaje máximo del criterio.</summary>
    public decimal PuntajeMaximo { get; set; }

    /// <summary>Puntaje otorgado por la IA.</summary>
    public decimal PuntajeOtorgado { get; set; }

    /// <summary>Comentario específico sobre este criterio.</summary>
    public string Comentario { get; set; } = string.Empty;
}
