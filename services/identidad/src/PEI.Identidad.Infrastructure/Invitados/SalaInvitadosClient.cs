using System.Net;
using System.Net.Http.Json;
using PEI.Identidad.Application.UseCases.Auth.Invitado;

namespace PEI.Identidad.Infrastructure.Invitados;

public sealed class SalaInvitadosOptions
{
    /// <summary>Clave compartida entre servicios (ServiciosInternos:ApiKey, RN-INT-01).</summary>
    public string ApiKey { get; set; } = "";
}

// S3-SA-01 (#231): GET /interno/salas/pin/{pin} de Sala en vivo con X-Api-Key-Interna.
public sealed class SalaInvitadosClient(HttpClient http, SalaInvitadosOptions options) : ISalaInvitadosClient
{
    public async Task<SalaParaInvitado?> ObtenerPorPinAsync(int pin, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"interno/salas/pin/{pin}");
        request.Headers.Add("X-Api-Key-Interna", options.ApiKey);

        using var response = await http.SendAsync(request, ct);
        if (response.StatusCode == HttpStatusCode.NotFound) return null;
        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException($"Sala en vivo respondió {(int)response.StatusCode} al buscar la sala por PIN.");

        var sala = await response.Content.ReadFromJsonAsync<SalaParaInvitado>(cancellationToken: ct);
        return sala ?? throw new HttpRequestException("Sala en vivo devolvió una respuesta vacía.");
    }
}
