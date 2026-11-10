using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using PEI.SalaEnVivo.Application.DTOs.Entregas;
using PEI.SalaEnVivo.Application.DTOs.Evaluaciones;
using PEI.SalaEnVivo.Application.Interfaces;

namespace PEI.SalaEnVivo.Infrastructure.Clientes
{
    public sealed class EvaluacionesClient : IEvaluacionesClient
    {
        private const string EncabezadoInstitucion = "X-Institucion-Id";

        private static readonly JsonSerializerOptions OpcionesJson =
            new(JsonSerializerDefaults.Web);

        private readonly HttpClient _httpClient;
        private readonly ILogger<EvaluacionesClient>? _logger;

        public EvaluacionesClient(
            HttpClient httpClient,
            ILogger<EvaluacionesClient>? logger = null)
        {
            _httpClient = httpClient;
            _logger = logger;
        }

        public async Task<EvaluacionPreguntasDto?> ObtenerPreguntasAsync(
            Guid evaluacionId,
            Guid institucionId,
            CancellationToken cancellationToken = default)
        {
            using var request = new HttpRequestMessage(
                HttpMethod.Get,
                $"api/v1/evaluaciones/{evaluacionId}");

            request.Headers.Add(EncabezadoInstitucion, institucionId.ToString());

            using var response = await _httpClient.SendAsync(request, cancellationToken);

            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return null;
            }

            response.EnsureSuccessStatusCode();

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);

            var evaluacion = await JsonSerializer.DeserializeAsync<EvaluacionApiDto>(
                stream,
                OpcionesJson,
                cancellationToken);

            if (evaluacion == null)
            {
                return null;
            }

            var preguntas = evaluacion.PreguntasJson?.Preguntas ?? new List<PreguntaApiDto>();

            return new EvaluacionPreguntasDto(
                evaluacion.Id,
                evaluacion.Estado ?? string.Empty,
                preguntas
                    .Select(p => new PreguntaEvaluacionDto(
                        p.Id,
                        p.Enunciado ?? string.Empty,
                        p.Tipo ?? string.Empty,
                        p.PuntajeMaximo,
                        p.Opciones?
                            .Select(o => new OpcionEvaluacionDto(o.Id, o.Texto ?? string.Empty))
                            .ToList()))
                    .ToList());
        }

        public async Task<ResultadoLoteEntregasDto?> EnviarLoteEntregasAsync(
            Guid salaId,
            LoteEntregasRequestDto lote,
            CancellationToken cancellationToken = default)
        {
            const int maxIntentos = 3;
            var ruta = $"interno/salas/{salaId}/entregas";

            for (int intento = 1; intento <= maxIntentos; intento++)
            {
                try
                {
                    using var request = new HttpRequestMessage(HttpMethod.Post, ruta)
                    {
                        Content = JsonContent.Create(lote, options: OpcionesJson)
                    };
                    request.Headers.Add(EncabezadoInstitucion, lote.InstitucionId.ToString());

                    using var response = await _httpClient.SendAsync(request, cancellationToken);

                    if (response.IsSuccessStatusCode)
                    {
                        var resultado = await response.Content.ReadFromJsonAsync<ResultadoLoteEntregasDto>(
                            OpcionesJson,
                            cancellationToken);

                        _logger?.LogInformation(
                            "Lote de entregas {LoteId} de la sala {SalaId} enviado exitosamente a Evaluaciones.",
                            lote.LoteId,
                            salaId);

                        return resultado;
                    }

                    var statusCode = (int)response.StatusCode;

                    // Si es un error 4xx de cliente (ej. 400, 404), no se reintenta según contrato
                    if (statusCode is >= 400 and < 500)
                    {
                        var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
                        _logger?.LogWarning(
                            "Evaluaciones rechazó el lote {LoteId} con estado {StatusCode}: {ErrorBody}. No se reintenta.",
                            lote.LoteId,
                            statusCode,
                            errorBody);
                        return null;
                    }

                    _logger?.LogWarning(
                        "Intento {Intento}/{MaxIntentos} falló al enviar lote {LoteId} (HTTP {StatusCode}).",
                        intento,
                        maxIntentos,
                        statusCode);
                }
                catch (HttpRequestException ex) when (intento < maxIntentos)
                {
                    _logger?.LogWarning(
                        ex,
                        "Intento {Intento}/{MaxIntentos} falló por error de red al enviar lote {LoteId}.",
                        intento,
                        maxIntentos,
                        lote.LoteId);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning(
                        ex,
                        "Error inesperado en intento {Intento}/{MaxIntentos} enviando lote {LoteId}.",
                        intento,
                        maxIntentos,
                        lote.LoteId);
                }

                if (intento < maxIntentos)
                {
                    await Task.Delay(TimeSpan.FromMilliseconds(150 * intento), cancellationToken);
                }
            }

            _logger?.LogError(
                "No se pudo entregar el lote {LoteId} de la sala {SalaId} tras {MaxIntentos} intentos. Se registrará entrega pendiente.",
                lote.LoteId,
                salaId,
                maxIntentos);

            return null;
        }

        private sealed record EvaluacionApiDto(
            Guid Id,
            string? Estado,
            PreguntasJsonApiDto? PreguntasJson);

        private sealed record PreguntasJsonApiDto(
            List<PreguntaApiDto>? Preguntas);

        private sealed record PreguntaApiDto(
            Guid Id,
            string? Enunciado,
            string? Tipo,
            decimal PuntajeMaximo,
            List<OpcionApiDto>? Opciones);

        private sealed record OpcionApiDto(
            Guid Id,
            string? Texto);
    }
}
