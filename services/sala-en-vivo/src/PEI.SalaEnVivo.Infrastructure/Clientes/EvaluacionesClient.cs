using System.Net;
using System.Text.Json;
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

        public EvaluacionesClient(HttpClient httpClient)
        {
            _httpClient = httpClient;
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
