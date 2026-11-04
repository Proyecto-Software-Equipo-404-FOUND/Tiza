using PEI.SalaEnVivo.Application.DTOs.Evaluaciones;
using PEI.SalaEnVivo.Application.DTOs.Salas;

namespace PEI.SalaEnVivo.Application.UseCases.Salas.Handlers
{
    internal static class PreguntaActualMapper
    {
        public static PreguntaActualDto Crear(
            Guid salaId,
            IReadOnlyList<PreguntaEvaluacionDto> preguntas,
            int indice,
            DateTime fechaLimiteUtc)
        {
            var pregunta = preguntas[indice - 1];

            var opciones = pregunta.Opciones?
                .Select(o => new OpcionPreguntaActualDto(o.Id, o.Texto))
                .ToList();

            return new PreguntaActualDto(
                salaId,
                pregunta.Id,
                indice,
                preguntas.Count,
                pregunta.Tipo,
                pregunta.Enunciado,
                opciones,
                pregunta.PuntajeMaximo,
                DateTime.UtcNow,
                fechaLimiteUtc);
        }
    }
}
