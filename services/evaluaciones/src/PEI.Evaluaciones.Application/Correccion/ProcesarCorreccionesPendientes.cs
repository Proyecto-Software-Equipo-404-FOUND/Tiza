using MediatR;
using Microsoft.Extensions.Logging;
using PEI.Evaluaciones.Application.Evaluaciones;
using PEI.Evaluaciones.Domain.Entities;

namespace PEI.Evaluaciones.Application.Correccion;

// S2-EV-02 (#144): toma las entregas en PendienteCorreccion y manda cada respuesta abierta a IA.
// Lo dispara el worker cada pocos segundos. Devuelve cuántas entregas procesó.
public sealed record ProcesarCorreccionesPendientesCommand(int Maximo = 10) : IRequest<int>;

public sealed class ProcesarCorreccionesPendientesHandler : IRequestHandler<ProcesarCorreccionesPendientesCommand, int>
{
    private readonly ICorreccionRepositorio _repositorio;
    private readonly ICorreccionIaClient _ia;
    private readonly ILogger<ProcesarCorreccionesPendientesHandler> _logger;

    public ProcesarCorreccionesPendientesHandler(
        ICorreccionRepositorio repositorio,
        ICorreccionIaClient ia,
        ILogger<ProcesarCorreccionesPendientesHandler> logger)
    {
        _repositorio = repositorio;
        _ia = ia;
        _logger = logger;
    }

    public async Task<int> Handle(ProcesarCorreccionesPendientesCommand request, CancellationToken ct)
    {
        var pendientes = await _repositorio.ListarPendientesAsync(request.Maximo, ct);
        foreach (var entrega in pendientes)
        {
            await ProcesarAsync(entrega, ct);
        }
        return pendientes.Count;
    }

    private async Task ProcesarAsync(EntregaExamen entrega, CancellationToken ct)
    {
        var falloIa = false;
        var evaluacion = await _repositorio.ObtenerEvaluacionAsync(entrega.EvaluacionId, entrega.InstitucionId, ct);

        if (evaluacion is null)
        {
            _logger.LogWarning("Entrega {EntregaId}: no se encontró su evaluación; pasa a corrección manual.", entrega.Id);
            falloIa = true;
        }
        else
        {
            var yaCalificadas = (await _repositorio.ListarCalificacionesAsync(entrega.Id, ct))
                .Select(c => c.PreguntaId).ToHashSet();

            foreach (var (pregunta, respuesta) in PreguntasCorreccion.AbiertasRespondidas(evaluacion.PreguntasJson, entrega.RespuestasJson))
            {
                if (yaCalificadas.Contains(pregunta.Id)) continue;

                var ok = await _ia.SolicitarCorreccionAsync(new SolicitudCorreccionIa(
                    entrega.InstitucionId, entrega.EvaluacionId, entrega.Id, pregunta.Id,
                    pregunta.Enunciado, respuesta, pregunta.RubricaJson, pregunta.PuntajeMaximo), ct);

                if (!ok)
                {
                    _logger.LogWarning("Entrega {EntregaId}, pregunta {PreguntaId}: IA no pudo corregir.", entrega.Id, pregunta.Id);
                    falloIa = true;
                    break;
                }
            }
        }

        await CerrarEntregaAsync(entrega.Id, falloIa, ct);
    }

    // Vuelve a leer la entrega: mientras IA corregía, el callback pudo haberla pasado a Corregida.
    private async Task CerrarEntregaAsync(Guid entregaId, bool falloIa, CancellationToken ct)
    {
        var actual = await _repositorio.ObtenerEntregaAsync(entregaId, ct);
        if (actual is null || actual.Estado != EstadoEntregaExamen.PendienteCorreccion) return;

        if (falloIa)
        {
            actual.Estado = EstadoEntregaExamen.CorreccionManual;
        }
        else
        {
            // IA respondió OK a todo pero falta alguna nota: el callback no llegó (por ejemplo,
            // IA con HabilitarIntegracion=false). No se reintenta en bucle: queda para el docente.
            _logger.LogWarning("Entrega {EntregaId}: IA respondió pero no registró todas las notas; pasa a corrección manual.", entregaId);
            actual.Estado = EstadoEntregaExamen.CorreccionManual;
        }

        actual.Version++;
        try
        {
            await _repositorio.GuardarCambiosAsync(ct);
        }
        catch (ConflictoConcurrenciaException)
        {
            // Otra operación (el callback) ya la actualizó: su estado es el que vale.
        }
    }
}
