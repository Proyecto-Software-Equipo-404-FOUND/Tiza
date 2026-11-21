using MediatR;
using PEI.Evaluaciones.Application.Evaluaciones;
using PEI.Evaluaciones.Domain.Entities;

namespace PEI.Evaluaciones.Application.Evaluaciones.Queries.GetMisExamenes;

public sealed record MiExamenDto(
    Guid EvaluacionId,
    Guid EntregaId,
    Guid? CohorteId,
    string Titulo,
    DateTimeOffset FechaEntregaUtc,
    string Estado,
    decimal? NotaFinal,
    decimal? PuntajeMaximo);

public sealed record GetMisExamenesQuery(
    Guid AlumnoId,
    string? Rol) : IRequest<ResultadoOperacion<IReadOnlyList<MiExamenDto>>>;

public sealed class GetMisExamenesQueryHandler
    : IRequestHandler<GetMisExamenesQuery, ResultadoOperacion<IReadOnlyList<MiExamenDto>>>
{
    private readonly IResultadosRepositorio _repositorio;

    public GetMisExamenesQueryHandler(IResultadosRepositorio repositorio)
    {
        _repositorio = repositorio;
    }

    public async Task<ResultadoOperacion<IReadOnlyList<MiExamenDto>>> Handle(
        GetMisExamenesQuery request,
        CancellationToken ct)
    {
        if (!string.Equals(request.Rol, "Alumno", StringComparison.OrdinalIgnoreCase))
        {
            return ResultadoOperacion<IReadOnlyList<MiExamenDto>>.Invalido(
                "Solo los alumnos tienen \"Mis exámenes\".");
        }

        var filas = await _repositorio.ListarEntregasPorAlumnoAsync(request.AlumnoId, ct);

        var resultado = filas
            .OrderByDescending(x => x.Entrega.FechaEntrega)
            .Select(x =>
            {
                bool estaAprobada = x.Entrega.EstadoNota == EstadoNotaEntrega.Aprobada;

                return new MiExamenDto(
                    EvaluacionId: x.Entrega.EvaluacionId,
                    EntregaId: x.Entrega.Id,
                    CohorteId: x.Evaluacion.CohorteId,
                    Titulo: x.Evaluacion.Titulo,
                    FechaEntregaUtc: x.Entrega.FechaEntrega,
                    Estado: estaAprobada ? "Aprobada" : "PendienteCorreccion",
                    NotaFinal: estaAprobada ? x.Entrega.NotaFinal : null,
                    PuntajeMaximo: estaAprobada ? x.Entrega.PuntajeMaximo : null);
            })
            .ToList();

        return ResultadoOperacion<IReadOnlyList<MiExamenDto>>.Ok(resultado);
    }
}