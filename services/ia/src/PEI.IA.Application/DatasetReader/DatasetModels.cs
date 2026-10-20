namespace PEI.IA.Application.DatasetReader;

/// <summary>
/// Modelo raíz del dataset de corrección pedagógica.
/// </summary>
public class DatasetCorreccion
{
    public int Version { get; set; }
    public string Descripcion { get; set; } = string.Empty;
    public string FechaCreacion { get; set; } = string.Empty;
    public string Autor { get; set; } = string.Empty;
    public List<PreguntaDataset> Preguntas { get; set; } = new();
}

/// <summary>
/// Representa una pregunta abierta del dataset de evaluación.
/// </summary>
public class PreguntaDataset
{
    public string Id { get; set; } = string.Empty;
    public string Enunciado { get; set; } = string.Empty;
    public string Tipo { get; set; } = "Abierta";
    public decimal PuntajeMaximo { get; set; }
    public RubricaDataset Rubrica { get; set; } = new();
    public List<RespuestaDataset> Respuestas { get; set; } = new();
}

/// <summary>
/// Estructura de la rúbrica asociada a una pregunta.
/// </summary>
public class RubricaDataset
{
    public List<CriterioDataset> Criterios { get; set; } = new();
}

/// <summary>
/// Criterio individual de evaluación en la rúbrica.
/// </summary>
public class CriterioDataset
{
    public string Descripcion { get; set; } = string.Empty;
    public decimal Puntaje { get; set; }
}

/// <summary>
/// Respuesta de prueba entregada por un estudiante en el dataset.
/// </summary>
public class RespuestaDataset
{
    public string Id { get; set; } = string.Empty;
    public string Texto { get; set; } = string.Empty;
    public decimal NotaEsperada { get; set; }
    public string JustificacionEsperada { get; set; } = string.Empty;
}
