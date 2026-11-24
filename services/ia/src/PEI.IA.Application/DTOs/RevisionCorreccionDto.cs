namespace PEI.IA.Application.DTOs;

/// <summary>
/// DTO con el resultado de la revisión entre dos agentes o correcciones.
/// </summary>
public class RevisionCorreccionResultado
{
    public bool EsConsistente { get; set; }
    public decimal DiferenciaPuntaje { get; set; }
    public decimal? NotaSugeridaAgente1 { get; set; }
    public decimal? NotaSugeridaRevisor { get; set; }
    public string? Observaciones { get; set; }
    public bool RequiereRevisionDocente { get; set; }
    public string? MotivoRevision { get; set; }
}

public class RevisarCorreccionRequest
{
    public string PreguntaEnunciado { get; set; } = string.Empty;
    public string RespuestaTexto { get; set; } = string.Empty;
    public string RubricaJson { get; set; } = string.Empty;
    public decimal PuntajeMaximo { get; set; }
    public decimal NotaPrimerAgente { get; set; }
    public string JustificacionPrimerAgente { get; set; } = string.Empty;
}
