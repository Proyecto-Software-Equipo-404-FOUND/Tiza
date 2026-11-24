namespace PEI.IA.Application.DTOs;

/// <summary>
/// Solicitud de sugerencia de preguntas a partir de un apunte del curso.
/// Según contrato contracts/openapi/ia-v1.json (SugerirPreguntasRequest).
/// </summary>
public class SugerirPreguntasRequest
{
    /// <summary>Identificador del apunte del curso en estado Listo.</summary>
    public Guid MaterialId { get; set; }

    /// <summary>Cantidad de preguntas a generar (entre 1 y 10, por defecto 5).</summary>
    public int Cantidad { get; set; } = 5;

    /// <summary>Tipos de preguntas solicitadas ('OpcionMultiple', 'Abierta'). Por defecto ambos.</summary>
    public List<string>? Tipos { get; set; }

    /// <summary>Tema opcional para enfocar las preguntas (máximo 200 caracteres).</summary>
    public string? Tema { get; set; }
}
