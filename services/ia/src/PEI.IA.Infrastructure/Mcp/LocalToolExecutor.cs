using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using PEI.IA.Application.Interfaces;

namespace PEI.IA.Infrastructure.Mcp;

/// <summary>
/// Plan B: ejecutor de tools local que simula la funcionalidad de MCP
/// sin necesidad de un servidor MCP externo. Útil como fallback si
/// el transporte MCP presenta problemas o para desarrollo local.
/// </summary>
public class LocalToolExecutor : IToolExecutor
{
    private readonly ILogger<LocalToolExecutor> _logger;
    private readonly ConcurrentDictionary<string, CalificacionRegistrada> _calificaciones = new();

    public LocalToolExecutor(ILogger<LocalToolExecutor> logger)
    {
        _logger = logger;
    }

    public Task<string> EjecutarAsync(string toolName, string argumentosJson, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("[Plan B - Local] Ejecutando tool '{Tool}' con args: {Args}",
            toolName, argumentosJson[..Math.Min(200, argumentosJson.Length)]);

        return toolName switch
        {
            "RegistrarCalificacionSugerida" => EjecutarRegistrarCalificacion(argumentosJson),
            "Echo" => Task.FromResult(JsonSerializer.Serialize(new { resultado = $"[Echo Local] {argumentosJson}" })),
            _ => Task.FromResult(JsonSerializer.Serialize(new { error = $"Tool '{toolName}' no encontrada en el executor local." }))
        };
    }

    public Task<IReadOnlyList<LlmToolDefinition>> ListarToolsAsync(CancellationToken cancellationToken = default)
    {
        var tools = new List<LlmToolDefinition>
        {
            new()
            {
                Nombre = "RegistrarCalificacionSugerida",
                Descripcion = "Registra la calificación sugerida por la IA junto con la justificación pedagógica y el desglose por criterio de rúbrica.",
                ParametrosSchemaJson = JsonSerializer.Serialize(new
                {
                    type = "object",
                    properties = new
                    {
                        notaSugerida = new { type = "number", description = "Nota sugerida (0 a puntajeMaximo)" },
                        justificacion = new { type = "string", description = "Justificación pedagógica de la nota" },
                        desglosePorCriterio = new
                        {
                            type = "array",
                            description = "Desglose detallado por criterio de rúbrica",
                            items = new
                            {
                                type = "object",
                                properties = new
                                {
                                    criterio = new { type = "string" },
                                    puntajeMaximo = new { type = "number" },
                                    puntajeOtorgado = new { type = "number" },
                                    comentario = new { type = "string" }
                                }
                            }
                        }
                    },
                    required = new[] { "notaSugerida", "justificacion" }
                })
            },
            new()
            {
                Nombre = "Echo",
                Descripcion = "Tool de prueba que devuelve el texto recibido.",
                ParametrosSchemaJson = "{\"type\":\"object\",\"properties\":{\"texto\":{\"type\":\"string\"}},\"required\":[\"texto\"]}"
            }
        };

        return Task.FromResult<IReadOnlyList<LlmToolDefinition>>(tools);
    }

    private Task<string> EjecutarRegistrarCalificacion(string argumentosJson)
    {
        try
        {
            var args = JsonSerializer.Deserialize<RegistrarCalificacionArgs>(argumentosJson, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (args is null)
                return Task.FromResult(JsonSerializer.Serialize(new { error = "No se pudieron deserializar los argumentos." }));

            if (string.IsNullOrWhiteSpace(args.Justificacion))
                return Task.FromResult(JsonSerializer.Serialize(new { error = "La justificación no puede estar vacía." }));

            if (args.NotaSugerida < 0)
                return Task.FromResult(JsonSerializer.Serialize(new { error = "La nota sugerida no puede ser negativa." }));

            var registro = new CalificacionRegistrada
            {
                Id = Guid.NewGuid().ToString(),
                NotaSugerida = args.NotaSugerida,
                Justificacion = args.Justificacion,
                DesglosePorCriterio = args.DesglosePorCriterio,
                FechaRegistroUtc = DateTime.UtcNow
            };

            _calificaciones[registro.Id] = registro;

            _logger.LogInformation("[Plan B - Local] Calificación registrada: {Id}, Nota: {Nota}",
                registro.Id, registro.NotaSugerida);

            return Task.FromResult(JsonSerializer.Serialize(new
            {
                exito = true,
                registroId = registro.Id,
                mensaje = $"Calificación {registro.NotaSugerida} registrada exitosamente."
            }));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Plan B - Local] Error al registrar calificación");
            return Task.FromResult(JsonSerializer.Serialize(new { error = ex.Message }));
        }
    }
}

internal class RegistrarCalificacionArgs
{
    public decimal NotaSugerida { get; set; }
    public string Justificacion { get; set; } = string.Empty;
    public List<DesgloseCriterioArgs>? DesglosePorCriterio { get; set; }
}

internal class DesgloseCriterioArgs
{
    public string Criterio { get; set; } = string.Empty;
    public decimal PuntajeMaximo { get; set; }
    public decimal PuntajeOtorgado { get; set; }
    public string Comentario { get; set; } = string.Empty;
}

internal class CalificacionRegistrada
{
    public string Id { get; set; } = string.Empty;
    public decimal NotaSugerida { get; set; }
    public string Justificacion { get; set; } = string.Empty;
    public List<DesgloseCriterioArgs>? DesglosePorCriterio { get; set; }
    public DateTime FechaRegistroUtc { get; set; }
}
