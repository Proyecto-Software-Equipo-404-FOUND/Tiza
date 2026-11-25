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

        _fakeCursosClient.Material = new TextoMaterialCursoDto(materialId, "Escaneo sin OCR", 3, "", false);

        var request = new SugerirPreguntasRequest
        {
            MaterialId = materialId,
            Cantidad = 3
        };

        // Act & Assert
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => _orchestrator.GenerarPreguntasAsync(request));
        Assert.Contains("MATERIAL_SIN_TEXTO", ex.Message);
        Assert.Equal(0, _fakeLlmClient.Llamadas);
    }

    // ── S3-IA-02: arreglos de la revisión (#281) ──

    [Fact]
    public async Task GenerarPreguntasAsync_ApunteInexistenteEnCursos_LanzaKeyNotFoundSinLlamarAlModelo()
    {
        _fakeCursosClient.DevolverNull = true;

        await Assert.ThrowsAsync<KeyNotFoundException>(() => _orchestrator.GenerarPreguntasAsync(
            new SugerirPreguntasRequest { MaterialId = Guid.NewGuid(), Cantidad = 2 }));
        Assert.Equal(0, _fakeLlmClient.Llamadas);
    }

    [Theory]
    [InlineData(401)]
    [InlineData(403)]
    public async Task GenerarPreguntasAsync_CursosRechazaElToken_PropagaElErrorSinLlamarAlModelo(int status)
    {
        _fakeCursosClient.Lanzar = new CursosAccesoDenegadoException(status);

        var ex = await Assert.ThrowsAsync<CursosAccesoDenegadoException>(() => _orchestrator.GenerarPreguntasAsync(
            new SugerirPreguntasRequest { MaterialId = Guid.NewGuid(), Cantidad = 2 }));
        Assert.Equal(status, ex.StatusCode);
        Assert.Equal(0, _fakeLlmClient.Llamadas);
    }

    [Fact]
    public async Task GenerarPreguntasAsync_ModeloDevuelveJsonInvalido_LanzaIaNoDisponibleSinInventarPreguntas()
    {
        _fakeLlmClient.Resultado = new LlmCompletionResult { Contenido = "no es json", Proveedor = "x", Modelo = "y" };

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => _orchestrator.GenerarPreguntasAsync(
            new SugerirPreguntasRequest { MaterialId = Guid.NewGuid(), Cantidad = 3 }));
        Assert.Contains("IA_NO_DISPONIBLE", ex.Message);
    }

    [Fact]
    public async Task GenerarPreguntasAsync_RevisorDescartaLasInvalidasYNoLasCompleta()
    {
        _fakeLlmClient.Resultado = new LlmCompletionResult
        {
            Contenido = JsonSerializer.Serialize(new
            {
                preguntas = new object[]
                {
                    // inválida: dos correctas
                    new { enunciado = "OM con dos correctas", tipo = "OpcionMultiple", puntajeMaximo = 2.0,
                          opciones = new[] { new { texto = "A", esCorrecta = true }, new { texto = "B", esCorrecta = true } } },
                    // inválida: abierta sin rúbrica
                    new { enunciado = "Abierta sin rúbrica", tipo = "Abierta", puntajeMaximo = 5.0 },
                    // válida
                    new { enunciado = "Explicá la regla de dependencias.", tipo = "Abierta", puntajeMaximo = 5.0,
                          rubrica = new { criterios = new[] { new { descripcion = "Define", puntaje = 3.0 }, new { descripcion = "Ejemplifica", puntaje = 2.0 } } } }
                }
            }),
            Proveedor = "OpenRouter",
            Modelo = "test-model"
        };

        var resultado = await _orchestrator.GenerarPreguntasAsync(
            new SugerirPreguntasRequest { MaterialId = Guid.NewGuid(), Cantidad = 3 });

        var pregunta = Assert.Single(resultado.Preguntas);
        Assert.Equal("Explicá la regla de dependencias.", pregunta.Enunciado);
        Assert.True(resultado.RevisadaPorAgenteRevisor);
        Assert.DoesNotContain(resultado.Preguntas, p => p.Opciones?.Any(o => o.Texto.Contains("Opción A")) == true);
    }

    [Fact]
    public async Task GenerarPreguntasAsync_TodasInvalidas_LanzaIaNoDisponible()
    {
        _fakeLlmClient.Resultado = new LlmCompletionResult
        {
            Contenido = JsonSerializer.Serialize(new
            {
                preguntas = new[] { new { enunciado = "Abierta con rúbrica que no suma", tipo = "Abierta", puntajeMaximo = 10.0,
                    rubrica = new { criterios = new[] { new { descripcion = "Define", puntaje = 3.0 } } } } }
            }),
            Proveedor = "x",
            Modelo = "y"
        };

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => _orchestrator.GenerarPreguntasAsync(
            new SugerirPreguntasRequest { MaterialId = Guid.NewGuid(), Cantidad = 1 }));
        Assert.Contains("IA_NO_DISPONIBLE", ex.Message);
    }

    [Fact]
    public void EsPreguntaValida_OpcionMultipleConUnaCorrecta_EsValida()
    {
        var p = new PreguntaSugeridaDto
        {
            Enunciado = "¿Qué es SOLID?",
            Tipo = "OpcionMultiple",
            PuntajeMaximo = 1,
            Opciones = new List<OpcionPreguntaDto>
            {
                new() { Texto = "Principios de diseño", EsCorrecta = true },
                new() { Texto = "Un framework", EsCorrecta = false }
            }
        };

        Assert.True(AgentesOrchestrator.EsPreguntaValida(p));
    }

    [Fact]
    public void EsPreguntaValida_OpcionMultipleSinCorrecta_NoEsValida()
    {
        var p = new PreguntaSugeridaDto
        {
            Enunciado = "¿Qué es SOLID?",
            Tipo = "OpcionMultiple",
            PuntajeMaximo = 1,
            Opciones = new List<OpcionPreguntaDto>
            {
                new() { Texto = "A", EsCorrecta = false },
                new() { Texto = "B", EsCorrecta = false }
            }
        };

        Assert.False(AgentesOrchestrator.EsPreguntaValida(p));
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

    // ── S3-IA-02: el modelo a veces devuelve la rúbrica con otra forma ──

    [Fact]
    public void ParsearPreguntas_RubricaComoListaYCriterioConOtroNombre_SeLeeIgual()
    {
        const string json = """
            ```json
            { "preguntas": [
              { "enunciado": "Explicá qué es un algoritmo", "tipo": "abierta", "puntajeMaximo": "10",
                "rubrica": [ { "criterio": "Define", "puntaje": 6 }, { "criterio": "Ejemplifica", "puntaje": "4" } ] },
              { "enunciado": "¿Qué es una variable?", "tipo": "Opción múltiple", "puntajeMaximo": 2,
                "opciones": [ { "texto": "Memoria con nombre", "esCorrecta": true }, { "texto": "Un bucle", "esCorrecta": "false" } ] }
            ] }
            ```
            """;

        var preguntas = AgentesOrchestrator.ParsearPreguntas(json);

        Assert.Equal(2, preguntas.Count);
        Assert.Equal("Abierta", preguntas[0].Tipo);
        Assert.Equal(10m, preguntas[0].PuntajeMaximo);
        Assert.Equal(new[] { "Define", "Ejemplifica" }, preguntas[0].Rubrica!.Criterios.Select(c => c.Descripcion));
        Assert.True(AgentesOrchestrator.EsPreguntaValida(preguntas[0]));
        Assert.Equal("OpcionMultiple", preguntas[1].Tipo);
        Assert.True(AgentesOrchestrator.EsPreguntaValida(preguntas[1]));
    }

    [Fact]
    public void ParsearPreguntas_UnaPreguntaRota_NoTiraAbajoLasDemas()
    {
        const string json = """
            { "preguntas": [
              "esto no es un objeto",
              { "enunciado": "¿Qué es una variable?", "tipo": "OpcionMultiple", "puntajeMaximo": 2,
                "opciones": [ { "texto": "Memoria con nombre", "esCorrecta": true }, { "texto": "Un bucle", "esCorrecta": false } ] }
            ] }
            """;

        var pregunta = Assert.Single(AgentesOrchestrator.ParsearPreguntas(json));
        Assert.Equal("¿Qué es una variable?", pregunta.Enunciado);
    }

    private sealed class FakeLlmClient : ILlmClient
    {
        public LlmCompletionResult Resultado { get; set; } = new() { Contenido = "{}" };

        public int Llamadas { get; private set; }

        public Task<LlmCompletionResult> CompletarAsync(
            string systemPrompt,
            string userPrompt,
            IReadOnlyList<LlmToolDefinition>? tools = null,
            CancellationToken cancellationToken = default)
        {
            Llamadas++;
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
        public TextoMaterialCursoDto? Material { get; set; }
        public bool DevolverNull { get; set; }
        public Exception? Lanzar { get; set; }

        public Task<TextoMaterialCursoDto?> ObtenerTextoMaterialAsync(Guid materialId, CancellationToken ct = default)
        {
            if (Lanzar != null) throw Lanzar;
            if (DevolverNull) return Task.FromResult<TextoMaterialCursoDto?>(null);
            return Task.FromResult<TextoMaterialCursoDto?>(Material ?? new TextoMaterialCursoDto(
                materialId, "Titulo", 1, "Texto apunte", true));
        }
    }
}
