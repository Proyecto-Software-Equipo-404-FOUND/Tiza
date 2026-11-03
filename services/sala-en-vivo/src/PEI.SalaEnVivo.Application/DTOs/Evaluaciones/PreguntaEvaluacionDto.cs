namespace PEI.SalaEnVivo.Application.DTOs.Evaluaciones
{
    public sealed record PreguntaEvaluacionDto(
        Guid Id,
        string Enunciado,
        string Tipo,
        decimal PuntajeMaximo,
        IReadOnlyList<OpcionEvaluacionDto>? Opciones);
}
