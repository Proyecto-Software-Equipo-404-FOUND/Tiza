namespace PEI.IA.Application.DTOs;

/// <summary>
/// Opción para preguntas de tipo OpcionMultiple.
/// </summary>
public class OpcionPreguntaDto
{
    public string Texto { get; set; } = string.Empty;
    public bool EsCorrecta { get; set; }
}

/// <summary>
/// Rúbrica con criterios para preguntas de tipo Abierta.
/// </summary>
public class RubricaPreguntaDto
{
    public List<CriterioRubricaDto> Criterios { get; set; } = new();
}

public class CriterioRubricaDto
{
    public string Descripcion { get; set; } = string.Empty;
    public decimal Puntaje { get; set; }
}

/// <summary>
/// Pregunta sugerida generada por la IA según OpenAPI ia-v1.json.
/// </summary>
public class PreguntaSugeridaDto
{
    public string Enunciado { get; set; } = string.Empty;

    /// <summary>OpcionMultiple o Abierta</summary>
    public string Tipo { get; set; } = "OpcionMultiple";

    public decimal PuntajeMaximo { get; set; } = 10;

    public List<OpcionPreguntaDto>? Opciones { get; set; }

    public RubricaPreguntaDto? Rubrica { get; set; }

    /// <summary>Parte del apunte en la que se basa para que el docente la verifique.</summary>
    public string? FragmentoApunte { get; set; }
}
