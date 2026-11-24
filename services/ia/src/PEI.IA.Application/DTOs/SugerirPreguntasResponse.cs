namespace PEI.IA.Application.DTOs;

/// <summary>
/// Respuesta devuelta por el endpoint de sugerencia de preguntas (SugerirPreguntasResponse).
/// </summary>
public class SugerirPreguntasResponse
{
    public Guid MaterialId { get; set; }
    public List<PreguntaSugeridaDto> Preguntas { get; set; } = new();
    public bool RevisadaPorAgenteRevisor { get; set; }
    public string Proveedor { get; set; } = string.Empty;
    public string Modelo { get; set; } = string.Empty;
}
