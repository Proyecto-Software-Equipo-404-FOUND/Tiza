using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using PEI.IA.Application.DTOs;
using PEI.IA.Application.Guardrails;
using PEI.IA.Application.Interfaces;
using PEI.IA.Application.Services;
using Xunit;

namespace PEI.IA.UnitTests;

public class AgentesOrchestratorTests
{
    private readonly FakeLlmClient _fakeLlmClient = new();
    private readonly FakeToolExecutor _fakeToolExecutor = new();
    private readonly FakeBarandaSeguridad _fakeBaranda = new();
    private readonly FakeCursosClient _fakeCursosClient = new();
    private readonly AgentesOrchestrator _orchestrator;

    public AgentesOrchestratorTests()
    {
        _orchestrator = new AgentesOrchestrator(
            _fakeLlmClient,
            _fakeToolExecutor,
            _fakeBaranda,
            _fakeCursosClient,
            NullLogger<AgentesOrchestrator>.Instance
        );
    }

    [Fact]
    public async Task GenerarPreguntasAsync_CuandoApunteTieneTexto_GeneraPreguntasYRevisaConAgenteRevisor()
    {
        // Arrange
        var materialId = Guid.NewGuid();
        _fakeToolExecutor.ResultadoEjecutar = JsonSerializer.Serialize(new
        {
            exito = true,
            materialId,
            titulo = "Clean Architecture y Patrones",
            cantidadPaginas = 10,
            texto = "La Clean Architecture promueve la independencia de frameworks y la testeabilidad.",
            tieneTexto = true
        });

        _fakeLlmClient.Resultado = new LlmCompletionResult
        {
            Contenido = JsonSerializer.Serialize(new
            {
                preguntas = new[]
                {
                    new
                    {
                        enunciado = "¿Cuál es el beneficio de la independencia de frameworks en Clean Architecture?",
                        tipo = "OpcionMultiple",
                        puntajeMaximo = 10.0,
                        opciones = new[]
                        {
                            new { texto = "Facilita la evolución y reemplazo de detalles técnicos.", esCorrecta = true },
                            new { texto = "Obliga a usar bases de datos relacionales.", esCorrecta = false }
                        },
                        fragmentoApunte = "La Clean Architecture promueve la independencia de frameworks..."
                    }
                }
            }),
            Proveedor = "OpenRouter",
            Modelo = "test-model"
        };

        var request = new SugerirPreguntasRequest
        {
            MaterialId = materialId,
            Cantidad = 1,
            Tipos = new List<string> { "OpcionMultiple" },
            Tema = "Arquitectura"
        };

        // Act
        var resultado = await _orchestrator.GenerarPreguntasAsync(request);

        // Assert
        Assert.NotNull(resultado);
        Assert.Equal(materialId, resultado.MaterialId);
        Assert.Single(resultado.Preguntas);
        Assert.True(resultado.RevisadaPorAgenteRevisor);
        Assert.Equal("OpcionMultiple", resultado.Preguntas[0].Tipo);
        Assert.Equal("OpenRouter", resultado.Proveedor);
    }

    [Fact]
    public async Task GenerarPreguntasAsync_CuandoApunteNoTieneTexto_LanzaInvalidOperationException()
    {
        // Arrange
        var materialId = Guid.NewGuid();
        _fakeToolExecutor.ResultadoEjecutar = JsonSerializer.Serialize(new
        {
            exito = true,
            materialId,
            titulo = "Escaneo sin OCR",
            cantidadPaginas = 3,
            texto = "",
            tieneTexto = false
        });

        var request = new SugerirPreguntasRequest
        {
            MaterialId = materialId,
            Cantidad = 3
        };

        // Act & Assert
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => _orchestrator.GenerarPreguntasAsync(request));
        Assert.Contains("MATERIAL_SIN_TEXTO", ex.Message);
    }

    [Fact]
    public async Task RevisarCorreccionAsync_DetectaDiscrepanciasYMarcaRevisionDocente()
    {
        // Arrange
        _fakeLlmClient.Resultado = new LlmCompletionResult
        {
            Contenido = JsonSerializer.Serialize(new RevisionCorreccionResultado
            {
                EsConsistente = false,
                DiferenciaPuntaje = 3.5m,
                NotaSugeridaAgente1 = 9.0m,
                NotaSugeridaRevisor = 5.5m,
                Observaciones = "El estudiante omitió la justificación teórica requerida.",
                RequiereRevisionDocente = true,
                MotivoRevision = "Diferencia de 3.5 puntos supera el umbral permitido (1.5)."
            }),
            Proveedor = "Groq",
            Modelo = "llama-3"
        };

        var request = new RevisarCorreccionRequest
        {
            PreguntaEnunciado = "Explique polimorfismo.",
            RespuestaTexto = "Es cuando una cosa hace varias cosas.",
            RubricaJson = "{}",
            PuntajeMaximo = 10m,
            NotaPrimerAgente = 9.0m,
            JustificacionPrimerAgente = "Respuesta sintética adecuada."
        };

        // Act
        var resultado = await _orchestrator.RevisarCorreccionAsync(request);

        // Assert
        Assert.NotNull(resultado);
        Assert.False(resultado.EsConsistente);
        Assert.True(resultado.RequiereRevisionDocente);
        Assert.Equal(3.5m, resultado.DiferenciaPuntaje);
    }

    private sealed class FakeLlmClient : ILlmClient
    {
        public LlmCompletionResult Resultado { get; set; } = new() { Contenido = "{}" };

        public Task<LlmCompletionResult> CompletarAsync(
            string systemPrompt,
            string userPrompt,
            IReadOnlyList<LlmToolDefinition>? tools = null,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Resultado);
        }
    }

    private sealed class FakeToolExecutor : IToolExecutor
    {
        public string ResultadoEjecutar { get; set; } = "{}";

        public Task<string> EjecutarAsync(string toolName, string argumentosJson, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(ResultadoEjecutar);
        }

        public Task<IReadOnlyList<LlmToolDefinition>> ListarToolsAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<LlmToolDefinition>>(new List<LlmToolDefinition>());
        }
    }

    private sealed class FakeBarandaSeguridad : IBarandaSeguridad
    {
        public ResultadoSeguridadEntrada EvaluarEntrada(string? respuestaAlumno) => new(false, null);

        public ResultadoValidacionSalida ValidarSalida(
            decimal? notaSugerida,
            decimal puntajeMaximo,
            string? justificacion,
            IReadOnlyList<DesgloseCriterio>? desglose) => new(true, null);
    }

    private sealed class FakeCursosClient : ICursosClient
    {
        public Task<TextoMaterialCursoDto?> ObtenerTextoMaterialAsync(Guid materialId, CancellationToken ct = default)
        {
            return Task.FromResult<TextoMaterialCursoDto?>(new TextoMaterialCursoDto(
                materialId, "Titulo", 1, "Texto apunte", true));
        }
    }
}
