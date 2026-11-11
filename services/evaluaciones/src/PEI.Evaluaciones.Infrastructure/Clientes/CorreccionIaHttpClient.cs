using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using PEI.Evaluaciones.Application.Correccion;

namespace PEI.Evaluaciones.Infrastructure.Clientes;

// S2-EV-02 (#144): cliente HTTP hacia IA. Contrato: contracts/openapi/ia-v1.json, POST /api/v1/Correcciones.
// La URL base se configura en CorreccionIa:UrlBaseIa (Program.cs).
public sealed class CorreccionIaHttpClient : ICorreccionIaClient
{
    private readonly HttpClient _http;
    private readonly ILogger<CorreccionIaHttpClient> _logger;

    public CorreccionIaHttpClient(HttpClient http, ILogger<CorreccionIaHttpClient> logger)
    {
        _http = http;
        _logger = logger;
    }

    public async Task<bool> SolicitarCorreccionAsync(SolicitudCorreccionIa s, CancellationToken ct)
    {
        // Solo identificadores técnicos: ningún dato del alumno viaja a IA.
        var cuerpo = new
        {
            respuestaTexto = s.Respuesta,
            preguntaEnunciado = s.Enunciado,
            rubricaJson = s.RubricaJson,
            puntajeMaximo = s.PuntajeMaximo,
            institucionId = s.InstitucionId,
            evaluacionId = s.EvaluacionId,
            preguntaId = s.PreguntaId,
            entregaId = s.EntregaId
        };

        try
        {
            using var respuesta = await _http.PostAsJsonAsync("api/v1/Correcciones", cuerpo, ct);
            if (respuesta.IsSuccessStatusCode) return true;

            _logger.LogWarning("IA respondió {Status} al corregir la entrega {EntregaId}, pregunta {PreguntaId}.",
                (int)respuesta.StatusCode, s.EntregaId, s.PreguntaId);
            return false;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            _logger.LogWarning(ex, "No se pudo contactar a IA para la entrega {EntregaId}.", s.EntregaId);
            return false;
        }
    }
}
