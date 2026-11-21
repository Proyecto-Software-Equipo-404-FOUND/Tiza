using PEI.Evaluaciones.Application.Evaluaciones;
using PEI.Evaluaciones.Application.Evaluaciones.Queries.GetMisExamenes;
using PEI.Evaluaciones.Domain.Entities;
using Xunit;

namespace PEI.Evaluaciones.UnitTests;

public class MisExamenesTests
{
    private sealed class FakeRepoMisExamenes : IResultadosRepositorio
    {
        public List<(EntregaExamen Entrega, Evaluacion Evaluacion)> Filas { get; } = new();
        public Guid? UltimoAlumnoConsultado { get; private set; }

        public Task<List<(EntregaExamen Entrega, Evaluacion Evaluacion)>> ListarEntregasPorAlumnoAsync(
            Guid alumnoId,
            CancellationToken ct)
        {
            UltimoAlumnoConsultado = alumnoId;
            var filtradas = Filas.Where(x => x.Entrega.AlumnoId == alumnoId).ToList();
            return Task.FromResult(filtradas);
        }

        public Task<Evaluacion?> ObtenerEvaluacionAsync(Guid evaluacionId, Guid institucionId, CancellationToken ct)
            => Task.FromResult<Evaluacion?>(null);

        public Task<List<EntregaExamen>> ListarEntregasPorEvaluacionAsync(Guid evaluacionId, Guid institucionId, CancellationToken ct)
            => Task.FromResult(new List<EntregaExamen>());

        public Task<EntregaExamen?> ObtenerEntregaPorIdAsync(Guid evaluacionId, Guid entregaId, Guid institucionId, CancellationToken ct)
            => Task.FromResult<EntregaExamen?>(null);

        public Task<List<CalificacionSugerida>> ListarCalificacionesPorEntregasAsync(List<Guid> entregaIds, CancellationToken ct)
            => Task.FromResult(new List<CalificacionSugerida>());

        public Task<List<CalificacionSugerida>> ListarCalificacionesPorEntregaAsync(Guid entregaId, CancellationToken ct)
            => Task.FromResult(new List<CalificacionSugerida>());

        public Task<CalificacionDocente?> ObtenerCalificacionDocenteAsync(Guid entregaId, Guid preguntaId, CancellationToken ct)
            => Task.FromResult<CalificacionDocente?>(null);

        public Task<List<CalificacionDocente>> ListarCalificacionesDocentePorEntregaAsync(Guid entregaId, CancellationToken ct)
            => Task.FromResult(new List<CalificacionDocente>());

        public Task<List<CalificacionDocente>> ListarCalificacionesDocentePorEntregasAsync(List<Guid> entregaIds, CancellationToken ct)
            => Task.FromResult(new List<CalificacionDocente>());

        public Task GuardarCalificacionDocenteAsync(CalificacionDocente calificacion, CancellationToken ct)
            => Task.CompletedTask;

        public Task GuardarCambiosAsync(CancellationToken ct)
            => Task.CompletedTask;
    }

    [Fact]
    public async Task RolNoAlumno_EsRechazado()
    {
        var repo = new FakeRepoMisExamenes();
        var handler = new GetMisExamenesQueryHandler(repo);

        var resultado = await handler.Handle(
            new GetMisExamenesQuery(Guid.NewGuid(), "Docente"),
            CancellationToken.None);

        Assert.NotEqual(TipoResultado.Ok, resultado.Tipo);
    }

    [Fact]
    public async Task Alumno_SoloVeSusExamenes_OrdenadosDeMasRecienteAMasViejo()
    {
        var alumnoId = Guid.NewGuid();
        var otroAlumnoId = Guid.NewGuid();
        var evalViejaId = Guid.NewGuid();
        var evalNuevaId = Guid.NewGuid();
        var cohorteId = Guid.NewGuid();

        var fechaVieja = DateTimeOffset.UtcNow.AddDays(-5);
        var fechaNueva = DateTimeOffset.UtcNow.AddDays(-1);

        var repo = new FakeRepoMisExamenes();
        repo.Filas.Add((
            new EntregaExamen
            {
                Id = Guid.NewGuid(),
                EvaluacionId = evalViejaId,
                AlumnoId = alumnoId,
                FechaEntrega = fechaVieja,
                EstadoNota = EstadoNotaEntrega.Sugerida
            },
            new Evaluacion { Id = evalViejaId, Titulo = "Parcial Viejo", CohorteId = cohorteId }
        ));
        repo.Filas.Add((
            new EntregaExamen
            {
                Id = Guid.NewGuid(),
                EvaluacionId = evalNuevaId,
                AlumnoId = alumnoId,
                FechaEntrega = fechaNueva,
                EstadoNota = EstadoNotaEntrega.Sugerida
            },
            new Evaluacion { Id = evalNuevaId, Titulo = "Parcial Nuevo", CohorteId = cohorteId }
        ));
        repo.Filas.Add((
            new EntregaExamen
            {
                Id = Guid.NewGuid(),
                EvaluacionId = Guid.NewGuid(),
                AlumnoId = otroAlumnoId,
                FechaEntrega = DateTimeOffset.UtcNow,
                EstadoNota = EstadoNotaEntrega.Aprobada,
                NotaFinal = 10m,
                PuntajeMaximo = 10m
            },
            new Evaluacion { Id = Guid.NewGuid(), Titulo = "Examen Ajeno" }
        ));

        var handler = new GetMisExamenesQueryHandler(repo);
        var resultado = await handler.Handle(
            new GetMisExamenesQuery(alumnoId, "Alumno"),
            CancellationToken.None);

        Assert.Equal(TipoResultado.Ok, resultado.Tipo);
        Assert.Equal(alumnoId, repo.UltimoAlumnoConsultado);
        Assert.NotNull(resultado.Valor);
        Assert.Equal(2, resultado.Valor!.Count);
        Assert.Equal(evalNuevaId, resultado.Valor[0].EvaluacionId);
        Assert.Equal(evalViejaId, resultado.Valor[1].EvaluacionId);
    }

    [Fact]
    public async Task EntregaAprobada_ExponeNotaFinalYPuntajeMaximo()
    {
        var alumnoId = Guid.NewGuid();
        var evalId = Guid.NewGuid();
        var entregaId = Guid.NewGuid();
        var cohorteId = Guid.NewGuid();
        var fecha = DateTimeOffset.UtcNow;

        var repo = new FakeRepoMisExamenes();
        repo.Filas.Add((
            new EntregaExamen
            {
                Id = entregaId,
                EvaluacionId = evalId,
                AlumnoId = alumnoId,
                FechaEntrega = fecha,
                EstadoNota = EstadoNotaEntrega.Aprobada,
                NotaFinal = 8.5m,
                PuntajeMaximo = 10m
            },
            new Evaluacion { Id = evalId, Titulo = "Parcial 1", CohorteId = cohorteId }
        ));

        var handler = new GetMisExamenesQueryHandler(repo);
        var resultado = await handler.Handle(
            new GetMisExamenesQuery(alumnoId, "Alumno"),
            CancellationToken.None);

        Assert.Equal(TipoResultado.Ok, resultado.Tipo);
        var dto = Assert.Single(resultado.Valor!);
        Assert.Equal(evalId, dto.EvaluacionId);
        Assert.Equal(entregaId, dto.EntregaId);
        Assert.Equal(cohorteId, dto.CohorteId);
        Assert.Equal("Parcial 1", dto.Titulo);
        Assert.Equal(fecha, dto.FechaEntregaUtc);
        Assert.Equal("Aprobada", dto.Estado);
        Assert.Equal(8.5m, dto.NotaFinal);
        Assert.Equal(10m, dto.PuntajeMaximo);
    }

    [Fact]
    public async Task EntregaNoAprobada_OcultaNotas_YUsaEstadoPendienteCorreccion()
    {
        var alumnoId = Guid.NewGuid();
        var evalId = Guid.NewGuid();
        var entregaId = Guid.NewGuid();

        var repo = new FakeRepoMisExamenes();
        repo.Filas.Add((
            new EntregaExamen
            {
                Id = entregaId,
                EvaluacionId = evalId,
                AlumnoId = alumnoId,
                FechaEntrega = DateTimeOffset.UtcNow,
                EstadoNota = EstadoNotaEntrega.Editada,
                NotaFinal = 7m,
                PuntajeMaximo = 10m
            },
            new Evaluacion { Id = evalId, Titulo = "Parcial Editado" }
        ));

        var handler = new GetMisExamenesQueryHandler(repo);
        var resultado = await handler.Handle(
            new GetMisExamenesQuery(alumnoId, "Alumno"),
            CancellationToken.None);

        Assert.Equal(TipoResultado.Ok, resultado.Tipo);
        var dto = Assert.Single(resultado.Valor!);
        Assert.Equal("PendienteCorreccion", dto.Estado);
        Assert.Null(dto.NotaFinal);
        Assert.Null(dto.PuntajeMaximo);
        // MiExamenDto no expone comentarioDocente por diseño del contrato
        Assert.Null(typeof(MiExamenDto).GetProperty("ComentarioDocente"));
    }
}