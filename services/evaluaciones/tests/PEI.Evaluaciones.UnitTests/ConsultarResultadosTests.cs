using PEI.Evaluaciones.Application.Evaluaciones;
using PEI.Evaluaciones.Domain.Entities;

namespace PEI.Evaluaciones.UnitTests;

public class ConsultarResultadosTests
{
    private static readonly Guid InstitucionA = Guid.Parse("11111111-1111-4111-8111-111111111111");
    private static readonly Guid InstitucionB = Guid.Parse("22222222-2222-4222-8222-222222222222");
    private static readonly Guid EvaluacionId = Guid.Parse("33333333-3333-4333-8333-333333333333");
    private static readonly Guid EntregaId = Guid.Parse("44444444-4444-4444-8444-444444444444");

    [Fact]
    public async Task AislamientoInstituciones_Listar_EvaluacionDeOtraInstitucion_DevuelveNoEncontrado()
    {
        // Regla: Aislamiento multi-tenant. Si la evaluación pertenece a Institución B y se consulta desde Institución A -> NoEncontrado
        var repo = new RepositorioResultadosFake(InstitucionB);
        var handler = new ConsultarResultadosHandler(repo);

        var resultado = await handler.Handle(
            new ListarEntregasEvaluacionQuery(EvaluacionId, InstitucionA),
            CancellationToken.None);

        Assert.Equal(TipoResultado.NoEncontrado, resultado.Tipo);
    }

    [Fact]
    public async Task AislamientoInstituciones_Listar_MismaInstitucion_DevuelveOkConEntregas()
    {
        var repo = new RepositorioResultadosFake(InstitucionA);
        var handler = new ConsultarResultadosHandler(repo);

        var resultado = await handler.Handle(
            new ListarEntregasEvaluacionQuery(EvaluacionId, InstitucionA),
            CancellationToken.None);

        Assert.Equal(TipoResultado.Ok, resultado.Tipo);
        Assert.Single(resultado.Valor!);
        Assert.Equal(1m, resultado.Valor![0].NotaMultipleChoice);
        // RN-CMP-07: Alertas de foco clasificadas por severidad
        Assert.Equal(1, resultado.Valor![0].AlertasFoco.Rojas);
        Assert.Equal(0, resultado.Valor![0].AlertasFoco.Amarillas);
    }

    [Fact]
    public async Task AislamientoInstituciones_Detalle_EvaluacionDeOtraInstitucion_DevuelveNoEncontrado()
    {
        var repo = new RepositorioResultadosFake(InstitucionB);
        var handler = new ConsultarResultadosHandler(repo);

        var resultado = await handler.Handle(
            new ObtenerEntregaDetalleQuery(EvaluacionId, EntregaId, InstitucionA),
            CancellationToken.None);

        Assert.Equal(TipoResultado.NoEncontrado, resultado.Tipo);
    }

    [Fact]
    public async Task ObtenerDetalle_MismaInstitucion_DevuelveDetalleConAlertasYRespuestas()
    {
        var repo = new RepositorioResultadosFake(InstitucionA);
        var handler = new ConsultarResultadosHandler(repo);

        var resultado = await handler.Handle(
            new ObtenerEntregaDetalleQuery(EvaluacionId, EntregaId, InstitucionA),
            CancellationToken.None);

        Assert.Equal(TipoResultado.Ok, resultado.Tipo);
        Assert.NotNull(resultado.Valor);
        Assert.Equal(EntregaId, resultado.Valor.EntregaId);
        Assert.Single(resultado.Valor.Alertas);
        Assert.Equal("Roja", resultado.Valor.Alertas[0].Severidad);
    }

    private sealed class RepositorioResultadosFake : IResultadosRepositorio
    {
        private readonly Guid _institucionId;

        public RepositorioResultadosFake(Guid institucionId)
        {
            _institucionId = institucionId;
        }

        public Task<Evaluacion?> ObtenerEvaluacionAsync(Guid evaluacionId, Guid institucionId, CancellationToken ct)
        {
            if (institucionId != _institucionId) return Task.FromResult<Evaluacion?>(null);

            return Task.FromResult<Evaluacion?>(new Evaluacion
            {
                Id = evaluacionId,
                InstitucionId = _institucionId,
                Titulo = "Parcial",
                Tipo = "Examen",
                PreguntasJson = "{\"version\":1,\"preguntas\":[{\"id\":\"" + Guid.NewGuid() + "\",\"tipo\":\"OpcionMultiple\",\"puntajeMaximo\":1}]}"
            });
        }

        public Task<List<EntregaExamen>> ListarEntregasPorEvaluacionAsync(Guid evaluacionId, Guid institucionId, CancellationToken ct)
        {
            return Task.FromResult(new List<EntregaExamen>
            {
                new()
                {
                    Id = EntregaId,
                    EvaluacionId = evaluacionId,
                    InstitucionId = institucionId,
                    AlumnoId = Guid.NewGuid(),
                    SalaId = Guid.NewGuid(),
                    Estado = EstadoEntregaExamen.Corregida,
                    NotaMultipleChoice = 1m,
                    FechaEntrega = DateTimeOffset.UtcNow,
                    RespuestasJson = "[]",
                    AlertasFocoJson = "[{\"tipoEvento\":\"blur\",\"duracionMs\":3500,\"severidad\":\"Roja\"}]"
                }
            });
        }

        public Task<EntregaExamen?> ObtenerEntregaPorIdAsync(Guid evaluacionId, Guid entregaId, Guid institucionId, CancellationToken ct)
        {
            if (institucionId != _institucionId) return Task.FromResult<EntregaExamen?>(null);

            return Task.FromResult<EntregaExamen?>(new EntregaExamen
            {
                Id = entregaId,
                EvaluacionId = evaluacionId,
                InstitucionId = institucionId,
                AlumnoId = Guid.NewGuid(),
                SalaId = Guid.NewGuid(),
                Estado = EstadoEntregaExamen.Corregida,
                NotaMultipleChoice = 1m,
                FechaEntrega = DateTimeOffset.UtcNow,
                RespuestasJson = "[]",
                AlertasFocoJson = "[{\"tipoEvento\":\"blur\",\"duracionMs\":3500,\"severidad\":\"Roja\"}]"
            });
        }

        public Task<List<CalificacionSugerida>> ListarCalificacionesPorEntregasAsync(List<Guid> entregaIds, CancellationToken ct) =>
            Task.FromResult(new List<CalificacionSugerida>());

        public Task<List<CalificacionSugerida>> ListarCalificacionesPorEntregaAsync(Guid entregaId, CancellationToken ct) =>
            Task.FromResult(new List<CalificacionSugerida>());
    }
}