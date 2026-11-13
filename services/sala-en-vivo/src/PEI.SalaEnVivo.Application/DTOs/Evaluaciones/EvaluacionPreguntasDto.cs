namespace PEI.SalaEnVivo.Application.DTOs.Evaluaciones
{
    public sealed record EvaluacionPreguntasDto(
        Guid EvaluacionId,
        string Estado,
        IReadOnlyList<PreguntaEvaluacionDto> Preguntas)
    {
        /// <summary>Cohorte a la que se publicó la evaluación (null si no tiene).</summary>
        public Guid? CohorteId { get; init; }
    }
}
