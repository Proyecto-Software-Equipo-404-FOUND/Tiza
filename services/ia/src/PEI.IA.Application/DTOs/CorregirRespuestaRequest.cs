using System.ComponentModel.DataAnnotations;

namespace PEI.IA.Application.DTOs;

/// <summary>
/// DTO de entrada para solicitar la corrección automatizada de una respuesta abierta.
/// </summary>
public class CorregirRespuestaRequest
{
    /// <summary>Texto de la respuesta del alumno a corregir.</summary>
    [Required(ErrorMessage = "La respuesta del alumno es obligatoria.")]
    [StringLength(10000, ErrorMessage = "La respuesta no puede superar los 10.000 caracteres.")]
    public string RespuestaTexto { get; set; } = string.Empty;

    /// <summary>Enunciado de la pregunta que se está respondiendo.</summary>
    [Required(ErrorMessage = "El enunciado de la pregunta es obligatorio.")]
    [StringLength(2000, ErrorMessage = "El enunciado no puede superar los 2.000 caracteres.")]
    public string PreguntaEnunciado { get; set; } = string.Empty;

    /// <summary>
    /// Rúbrica del docente en formato JSON (array de criterios con descripción y puntaje).
    /// </summary>
    [Required(ErrorMessage = "La rúbrica es obligatoria.")]
    public string RubricaJson { get; set; } = string.Empty;

    /// <summary>Puntaje máximo de la pregunta según la rúbrica.</summary>
    [Range(0.1, 100, ErrorMessage = "El puntaje máximo debe estar entre 0.1 y 100.")]
    public decimal PuntajeMaximo { get; set; }

    /// <summary>Identificador de la institución (extraído del JWT/TenantContext).</summary>
    public Guid? InstitucionId { get; set; }

    /// <summary>Identificador opcional de la evaluación (referencia cruzada).</summary>
    public Guid? EvaluacionId { get; set; }

    /// <summary>Identificador opcional de la pregunta dentro de la evaluación.</summary>
    public Guid? PreguntaId { get; set; }

    /// <summary>Identificador de la entrega en Evaluaciones (para registrar la sugerencia vía S2-EV-02 / EV-06).</summary>
    public Guid? EntregaId { get; set; }
}
