using PEI.Evaluaciones.Application.Evaluaciones;
using PEI.Evaluaciones.Domain.Entities;

namespace PEI.Evaluaciones.UnitTests;

// S3-EV-01: la nota de los criterios de la IA aparecía en 0.
public class DesgloseCriterioTests
{
    // JSON tal como lo guarda la IA (CorreccionService serializa DesgloseCriterio en camelCase).
    private const string DesgloseDeLaIa =
        "[{\"criterio\":\"Define el concepto\",\"puntajeMaximo\":3,\"puntajeOtorgado\":2.5,\"comentario\":\"Bien definido\"}," +
        "{\"criterio\":\"Da un ejemplo\",\"puntajeMaximo\":2,\"puntajeOtorgado\":1,\"comentario\":\"Ejemplo incompleto\"}]";

    [Fact]
    public void Leer_FormatoDeLaIa_TomaPuntajeOtorgadoYPuntajeMaximo()
    {
        var desglose = DesgloseCriterioLector.Leer(DesgloseDeLaIa);

        Assert.NotNull(desglose);
        Assert.Equal(2, desglose!.Count);
        Assert.Equal("Define el concepto", desglose[0].Criterio);
        Assert.Equal(2.5m, desglose[0].Puntaje);
        Assert.Equal(3m, desglose[0].PuntajeMaximo);
        Assert.Equal("Bien definido", desglose[0].Comentario);
        Assert.Equal(1m, desglose[1].Puntaje);
        Assert.Equal(2m, desglose[1].PuntajeMaximo);
    }

    [Fact]
    public void Leer_FormatoOriginalDelContrato_SigueFuncionando()
    {
        var desglose = DesgloseCriterioLector.Leer(
            "[{\"criterio\":\"Relaciona con placas tectónicas\",\"puntaje\":1.5,\"comentario\":\"Incompleto\"}]");

        Assert.NotNull(desglose);
        Assert.Equal(1.5m, Assert.Single(desglose!).Puntaje);
        Assert.Null(desglose[0].PuntajeMaximo);
    }

    [Fact]
    public void Leer_PascalCaseYNumerosComoTexto_SeInterpretan()
    {
        var desglose = DesgloseCriterioLector.Leer(
            "[{\"Criterio\":\"Claridad\",\"PuntajeMaximo\":\"4\",\"PuntajeOtorgado\":\"3.5\",\"Comentario\":\"Claro\"}]");

        var criterio = Assert.Single(desglose!);
        Assert.Equal(3.5m, criterio.Puntaje);
        Assert.Equal(4m, criterio.PuntajeMaximo);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("no es json")]
    [InlineData("{\"criterio\":\"no es una lista\"}")]
    public void Leer_JsonVacioOInvalido_DevuelveNull(string? json)
    {
        Assert.Null(DesgloseCriterioLector.Leer(json));
    }

    [Fact]
    public async Task ObtenerDetalle_PreguntaAbiertaCorregidaPorIa_DevuelveLosPuntajesDeCadaCriterio()
    {
        var repo = new RepositorioConPreguntaAbierta(DesgloseDeLaIa);
        var handler = new ConsultarResultadosHandler(repo);

        var resultado = await handler.Handle(
            new ObtenerEntregaDetalleQuery(repo.EvaluacionId, repo.EntregaId, repo.InstitucionId),
            CancellationToken.None);

        Assert.Equal(TipoResultado.Ok, resultado.Tipo);
        var respuesta = Assert.Single(resultado.Valor!.Respuestas);
        Assert.Equal("SugeridaIA", respuesta.EstadoCorreccion);
        Assert.Equal(3.5m, respuesta.NotaSugerida);
        Assert.NotNull(respuesta.DesglosePorCriterio);
        Assert.Equal(new[] { 2.5m, 1m }, respuesta.DesglosePorCriterio!.Select(d => d.Puntaje));
        Assert.Equal(new decimal?[] { 3m, 2m }, respuesta.DesglosePorCriterio!.Select(d => d.PuntajeMaximo));
    }

    private sealed class RepositorioConPreguntaAbierta : IResultadosRepositorio
    {
        public Guid InstitucionId { get; } = Guid.NewGuid();
        public Guid EvaluacionId { get; } = Guid.NewGuid();
        public Guid EntregaId { get; } = Guid.NewGuid();
        private readonly Guid _preguntaId = Guid.NewGuid();
        private readonly string _desgloseJson;

        public RepositorioConPreguntaAbierta(string desgloseJson) => _desgloseJson = desgloseJson;

        public Task<Evaluacion?> ObtenerEvaluacionAsync(Guid evaluacionId, Guid institucionId, CancellationToken ct) =>
            Task.FromResult<Evaluacion?>(new Evaluacion
            {
                Id = evaluacionId,
                InstitucionId = InstitucionId,
                Titulo = "Parcial",
                Tipo = "Examen",
                PreguntasJson = "{\"version\":1,\"preguntas\":[{\"id\":\"" + _preguntaId +
                                "\",\"tipo\":\"Abierta\",\"enunciado\":\"¿Qué es un sismo?\",\"puntajeMaximo\":5}]}"
            });

        public Task<List<EntregaExamen>> ListarEntregasPorEvaluacionAsync(Guid evaluacionId, Guid institucionId, CancellationToken ct) =>
            Task.FromResult(new List<EntregaExamen> { CrearEntrega() });

        public Task<EntregaExamen?> ObtenerEntregaPorIdAsync(Guid evaluacionId, Guid entregaId, Guid institucionId, CancellationToken ct) =>
            Task.FromResult<EntregaExamen?>(CrearEntrega());

        public Task<List<CalificacionSugerida>> ListarCalificacionesPorEntregasAsync(List<Guid> entregaIds, CancellationToken ct) =>
            Task.FromResult(new List<CalificacionSugerida> { CrearCalificacion() });

        public Task<List<CalificacionSugerida>> ListarCalificacionesPorEntregaAsync(Guid entregaId, CancellationToken ct) =>
            Task.FromResult(new List<CalificacionSugerida> { CrearCalificacion() });

        public Task<CalificacionDocente?> ObtenerCalificacionDocenteAsync(Guid entregaId, Guid preguntaId, CancellationToken ct) =>
            Task.FromResult<CalificacionDocente?>(null);

        public Task<List<CalificacionDocente>> ListarCalificacionesDocentePorEntregaAsync(Guid entregaId, CancellationToken ct) =>
            Task.FromResult(new List<CalificacionDocente>());

        public Task<List<CalificacionDocente>> ListarCalificacionesDocentePorEntregasAsync(List<Guid> entregaIds, CancellationToken ct) =>
            Task.FromResult(new List<CalificacionDocente>());

        public Task GuardarCalificacionDocenteAsync(CalificacionDocente calificacion, CancellationToken ct) =>
            Task.CompletedTask;

        public Task GuardarCambiosAsync(CancellationToken ct) => Task.CompletedTask;    

        private EntregaExamen CrearEntrega() => new()
        {
            Id = EntregaId,
            EvaluacionId = EvaluacionId,
            InstitucionId = InstitucionId,
            AlumnoId = Guid.NewGuid(),
            SalaId = Guid.NewGuid(),
            Estado = EstadoEntregaExamen.Corregida,
            FechaEntrega = DateTimeOffset.UtcNow,
            RespuestasJson = "[{\"preguntaId\":\"" + _preguntaId + "\",\"respuesta\":\"Un movimiento de la corteza terrestre\"}]",
            AlertasFocoJson = "[]"
        };

        private CalificacionSugerida CrearCalificacion() => new()
        {
            EntregaId = EntregaId,
            InstitucionId = InstitucionId,
            PreguntaId = _preguntaId,
            NotaSugerida = 3.5m,
            Justificacion = "Define bien, el ejemplo es incompleto.",
            DesgloseJson = _desgloseJson
        };
    }
}
