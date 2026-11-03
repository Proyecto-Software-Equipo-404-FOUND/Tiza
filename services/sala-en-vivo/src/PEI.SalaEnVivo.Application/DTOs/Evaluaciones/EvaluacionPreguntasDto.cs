namespace PEI.SalaEnVivo.Application.DTOs.Evaluaciones
{
    public sealed record EvaluacionPreguntasDto(
        Guid EvaluacionId,
        string Estado,
        IReadOnlyList<PreguntaEvaluacionDto> Preguntas);
}
