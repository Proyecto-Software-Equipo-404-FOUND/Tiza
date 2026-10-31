using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using PEI.IA.Application.Interfaces;

namespace PEI.IA.Infrastructure.Evaluaciones;

/// <summary>
/// Cliente HTTP tipado que registra la calificación sugerida por IA
/// en el endpoint interno del microservicio de Evaluaciones (EV-06).
/// Si la integración está deshabilitada (HabilitarIntegracion=false), retorna true
/// sin realizar la llamada HTTP (modo stub para desarrollo local).
/// </summary>
public class EvaluacionesHttpClient : IEvaluacionesClient
{
    private readonly HttpClient _http;
    private readonly EvaluacionesSettings _settings;
    private readonly ILogger<EvaluacionesHttpClient> _logger;

    /// <summary>
    /// Cuerpo del request hacia POST interno/entregas/{entregaId}/calificacion-sugerida
    /// según contrato EV-06.
    /// </summary>
    private sealed record RegistrarSugerenciaRequest(
        decimal NotaSugerida,
        string Justificacion,
        string DesgloseJson);

    public EvaluacionesHttpClient(
        HttpClient http,
        EvaluacionesSettings settings,
        ILogger<EvaluacionesHttpClient> logger)
    {
        _http = http;
        _settings = settings;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<bool> RegistrarSugerenciaAsync(
        Guid entregaId,
        decimal notaSugerida,
        string justificacion,
        string desgloseJson,
        CancellationToken ct = default)
    {
        // Modo stub: S2-EV-02 aún no disponible en el entorno local
        if (!_settings.HabilitarIntegracion)
        {
            _logger.LogInformation(
                "[EvaluacionesHttpClient] Integración deshabilitada (stub). " +
                "Simulando OK para EntregaId={EntregaId}, Nota={Nota}",
                entregaId, notaSugerida);
            return true;
        }

        var url = $"interno/entregas/{entregaId}/calificacion-sugerida";

        _logger.LogInformation(
            "[EvaluacionesHttpClient] POST {Url} | Nota={Nota}",
            url, notaSugerida);

        try
        {
            var body = new RegistrarSugerenciaRequest(notaSugerida, justificacion, desgloseJson);
            var response = await _http.PostAsJsonAsync(url, body, ct);

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation(
                    "[EvaluacionesHttpClient] Sugerencia registrada OK para EntregaId={EntregaId}",
                    entregaId);
                return true;
            }

            var errorBody = await response.Content.ReadAsStringAsync(ct);
            _logger.LogWarning(
                "[EvaluacionesHttpClient] HTTP {Status} al registrar sugerencia para EntregaId={EntregaId}. Body: {Body}",
                (int)response.StatusCode,
                entregaId,
                errorBody[..Math.Min(300, errorBody.Length)]);
            return false;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex,
                "[EvaluacionesHttpClient] No se pudo conectar con Evaluaciones para EntregaId={EntregaId}. " +
                "El flujo de corrección continúa (resiliente).",
                entregaId);
            return false;
        }
        catch (TaskCanceledException ex) when (!ct.IsCancellationRequested)
        {
            _logger.LogWarning(ex,
                "[EvaluacionesHttpClient] Timeout al registrar sugerencia para EntregaId={EntregaId}.",
                entregaId);
            return false;
        }
    }
}
