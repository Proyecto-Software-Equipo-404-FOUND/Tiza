using Microsoft.Extensions.Logging.Abstractions;
using PEI.IA.Domain.Entities;
using PEI.IA.McpServer.Tools;

namespace PEI.IA.UnitTests;

public class RegistrarCalificacionToolTests
{
    // IDs y constantes reutilizadas
    private static readonly Guid EntregaIdValido   = Guid.NewGuid();
    private static readonly Guid PreguntaIdValido  = Guid.NewGuid();
    private const decimal       PuntajeMaximoValido = 10m;

    // ── Helper para construir la tool con el stub de Evaluaciones ──────────

    /// <summary>
    /// Crea una instancia de la tool que responde <paramref name="callbackOk"/>
    /// cada vez que se llama a RegistrarSugerenciaAsync.
    /// </summary>
    private static RegistrarCalificacionSugeridaTool CrearTool(bool callbackOk = true)
    {
        var stub = new EvaluacionesClientStub(callbackOk);
        return new RegistrarCalificacionSugeridaTool(
            stub,
            NullLogger<RegistrarCalificacionSugeridaTool>.Instance);
    }

    // ── Happy path ──────────────────────────────────────────────────────────

    [Fact]
    public async Task RegistrarCalificacion_ValoresValidos_RetornaExito()
    {
        var tool = CrearTool(callbackOk: true);

        var result = await tool.RegistrarCalificacion(
            EntregaIdValido, PreguntaIdValido,
            notaSugerida: 8.5m, puntajeMaximo: PuntajeMaximoValido,
            justificacion: "El alumno comprende la abstracción y provee ejemplos correctos.",
            desgloseJson: "[{\"criterio\":\"Definición\",\"puntajeMaximo\":5,\"puntajeOtorgado\":4.5}]");

        Assert.True(result.Exito);
        Assert.Equal(8.5m, result.NotaSugerida);
        Assert.Null(result.Error);
        Assert.NotNull(result.Mensaje);
    }

    [Fact]
    public async Task RegistrarCalificacion_NotaIgualAlMaximo_RegistraCorrectamente()
    {
        var tool = CrearTool();

        var result = await tool.RegistrarCalificacion(
            EntregaIdValido, PreguntaIdValido,
            PuntajeMaximoValido, PuntajeMaximoValido,
            "Respuesta perfecta.");

        Assert.True(result.Exito);
        Assert.Equal(PuntajeMaximoValido, result.NotaSugerida);
    }

    [Fact]
    public async Task RegistrarCalificacion_NotaCero_RegistraCorrectamente()
    {
        var tool = CrearTool();

        var result = await tool.RegistrarCalificacion(
            EntregaIdValido, PreguntaIdValido,
            0m, PuntajeMaximoValido,
            "No responde el concepto solicitado.");

        Assert.True(result.Exito);
        Assert.Equal(0m, result.NotaSugerida);
    }

    // ── Callback hacia Evaluaciones ─────────────────────────────────────────

    [Fact]
    public async Task RegistrarCalificacion_LlamaCallbackConPreguntaId()
    {
        var stub = new EvaluacionesClientStub(true);
        var tool = new RegistrarCalificacionSugeridaTool(
            stub, NullLogger<RegistrarCalificacionSugeridaTool>.Instance);

        await tool.RegistrarCalificacion(
            EntregaIdValido, PreguntaIdValido,
            7m, PuntajeMaximoValido, "Justificación correcta.");

        Assert.Equal(1, stub.LlamadasRegistradas);
        Assert.Equal(EntregaIdValido, stub.UltimoEntregaId);
        Assert.Equal(PreguntaIdValido, stub.UltimoPreguntaId);
        Assert.Equal(7m, stub.UltimaNotaSugerida);
    }

    [Fact]
    public async Task RegistrarCalificacion_CuandoCallbackFalla_RetornaFallido()
    {
        var tool = CrearTool(callbackOk: false);

        var result = await tool.RegistrarCalificacion(
            EntregaIdValido, PreguntaIdValido,
            6m, PuntajeMaximoValido, "Justificación válida.");

        Assert.False(result.Exito);
        Assert.NotNull(result.Error);
    }

    // ── Validaciones: notaSugerida ───────────────────────────────────────────

    [Fact]
    public async Task RegistrarCalificacion_NotaNegativa_RetornaErrorSinCallback()
    {
        var stub = new EvaluacionesClientStub(true);
        var tool = new RegistrarCalificacionSugeridaTool(
            stub, NullLogger<RegistrarCalificacionSugeridaTool>.Instance);

        var result = await tool.RegistrarCalificacion(
            EntregaIdValido, PreguntaIdValido, -1m, PuntajeMaximoValido, "Justificación");

        Assert.False(result.Exito);
        Assert.Contains("negativa", result.Error);
        Assert.Equal(0, stub.LlamadasRegistradas); // sin callback
    }

    [Fact]
    public async Task RegistrarCalificacion_NotaSuperaPuntajeMaximo_RetornaError()
    {
        var tool = CrearTool();

        var result = await tool.RegistrarCalificacion(
            EntregaIdValido, PreguntaIdValido, 11m, 10m, "Justificación válida.");

        Assert.False(result.Exito);
        Assert.Contains("excede", result.Error);
    }

    // ── Validaciones: puntajeMaximo ─────────────────────────────────────────

    [Fact]
    public async Task RegistrarCalificacion_PuntajeMaximoCero_RetornaError()
    {
        var tool = CrearTool();

        var result = await tool.RegistrarCalificacion(
            EntregaIdValido, PreguntaIdValido, 5m, 0m, "Justificación válida.");

        Assert.False(result.Exito);
        Assert.Contains("mayor que cero", result.Error);
    }

    [Fact]
    public async Task RegistrarCalificacion_PuntajeMaximoNegativo_RetornaError()
    {
        var tool = CrearTool();

        var result = await tool.RegistrarCalificacion(
            EntregaIdValido, PreguntaIdValido, 5m, -1m, "Justificación válida.");

        Assert.False(result.Exito);
        Assert.Contains("mayor que cero", result.Error);
    }

    // ── Validaciones: justificacion ─────────────────────────────────────────

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public async Task RegistrarCalificacion_JustificacionVacia_RetornaErrorSinCallback(string? justificacionInvalida)
    {
        var stub = new EvaluacionesClientStub(true);
        var tool = new RegistrarCalificacionSugeridaTool(
            stub, NullLogger<RegistrarCalificacionSugeridaTool>.Instance);

        var result = await tool.RegistrarCalificacion(
            EntregaIdValido, PreguntaIdValido, 7m, PuntajeMaximoValido, justificacionInvalida!);

        Assert.False(result.Exito);
        Assert.Contains("obligatoria", result.Error);
        Assert.Equal(0, stub.LlamadasRegistradas);
    }

    [Fact]
    public async Task RegistrarCalificacion_JustificacionSuperaLimite4000_RetornaError()
    {
        var tool = CrearTool();
        var justificacionLarga = new string('x', 4001);

        var result = await tool.RegistrarCalificacion(
            EntregaIdValido, PreguntaIdValido, 7m, PuntajeMaximoValido, justificacionLarga);

        Assert.False(result.Exito);
        Assert.Contains("4000", result.Error);
    }

    // ── Entidad de dominio ──────────────────────────────────────────────────

    [Fact]
    public void CalificacionSugerida_Entidad_InicializaIdYFechaUtc()
    {
        var entidad = new CalificacionSugerida
        {
            EntregaId     = Guid.NewGuid(),
            NotaSugerida  = 10,
            Justificacion = "Perfecto"
        };

        Assert.NotEqual(Guid.Empty, entidad.Id);
        Assert.NotEqual(Guid.Empty, entidad.EntregaId);
        Assert.True((DateTime.UtcNow - entidad.FechaRegistroUtc).TotalSeconds < 5);
    }

    // ── Stub de IEvaluacionesClient ─────────────────────────────────────────

    private sealed class EvaluacionesClientStub : PEI.IA.Application.Interfaces.IEvaluacionesClient
    {
        private readonly bool _respuesta;

        public int     LlamadasRegistradas { get; private set; }
        public Guid    UltimoEntregaId     { get; private set; }
        public Guid?   UltimoPreguntaId    { get; private set; }
        public decimal UltimaNotaSugerida  { get; private set; }

        public EvaluacionesClientStub(bool respuesta) => _respuesta = respuesta;

        public Task<bool> RegistrarSugerenciaAsync(
            Guid entregaId,
            decimal notaSugerida,
            string justificacion,
            string desgloseJson,
            Guid? preguntaId = null,
            CancellationToken ct = default)
        {
            LlamadasRegistradas++;
            UltimoEntregaId    = entregaId;
            UltimoPreguntaId   = preguntaId;
            UltimaNotaSugerida = notaSugerida;
            return Task.FromResult(_respuesta);
        }
    }
}
