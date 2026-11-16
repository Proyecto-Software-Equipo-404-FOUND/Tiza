using MediatR;
using Microsoft.Extensions.Logging;
using PEI.SalaEnVivo.Application.DTOs.Entregas;
using PEI.SalaEnVivo.Application.DTOs.Salas;
using PEI.SalaEnVivo.Application.Interfaces;
using PEI.SalaEnVivo.Application.Interfaces.Repositories;
using PEI.SalaEnVivo.Application.UseCases.Salas.Commands;
using PEI.SalaEnVivo.Domain.Entities;

namespace PEI.SalaEnVivo.Application.UseCases.Salas.Handlers
{
    public sealed class FinalizarSalaCommandHandler
        : IRequestHandler<FinalizarSalaCommand, ResultadoControlSala<FinalizacionSalaDto>>
    {
        private const string MotivoDocente = "Docente";
        private const string MotivoTiempoAgotado = "TiempoAgotado";
        private const int MaximoEntregasPorLote = 100;

        private readonly ISalaRepository _salaRepository;
        private readonly IParticipanteSalaRepository _participanteRepository;
        private readonly IRespuestaSalaRepository _respuestaRepository;
        private readonly IAuditoriaFocoRepository _auditoriaFocoRepository;
        private readonly IEvaluacionesClient _evaluacionesClient;
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILogger<FinalizarSalaCommandHandler> _logger;

        public FinalizarSalaCommandHandler(
            ISalaRepository salaRepository,
            IParticipanteSalaRepository participanteRepository,
            IRespuestaSalaRepository respuestaRepository,
            IAuditoriaFocoRepository auditoriaFocoRepository,
            IEvaluacionesClient evaluacionesClient,
            IUnitOfWork unitOfWork,
            ILogger<FinalizarSalaCommandHandler> logger)
        {
            _salaRepository = salaRepository;
            _participanteRepository = participanteRepository;
            _respuestaRepository = respuestaRepository;
            _auditoriaFocoRepository = auditoriaFocoRepository;
            _evaluacionesClient = evaluacionesClient;
            _unitOfWork = unitOfWork;
            _logger = logger;
        }

        public async Task<ResultadoControlSala<FinalizacionSalaDto>> Handle(
            FinalizarSalaCommand command,
            CancellationToken cancellationToken)
        {
            var sala = await _salaRepository.ObtenerPorIdAsync(command.SalaId, cancellationToken);

            if (sala == null)
            {
                return ResultadoControlSala<FinalizacionSalaDto>.Error(
                    "SALA_NO_ENCONTRADA",
                    "La sala no existe.");
            }

            if (sala.InstitucionId != command.InstitucionId)
            {
                return ResultadoControlSala<FinalizacionSalaDto>.Error(
                    "NO_AUTORIZADO",
                    "No tienes permiso para finalizar esta sala.");
            }

            var motivo = NormalizarMotivo(command.Motivo);
            var yaEstabaFinalizada = string.Equals(
                sala.Estado,
                "Finalizada",
                StringComparison.OrdinalIgnoreCase);

            var transiciono = sala.Finalizar(motivo);

            if (transiciono)
            {
                await _unitOfWork.SaveChangesAsync(cancellationToken);
                await DespacharEntregasAsync(sala, motivo, cancellationToken);
            }

            return ResultadoControlSala<FinalizacionSalaDto>.Ok(
                new FinalizacionSalaDto(
                    sala.Id,
                    sala.FinalizadaUtc ?? DateTime.UtcNow,
                    sala.MotivoCierre ?? motivo,
                    yaEstabaFinalizada));
        }

        private async Task DespacharEntregasAsync(
            Sala sala,
            string motivo,
            CancellationToken cancellationToken)
        {
            try
            {
                var lote = await CrearLoteEntregasAsync(sala, motivo, cancellationToken);

                var resultado = await _evaluacionesClient.EnviarLoteEntregasAsync(
                    sala.Id,
                    lote,
                    cancellationToken);

                if (resultado != null)
                {
                    _logger.LogInformation(
                        "Lote {LoteId} de la sala {SalaId} entregado a Evaluaciones con {EntregasNuevas} entregas nuevas.",
                        resultado.LoteId,
                        sala.Id,
                        resultado.EntregasNuevas);
                    return;
                }

                await MarcarEntregaPendienteAsync(sala, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Error al despachar las entregas de la sala {SalaId} a Evaluaciones.",
                    sala.Id);

                await MarcarEntregaPendienteAsync(sala, cancellationToken);
            }
        }

        private async Task<LoteEntregasRequestDto> CrearLoteEntregasAsync(
            Sala sala,
            string motivo,
            CancellationToken cancellationToken)
        {
            var participantes = await _participanteRepository.ObtenerPorSalaAsync(
                sala.Id,
                cancellationToken);
            var respuestas = await _respuestaRepository.ObtenerPorSalaAsync(
                sala.Id,
                cancellationToken);
            var eventosFoco = await _auditoriaFocoRepository.ObtenerPorSalaAsync(
                sala.Id,
                cancellationToken);

            var respuestasPorAlumno = respuestas
                .GroupBy(r => r.AlumnoId)
                .ToDictionary(
                    g => g.Key,
                    g => g.OrderBy(r => r.Secuencia).ToList());
            var focoPorAlumno = eventosFoco
                .GroupBy(e => e.AlumnoId)
                .ToDictionary(
                    g => g.Key,
                    g => g.OrderBy(e => e.RegistradoUtc).ToList());

            var alumnos = participantes
                .Select(p => p.AlumnoId)
                .Distinct()
                .OrderBy(id => id)
                .ToList();

            if (alumnos.Count > MaximoEntregasPorLote)
            {
                _logger.LogWarning(
                    "La sala {SalaId} tiene {Cantidad} participantes; el lote se limita a {Maximo} entregas.",
                    sala.Id,
                    alumnos.Count,
                    MaximoEntregasPorLote);

                alumnos = alumnos.Take(MaximoEntregasPorLote).ToList();
            }

            var entregas = alumnos
                .Select(alumnoId => new EntregaAlumnoDto(
                    alumnoId,
                    respuestasPorAlumno.TryGetValue(alumnoId, out var respuestasAlumno)
                        ? respuestasAlumno
                            .Select(r => new RespuestaEntregaDto(
                                r.PreguntaId,
                                r.Respuesta,
                                r.Secuencia,
                                r.RecibidaUtc))
                            .ToList()
                        : new List<RespuestaEntregaDto>(),
                    focoPorAlumno.TryGetValue(alumnoId, out var focoAlumno)
                        ? focoAlumno
                            .Select(f => new AlertaFocoEntregaDto(
                                f.PreguntaId,
                                f.TipoEvento,
                                f.DuracionMs,
                                f.Severidad,
                                f.RegistradoUtc))
                            .ToList()
                        : new List<AlertaFocoEntregaDto>()))
                .ToList();

            return new LoteEntregasRequestDto(
                Version: 1,
                LoteId: Guid.NewGuid(),
                SalaId: sala.Id,
                EvaluacionId: sala.EvaluacionId,
                InstitucionId: sala.InstitucionId,
                FinalizadaUtc: sala.FinalizadaUtc ?? DateTime.UtcNow,
                MotivoCierre: sala.MotivoCierre ?? motivo,
                Entregas: entregas);
        }

        private async Task MarcarEntregaPendienteAsync(
            Sala sala,
            CancellationToken cancellationToken)
        {
            _logger.LogWarning(
                "La sala {SalaId} quedó con entrega pendiente hacia Evaluaciones.",
                sala.Id);

            if (sala.EntregaPendiente)
            {
                return;
            }

            sala.EntregaPendiente = true;

            try
            {
                await _unitOfWork.SaveChangesAsync(cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "No se pudo marcar como pendiente la entrega de la sala {SalaId}.",
                    sala.Id);
            }
        }

        private static string NormalizarMotivo(string? motivo)
        {
            return string.Equals(
                motivo,
                MotivoTiempoAgotado,
                StringComparison.OrdinalIgnoreCase)
                ? MotivoTiempoAgotado
                : MotivoDocente;
        }
    }
}
