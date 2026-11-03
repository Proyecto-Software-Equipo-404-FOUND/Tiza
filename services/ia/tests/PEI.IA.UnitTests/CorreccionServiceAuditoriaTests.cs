using Microsoft.Extensions.Logging.Abstractions;
using PEI.IA.Application.DTOs;
using PEI.IA.Application.Interfaces;
using PEI.IA.Application.Prompts;
using PEI.IA.Application.Services;
using PEI.IA.Domain.Entities;
using PEI.IA.Infrastructure.Mcp;
using PEI.IA.Infrastructure.Repositories;

namespace PEI.IA.UnitTests;

public class CorreccionServiceAuditoriaTests
{
    private readonly PromptBuilder _promptBuilder = new();
    private readonly LocalToolExecutor _toolExecutor = new(NullLogger<LocalToolExecutor>.Instance);

    public CorreccionServiceAuditoriaTests()
    {
        InMemorySolicitudCorreccionRepository.Limpiar();
    }

    [Fact]
    public async Task CorregirAsync_CuandoLlmRespondeExitosamente_RegistraSolicitudConMetricasEnRepositorio()
    {
        // Arrange
        var institucionId = Guid.NewGuid();
        var evaluacionId = Guid.NewGuid();
        var preguntaId = Guid.NewGuid();

        var fakeLlm = new FakeAuditoriaLlmClient(new LlmCompletionResult
        {
            Proveedor = "Groq",
            Modelo = "llama-3.3-70b-versatile",
            TokensEntrada = 180,
            TokensSalida = 65,
            LatenciaMs = 410,
            Contenido = """
            {
                "notaSugerida": 9.0,
                "justificacion": "Respuesta completa y con fundamentación teórica sólida.",
                "desglosePorCriterio": [
                    {
                        "criterio": "Concepto",
                        "puntajeMaximo": 10.0,
                        "puntajeOtorgado": 9.0,
                        "comentario": "Excelente explicación."
                    }
                ]
            }
            """
        });

        var repository = new InMemorySolicitudCorreccionRepository();
        var service = new CorreccionService(
            fakeLlm,
            _promptBuilder,
            _toolExecutor,
            evaluacionesClient: null,
            solicitudRepository: repository,
            logger: NullLogger<CorreccionService>.Instance);

        var request = new CorregirRespuestaRequest
        {
            InstitucionId = institucionId,
            EvaluacionId = evaluacionId,
            PreguntaId = preguntaId,
            PreguntaEnunciado = "¿Qué es el polimorfismo?",
            PuntajeMaximo = 10,
            RubricaJson = "{\"criterios\":[]}",
            RespuestaTexto = "Es la capacidad de enviar mensajes sintácticamente iguales a objetos de distintos tipos."
        };

        // Act
        var response = await service.CorregirAsync(request);

        // Assert
        Assert.NotNull(response);
        Assert.NotEqual(Guid.Empty, response.SolicitudId);

        var persistida = await repository.ObtenerPorIdAsync(response.SolicitudId);
        Assert.NotNull(persistida);
        Assert.Equal(institucionId, persistida.InstitucionId);
        Assert.Equal(evaluacionId, persistida.EvaluacionId);
        Assert.Equal(preguntaId, persistida.PreguntaId);
        Assert.Equal(EstadoCorreccion.Completada, persistida.Estado);
        Assert.Equal("Groq", persistida.Proveedor);
        Assert.Equal("llama-3.3-70b-versatile", persistida.Modelo);
        Assert.Equal(180, persistida.TokensEntrada);
        Assert.Equal(65, persistida.TokensSalida);
        Assert.Equal(410, persistida.LatenciaMs);
        Assert.Equal(9.0m, persistida.NotaSugerida);
        Assert.Contains("fundamentación", persistida.Justificacion);
    }

    [Fact]
    public async Task CorregirAsync_CuandoLlmFalla_RegistraSolicitudComoFallidaEnRepositorio()
    {
        // Arrange
        var institucionId = Guid.NewGuid();
        var fakeLlm = new FakeAuditoriaLlmClient(ex =>
            throw new InvalidOperationException("Todos los proveedores LLM fallaron (Groq: 429; OpenRouter: Timeout)"));

        var repository = new InMemorySolicitudCorreccionRepository();
        var service = new CorreccionService(
            fakeLlm,
            _promptBuilder,
            _toolExecutor,
            evaluacionesClient: null,
            solicitudRepository: repository,
            logger: NullLogger<CorreccionService>.Instance);

        var request = new CorregirRespuestaRequest
        {
            InstitucionId = institucionId,
            PreguntaEnunciado = "¿Qué es la encapsulación?",
            PuntajeMaximo = 10,
            RubricaJson = "{\"criterios\":[]}",
            RespuestaTexto = "Ocultamiento de datos."
        };

        // Act
        var response = await service.CorregirAsync(request);

        // Assert
        Assert.NotNull(response);
        Assert.Contains("Fallback", response.Justificacion);

        var persistida = await repository.ObtenerPorIdAsync(response.SolicitudId);
        Assert.NotNull(persistida);
        Assert.Equal(EstadoCorreccion.Fallida, persistida.Estado);
        Assert.NotNull(persistida.Error);
        Assert.Contains("no disponible", persistida.Error);
    }

    [Fact]
    public async Task ListarAsync_FiltradoPorInstitucion_RetornaSoloDeEsaInstitucion()
    {
        // Arrange
        var repository = new InMemorySolicitudCorreccionRepository();
        var inst1 = Guid.NewGuid();
        var inst2 = Guid.NewGuid();

        var sol1 = SolicitudCorreccion.Crear(inst1, "resp 1", "preg 1", "{}", 10);
        var sol2 = SolicitudCorreccion.Crear(inst1, "resp 2", "preg 2", "{}", 10);
        var sol3 = SolicitudCorreccion.Crear(inst2, "resp 3", "preg 3", "{}", 10);

        await repository.GuardarAsync(sol1);
        await repository.GuardarAsync(sol2);
        await repository.GuardarAsync(sol3);

        // Act
        var listaInst1 = await repository.ListarAsync(inst1);
        var listaInst2 = await repository.ListarAsync(inst2);

        // Assert
        Assert.Equal(2, listaInst1.Count);
        Assert.Single(listaInst2);
        Assert.All(listaInst1, s => Assert.Equal(inst1, s.InstitucionId));
        Assert.All(listaInst2, s => Assert.Equal(inst2, s.InstitucionId));
    }

    // ── Fake LLM Client ─────────────────────────────────────────────────────
    private sealed class FakeAuditoriaLlmClient : ILlmClient
    {
        private readonly LlmCompletionResult? _resultado;
        private readonly Action<object>? _onLlamada;

        public FakeAuditoriaLlmClient(LlmCompletionResult resultado) => _resultado = resultado;
        public FakeAuditoriaLlmClient(Action<object> onLlamada) => _onLlamada = onLlamada;

        public Task<LlmCompletionResult> CompletarAsync(
            string systemPrompt,
            string userPrompt,
            IReadOnlyList<LlmToolDefinition>? tools = null,
            CancellationToken cancellationToken = default)
        {
            if (_onLlamada != null)
            {
                _onLlamada(this);
            }

            return Task.FromResult(_resultado!);
        }
    }
}
