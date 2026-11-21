using System.Text.Json;
using PEI.Evaluaciones.Application.Evaluaciones;
using PEI.Evaluaciones.Application.Evaluaciones.Queries.GetMetricasEvaluacion;
using PEI.Evaluaciones.Domain.Entities;
using Xunit;

namespace PEI.Evaluaciones.UnitTests;

public class MetricasEvaluacionTests
{
    private static readonly Guid InstitucionId = Guid.Parse("11111111-1111-4111-8111-111111111111");
    private static readonly Guid OtraInstitucionId = Guid.Parse("99999999-9999-4999-8999-999999999999");
    private static readonly Guid EvaluacionId = Guid.Parse("22222222-2222-4222-8222-222222222222");
    private static readonly Guid PreguntaMcId = Guid.Parse("aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa");
    private static readonly Guid OpcionCorrectaId = Guid.Parse("bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb");
    private static readonly Guid OpcionIncorrectaId = Guid.Parse("cccccccc-cccc-4ccc-8ccc-cccccccccccc");
    private static readonly Guid PreguntaAbiertaId = Guid.Parse("dddddddd-dddd-4ddd-8ddd-dddddddddddd");

    [Fact]
    public async Task Handle_EvaluacionInexistenteOOtraInstitucion_DevuelveNoEncontrado()
    {
        var repo = new FakeResultadosRepositorio(CrearEvaluacion());
        var handler = new GetMetricasEvaluacionHandler(repo);

        var resultado = await handler.Handle(
            new GetMetricasEvaluacionQuery(EvaluacionId, OtraInstitucionId),
            CancellationToken.None);

        Assert.Equal(TipoResultado.NoEncontrado, resultado.Tipo);
    }

    [Fact]
    public async Task Handle_SinEntregas_DevuelvePromedioNullYListaVacia()
    {
        var repo = new FakeResultadosRepositorio(CrearEvaluacion());
        var handler = new GetMetricasEvaluacionHandler(repo);

        var resultado = await handler.Handle(
            new GetMetricasEvaluacionQuery(EvaluacionId, InstitucionId),
            CancellationToken.None);

        Assert.Equal(TipoResultado.Ok, resultado.Tipo);
        Assert.NotNull(resultado.Valor);
        Assert.Equal(0, resultado.Valor!.CantidadEntregas);
        Assert.Null(resultado.Valor.PromedioExamen);
        Assert.Equal(10m, resultado.Valor.PuntajeMaximoExamen);
        Assert.Empty(resultado.Valor.Alumnos);
    }

    [Fact]
    public async Task Handle_ConEntregas_CalculaPromedioPreguntaMasDificilYComparativaAlumnos()
    {
        var alumno1 = Guid.NewGuid();
        var alumno2 = Guid.NewGuid();
        var entrega1Id = Guid.NewGuid();
        var entrega2Id = Guid.NewGuid();

        var repo = new FakeResultadosRepositorio(CrearEvaluacion());

        // Alumno 1: MC correcta (4m) + abierta sugerida IA (4m) -> 8m
        repo.Entregas.Add(new EntregaExamen
        {
            Id = entrega1Id,
            InstitucionId = InstitucionId,
            EvaluacionId = EvaluacionId,
            SalaId = Guid.NewGuid(),
            AlumnoId = alumno1,
            Estado = EstadoEntregaExamen.Corregida,
            EstadoNota = EstadoNotaEntrega.Sugerida,
            FechaEntrega = DateTimeOffset.UtcNow.AddMinutes(-10),
            RespuestasJson = JsonSerializer.Serialize(new object[]
            {
                new { preguntaId = PreguntaMcId, opcionElegidaId = OpcionCorrectaId.ToString() },
                new { preguntaId = PreguntaAbiertaId, respuesta = "Respuesta 1" }
            })
        });
        repo.CalificacionesIa.Add(new CalificacionSugerida
        {
            Id = Guid.NewGuid(),
            InstitucionId = InstitucionId,
            EntregaId = entrega1Id,
            PreguntaId = PreguntaAbiertaId,
            NotaSugerida = 4m,
            Justificacion = "Bien",
            FechaRegistro = DateTimeOffset.UtcNow
        });

        // Alumno 2: MC incorrecta (0m), pero docente editó abierta a 6m -> 6m
        // (Verifica prioridad: docente > IA > MC)
        repo.Entregas.Add(new EntregaExamen
        {
            Id = entrega2Id,
            InstitucionId = InstitucionId,
            EvaluacionId = EvaluacionId,
            SalaId = Guid.NewGuid(),
            AlumnoId = alumno2,
            Estado = EstadoEntregaExamen.CorreccionManual,
            EstadoNota = EstadoNotaEntrega.Editada,
            FechaEntrega = DateTimeOffset.UtcNow.AddMinutes(-5),
            RespuestasJson = JsonSerializer.Serialize(new object[]
            {
                new { preguntaId = PreguntaMcId, opcionElegidaId = OpcionIncorrectaId.ToString() },
                new { preguntaId = PreguntaAbiertaId, respuesta = "Respuesta 2" }
            })
        });
        repo.CalificacionesIa.Add(new CalificacionSugerida
        {
            Id = Guid.NewGuid(),
            InstitucionId = InstitucionId,
            EntregaId = entrega2Id,
            PreguntaId = PreguntaAbiertaId,
            NotaSugerida = 1m,
            Justificacion = "IA baja",
            FechaRegistro = DateTimeOffset.UtcNow
        });
        repo.CalificacionesDocente.Add(new CalificacionDocente
        {
            Id = Guid.NewGuid(),
            InstitucionId = InstitucionId,
            EntregaId = entrega2Id,
            PreguntaId = PreguntaAbiertaId,
            PuntajeDocente = 6m,
            FechaEdicionUtc = DateTimeOffset.UtcNow
        });

        // Puntajes: Alumno 1 = 8m, Alumno 2 = 6m -> Promedio curso = 7m
        // Pregunta MC: (4 + 0) / 2 = 2.0m promedio
        // Pregunta Abierta: (4 + 6) / 2 = 5.0m promedio -> Pregunta MC es la más difícil
        var handler = new GetMetricasEvaluacionHandler(repo);
        var resultado = await handler.Handle(
            new GetMetricasEvaluacionQuery(EvaluacionId, InstitucionId),
            CancellationToken.None);

        Assert.Equal(TipoResultado.Ok, resultado.Tipo);
        var metricas = resultado.Valor!;

        Assert.Equal(2, metricas.CantidadEntregas);
        Assert.Equal(7m, metricas.PromedioExamen);
        Assert.Equal(10m, metricas.PuntajeMaximoExamen);

        Assert.NotNull(metricas.PreguntaMasDificil);
        Assert.Equal(PreguntaMcId, metricas.PreguntaMasDificil!.PreguntaId);
        Assert.Equal(2m, metricas.PreguntaMasDificil.PuntajePromedio);

        Assert.Equal(2, metricas.Alumnos.Count);
        var metricaAlumno1 = metricas.Alumnos.Single(a => a.AlumnoId == alumno1);
        Assert.Equal(8m, metricaAlumno1.PuntajeObtenido);
        Assert.Equal(1m, metricaAlumno1.DiferenciaConPromedio);
        Assert.Equal("PorEncimaDelPromedio", metricaAlumno1.ComparacionConPromedio);

        var metricaAlumno2 = metricas.Alumnos.Single(a => a.AlumnoId == alumno2);
        Assert.Equal(6m, metricaAlumno2.PuntajeObtenido);
        Assert.Equal(-1m, metricaAlumno2.DiferenciaConPromedio);
        Assert.Equal("PorDebajoDelPromedio", metricaAlumno2.ComparacionConPromedio);
    }

    private static Evaluacion CrearEvaluacion() => new()
    {
        Id = EvaluacionId,
        InstitucionId = InstitucionId,
        Titulo = "Parcial de Prueba",
        Tipo = "Examen",
        PreguntasJson = $$$"""
        {"version":1,"preguntas":[
          {"id":"{{{PreguntaMcId}}}","enunciado":"Pregunta MC","tipo":"OpcionMultiple","puntajeMaximo":4,
           "opciones":[
             {"id":"{{{OpcionCorrectaId}}}","texto":"Correcta","esCorrecta":true},
             {"id":"{{{OpcionIncorrectaId}}}","texto":"Incorrecta","esCorrecta":false}
           ]},
          {"id":"{{{PreguntaAbiertaId}}}","enunciado":"Pregunta Abierta","tipo":"Abierta","puntajeMaximo":6}
        ]}
        """
    };

    private sealed class FakeResultadosRepositorio : IResultadosRepositorio
    {
        private readonly Evaluacion _evaluacion;
        public List<EntregaExamen> Entregas { get; } = new();
        public List<CalificacionSugerida> CalificacionesIa { get; } = new();
        public List<CalificacionDocente> CalificacionesDocente { get; } = new();

        public FakeResultadosRepositorio(Evaluacion evaluacion) => _evaluacion = evaluacion;

        public Task<Evaluacion?> ObtenerEvaluacionAsync(Guid evaluacionId, Guid institucionId, CancellationToken ct) =>
            Task.FromResult<Evaluacion?>(_evaluacion.Id == evaluacionId && _evaluacion.InstitucionId == institucionId ? _evaluacion : null);

        public Task<List<EntregaExamen>> ListarEntregasPorEvaluacionAsync(Guid evaluacionId, Guid institucionId, CancellationToken ct) =>
            Task.FromResult(Entregas.Where(e => e.EvaluacionId == evaluacionId && e.InstitucionId == institucionId).ToList());

        public Task<EntregaExamen?> ObtenerEntregaPorIdAsync(Guid evaluacionId, Guid entregaId, Guid institucionId, CancellationToken ct) =>
            Task.FromResult(Entregas.FirstOrDefault(e => e.Id == entregaId && e.EvaluacionId == evaluacionId && e.InstitucionId == institucionId));

        public Task<List<CalificacionSugerida>> ListarCalificacionesPorEntregasAsync(List<Guid> entregaIds, CancellationToken ct) =>
            Task.FromResult(CalificacionesIa.Where(c => entregaIds.Contains(c.EntregaId)).ToList());

        public Task<List<CalificacionSugerida>> ListarCalificacionesPorEntregaAsync(Guid entregaId, CancellationToken ct) =>
            Task.FromResult(CalificacionesIa.Where(c => c.EntregaId == entregaId).ToList());

        public Task<CalificacionDocente?> ObtenerCalificacionDocenteAsync(Guid entregaId, Guid preguntaId, CancellationToken ct) =>
            Task.FromResult(CalificacionesDocente.FirstOrDefault(c => c.EntregaId == entregaId && c.PreguntaId == preguntaId));

        public Task<List<CalificacionDocente>> ListarCalificacionesDocentePorEntregaAsync(Guid entregaId, CancellationToken ct) =>
            Task.FromResult(CalificacionesDocente.Where(c => c.EntregaId == entregaId).ToList());

        public Task<List<CalificacionDocente>> ListarCalificacionesDocentePorEntregasAsync(List<Guid> entregaIds, CancellationToken ct) =>
            Task.FromResult(CalificacionesDocente.Where(c => entregaIds.Contains(c.EntregaId)).ToList());

        public Task GuardarCalificacionDocenteAsync(CalificacionDocente calificacion, CancellationToken ct)
        {
            CalificacionesDocente.RemoveAll(c => c.EntregaId == calificacion.EntregaId && c.PreguntaId == calificacion.PreguntaId);
            CalificacionesDocente.Add(calificacion);
            return Task.CompletedTask;
        }

        public Task GuardarCambiosAsync(CancellationToken ct) => Task.CompletedTask;

        public Task<List<(EntregaExamen Entrega, Evaluacion Evaluacion)>> ListarEntregasPorAlumnoAsync(Guid alumnoId, CancellationToken ct) =>
            Task.FromResult(new List<(EntregaExamen Entrega, Evaluacion Evaluacion)>());
    }
}