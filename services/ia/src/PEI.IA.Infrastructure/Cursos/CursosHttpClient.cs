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
        try
        {
            var response = await _httpClient.GetAsync($"api/v1/materiales/{materialId}/texto", ct);
            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                _logger.LogWarning("Material {MaterialId} no encontrado en microservicio Cursos.", materialId);
                return null;
            }

            if (!response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync(ct);
                _logger.LogWarning("Error {Status} al obtener texto de material {MaterialId}: {Content}",
                    response.StatusCode, materialId, content);
                return null;
            }

            return await response.Content.ReadFromJsonAsync<TextoMaterialCursoDto>(JsonOptions, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Excepción al consultar texto del material {MaterialId} en Cursos.", materialId);
            return null;
        }
    }
}
