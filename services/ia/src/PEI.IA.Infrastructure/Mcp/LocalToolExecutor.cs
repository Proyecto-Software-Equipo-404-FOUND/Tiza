using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using PEI.IA.Application.DTOs;
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
    private readonly ICursosClient? _cursosClient;
    private readonly ConcurrentDictionary<string, CalificacionRegistrada> _calificaciones = new();

    public LocalToolExecutor(ILogger<LocalToolExecutor> logger, ICursosClient? cursosClient = null)
    {
        _logger = logger;
        _cursosClient = cursosClient;
    }

    public async Task<string> EjecutarAsync(string toolName, string argumentosJson, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("[Plan B - Local] Ejecutando tool '{Tool}' con args: {Args}",
            toolName, argumentosJson[..Math.Min(200, argumentosJson.Length)]);

        return toolName switch
        {
            "RegistrarCalificacionSugerida" => await EjecutarRegistrarCalificacion(argumentosJson),
            "ObtenerTextoApunte" => await EjecutarObtenerTextoApunte(argumentosJson, cancellationToken),
            "RevisarCorreccion" => await EjecutarRevisarCorreccion(argumentosJson),
            "Echo" => JsonSerializer.Serialize(new { resultado = $"[Echo Local] {argumentosJson}" }),
            _ => JsonSerializer.Serialize(new { error = $"Tool '{toolName}' no encontrada en el executor local." })
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
                Nombre = "ObtenerTextoApunte",
                Descripcion = "Devuelve el texto extraído y metadatos de un apunte del curso a partir de su ID.",
                ParametrosSchemaJson = JsonSerializer.Serialize(new
                {
                    type = "object",
                    properties = new
                    {
                        materialId = new { type = "string", format = "uuid", description = "ID del apunte" }
                    },
                    required = new[] { "materialId" }
                })
            },
            new()
            {
                Nombre = "RevisarCorreccion",
                Descripcion = "Revisa la corrección y nota asignada por un evaluador comparándola con la rúbrica.",
                ParametrosSchemaJson = JsonSerializer.Serialize(new
                {
                    type = "object",
                    properties = new
                    {
                        enunciado = new { type = "string" },
                        notaPrimerAgente = new { type = "number" },
                        puntajeMaximo = new { type = "number" },
                        notaRevisor = new { type = "number" },
                        observaciones = new { type = "string" }
                    },
                    required = new[] { "enunciado", "notaPrimerAgente", "puntajeMaximo", "notaRevisor" }
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

    private async Task<string> EjecutarObtenerTextoApunte(string argumentosJson, CancellationToken ct)
    {
        try
        {
            var doc = JsonDocument.Parse(argumentosJson);
            if (!doc.RootElement.TryGetProperty("materialId", out var matIdProp) ||
                !Guid.TryParse(matIdProp.GetString(), out var materialId))
            {
                return JsonSerializer.Serialize(new { exito = false, error = "materialId inválido o ausente." });
            }

            if (_cursosClient != null)
            {
                var material = await _cursosClient.ObtenerTextoMaterialAsync(materialId, ct);
                if (material == null)
                {
                    return JsonSerializer.Serialize(new { exito = false, error = $"Apunte {materialId} no encontrado." });
                }

                return JsonSerializer.Serialize(new
                {
                    exito = true,
                    materialId = material.MaterialId,
                    titulo = material.Titulo,
                    cantidadPaginas = material.CantidadPaginas,
                    texto = material.Texto,
                    tieneTexto = material.TieneTexto
                });
            }

            // Simulación en memoria si CursosClient no está inyectado
            return JsonSerializer.Serialize(new
            {
                exito = true,
                materialId = materialId,
                titulo = "Apunte de prueba simulado",
                cantidadPaginas = 5,
                texto = "Contenido pedagógico simulado sobre programación modular y clean architecture.",
                tieneTexto = true
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Plan B - Local] Error al ejecutar ObtenerTextoApunte");
            return JsonSerializer.Serialize(new { exito = false, error = ex.Message });
        }
    }

    private Task<string> EjecutarRevisarCorreccion(string argumentosJson)
    {
        try
        {
            var doc = JsonDocument.Parse(argumentosJson);
            var root = doc.RootElement;
            var nota1 = root.TryGetProperty("notaPrimerAgente", out var n1) ? n1.GetDecimal() : 0m;
            var nota2 = root.TryGetProperty("notaRevisor", out var n2) ? n2.GetDecimal() : 0m;
            var max = root.TryGetProperty("puntajeMaximo", out var m) ? m.GetDecimal() : 10m;
            var obs = root.TryGetProperty("observaciones", out var o) ? o.GetString() ?? "" : "";

            var diferencia = Math.Abs(nota1 - nota2);
            var umbral = max * 0.15m;
            var requiereRevision = diferencia > umbral;

            var resultado = new RevisionCorreccionResultado
            {
                EsConsistente = !requiereRevision,
                DiferenciaPuntaje = diferencia,
                NotaSugeridaAgente1 = nota1,
                NotaSugeridaRevisor = nota2,
                Observaciones = obs,
                RequiereRevisionDocente = requiereRevision,
                MotivoRevision = requiereRevision
                    ? $"Diferencia de {diferencia} puntos supera el umbral permitido ({umbral})."
                    : null
            };

            return Task.FromResult(JsonSerializer.Serialize(resultado));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Plan B - Local] Error al ejecutar RevisarCorreccion");
            return Task.FromResult(JsonSerializer.Serialize(new { error = ex.Message }));
        }
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
