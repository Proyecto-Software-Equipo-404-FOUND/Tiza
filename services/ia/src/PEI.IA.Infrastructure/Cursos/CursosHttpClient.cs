using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using PEI.IA.Application.Interfaces;

namespace PEI.IA.Infrastructure.Cursos;

public class CursosSettings
{
    public string BaseUrl { get; set; } = "http://localhost:5102";
    public int TimeoutMs { get; set; } = 15000;
}

public class CursosHttpClient : ICursosClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<CursosHttpClient> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public CursosHttpClient(HttpClient httpClient, ILogger<CursosHttpClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<TextoMaterialCursoDto?> ObtenerTextoMaterialAsync(Guid materialId, CancellationToken ct = default)
    {
        // El token del docente lo agrega ReenviarAuthorizationHandler (en la API de IA):
        // Cursos valida que no sea alumno y que el apunte sea de su institución.
        using var response = await _httpClient.GetAsync($"api/v1/materiales/{materialId}/texto", ct);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            _logger.LogWarning("Material {MaterialId} no encontrado en Cursos (inexistente o de otra institución).", materialId);
            return null;
        }

        if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
        {
            _logger.LogWarning("Cursos rechazó el pedido del texto del material {MaterialId}: {Status}.", materialId, (int)response.StatusCode);
            throw new CursosAccesoDenegadoException((int)response.StatusCode);
        }

        if (!response.IsSuccessStatusCode)
        {
            var content = await response.Content.ReadAsStringAsync(ct);
            _logger.LogWarning("Error {Status} al obtener texto de material {MaterialId}: {Content}",
                response.StatusCode, materialId, content);
            throw new HttpRequestException($"Cursos respondió {(int)response.StatusCode} al pedir el texto del apunte.");
        }

        return await response.Content.ReadFromJsonAsync<TextoMaterialCursoDto>(JsonOptions, ct);
    }
}
