using System.Text.Json;
using PEI.Evaluaciones.Domain.Entities;

namespace PEI.Evaluaciones.Application.Evaluaciones;

public record EvaluacionDto(
    Guid Id,
    Guid InstitucionId,
    string Titulo,
    string Tipo,
    string Estado,
    Guid? CohorteId,
    JsonElement PreguntasJson,
    DateTimeOffset FechaCreacion,
    DateTimeOffset FechaActualizacion,
    int Version)
{
    public static EvaluacionDto Desde(Evaluacion evaluacion)
    {
        using var documento = JsonDocument.Parse(evaluacion.PreguntasJson);

        return new EvaluacionDto(
            evaluacion.Id,
            evaluacion.InstitucionId,
            evaluacion.Titulo,
            evaluacion.Tipo,
            evaluacion.Estado.ToString(),
            evaluacion.CohorteId,
            documento.RootElement.Clone(),
            evaluacion.FechaCreacion,
            evaluacion.FechaActualizacion,
            evaluacion.Version);
    }
}