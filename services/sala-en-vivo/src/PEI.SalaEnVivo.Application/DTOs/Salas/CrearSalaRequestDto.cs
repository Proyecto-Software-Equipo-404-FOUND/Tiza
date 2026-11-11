namespace PEI.SalaEnVivo.Application.DTOs.Salas
{
    /// <summary>
    /// Payload HTTP para la solicitud de creación de sala según OpenAPI sala-en-vivo-v1.
    /// El identificador de institución no se recibe en el cuerpo; se extrae del JWT del docente.
    /// </summary>
    public sealed record CrearSalaRequestDto(
        Guid EvaluacionId,
        int CapacidadMaxima = 40);
}
