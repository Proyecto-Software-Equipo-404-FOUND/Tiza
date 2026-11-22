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
    public async Task Handle_SinEntregasAprobadas_DevuelvePromedioYPreguntaMasDificilNullYListasVacias()
    {
        var repo = new FakeResultadosRepositorio(CrearEvaluacion());
        var handler = new GetMetricasEvaluacionHandler(repo);

        var resultado = await handler.Handle(
            new GetMetricasEvaluacionQuery(EvaluacionId, InstitucionId),
            CancellationToken.None);

        Assert.Equal(TipoResultado.Ok, resultado.Tipo);
        Assert.NotNull(resultado.Valor);
        Assert.Equal(0, resultado.Valor!.CantidadAprobadas);
        Assert.Null(resultado.Valor.Promedio);
        Assert.Null(resultado.Valor.PreguntaMasDificilId);
        Assert.Equal(10m, resultado.Valor.PuntajeMaximo);
        Assert.Empty(resultado.Valor.Preguntas);
        Assert.Empty(resultado.Valor.Alumnos);
    }

    [Fact]
    public async Task Handle_EntregasSinAprobarNoCuentan_SoloConsideraAprobadas()
    {
        var alumnoAprobado = Guid.NewGuid();
        var alumnoSugerida = Guid.NewGuid();
        var alumnoPendiente = Guid.NewGuid();
        var alumnoEditada = Guid.NewGuid();

        var entregaAprobadaId = Guid.NewGuid();
        var entregaSugeridaId = Guid.NewGuid();
        var entregaPendienteId = Guid.NewGuid();
        var entregaEditadaId = Guid.NewGuid();

        var repo = new FakeResultadosRepositorio(CrearEvaluacion());

        // Única entrega Aprobada: MC correcta (2m) + abierta docente (6m) = 8m
        repo.Entregas.Add(new EntregaExamen
        {
            Id = entregaAprobadaId,
            InstitucionId = InstitucionId,
            EvaluacionId = EvaluacionId,
            SalaId = Guid.NewGuid(),
            AlumnoId = alumnoAprobado,
            Estado = EstadoEntregaExamen.CorreccionManual,
            EstadoNota = EstadoNotaEntrega.Aprobada,
            FechaEntrega = DateTimeOffset.UtcNow.AddMinutes(-15),
            RespuestasJson = JsonSerializer.Serialize(new object[]
            {
                new { preguntaId = PreguntaMcId, opcionElegidaId = OpcionCorrectaId.ToString() },
                new { preguntaId = PreguntaAbiertaId, respuesta = "Respuesta aprobada" }
            })
        });
        repo.CalificacionesDocente.Add(new CalificacionDocente
        {
            Id = Guid.NewGuid(),
            InstitucionId = InstitucionId,
            EntregaId = entregaAprobadaId,
            PreguntaId = PreguntaAbiertaId,
            PuntajeDocente = 6m,
            FechaEdicionUtc = DateTimeOffset.UtcNow
        });

        // Entrega con nota Sugerida (NO aprobada -> no debe contar)
        repo.Entregas.Add(new EntregaExamen
        {
            Id = entregaSugeridaId,
            InstitucionId = InstitucionId,
            EvaluacionId = EvaluacionId,
            SalaId = Guid.NewGuid(),
            AlumnoId = alumnoSugerida,
            Estado = EstadoEntregaExamen.Corregida,
            EstadoNota = EstadoNotaEntrega.Sugerida,
            FechaEntrega = DateTimeOffset.UtcNow.AddMinutes(-10),
            RespuestasJson = JsonSerializer.Serialize(new object[]
            {
                new { preguntaId = PreguntaMcId, opcionElegidaId = OpcionIncorrectaId.ToString() },
                new { preguntaId = PreguntaAbiertaId, respuesta = "Respuesta sugerida" }
            })
        });
        repo.CalificacionesIa.Add(new CalificacionSugerida
        {
            Id = Guid.NewGuid(),
            InstitucionId = InstitucionId,
            EntregaId = entregaSugeridaId,
            PreguntaId = PreguntaAbiertaId,
            NotaSugerida = 2m,
            Justificacion = "IA",
            FechaRegistro = DateTimeOffset.UtcNow
        });

        // Entrega Pendiente (NO aprobada -> no debe contar)
        repo.Entregas.Add(new EntregaExamen
        {
            Id = entregaPendienteId,
            InstitucionId = InstitucionId,
            EvaluacionId = EvaluacionId,
            SalaId = Guid.NewGuid(),
            AlumnoId = alumnoPendiente,
            Estado = EstadoEntregaExamen.Corregida,
            EstadoNota = EstadoNotaEntrega.Sugerida,
            FechaEntrega = DateTimeOffset.UtcNow.AddMinutes(-5),
            RespuestasJson = "[]"
        });

        // Entrega Editada pero aún no Aprobada (NO debe contar)
        repo.Entregas.Add(new EntregaExamen
        {
            Id = entregaEditadaId,
            InstitucionId = InstitucionId,
            EvaluacionId = EvaluacionId,
            SalaId = Guid.NewGuid(),
            AlumnoId = alumnoEditada,
            Estado = EstadoEntregaExamen.CorreccionManual,
            EstadoNota = EstadoNotaEntrega.Editada,
            FechaEntrega = DateTimeOffset.UtcNow.AddMinutes(-2),
            RespuestasJson = "[]"
        });

        var handler = new GetMetricasEvaluacionHandler(repo);
        var resultado = await handler.Handle(
            new GetMetricasEvaluacionQuery(EvaluacionId, InstitucionId),
            CancellationToken.None);

        Assert.Equal(TipoResultado.Ok, resultado.Tipo);
        var metricas = resultado.Valor!;

        Assert.Equal(1, metricas.CantidadAprobadas);
        Assert.Equal(8m, metricas.Promedio);
        Assert.Single(metricas.Alumnos);
        Assert.Equal(alumnoAprobado, metricas.Alumnos[0].AlumnoId);
        Assert.Equal(8m, metricas.Alumnos[0].NotaFinal);
        Assert.Equal(0m, metricas.Alumnos[0].DiferenciaConPromedio);
    }

    [Fact]
    public async Task Handle_ConEntregasAprobadas_CalculaPromedioPreguntaMasDificilPorPorcentajeYAlumnos()
    {
        var alumno1 = Guid.NewGuid();
        var alumno2 = Guid.NewGuid();
        var entrega1Id = Guid.NewGuid();
        var entrega2Id = Guid.NewGuid();

        // Pregunta MC: puntajeMaximo = 2
        // Pregunta Abierta: puntajeMaximo = 8
        var repo = new FakeResultadosRepositorio(CrearEvaluacion());

        // Alumno 1 (Aprobada): MC correcta (2/2 = 100%) + abierta docente (4/8 = 50%) -> 6m
        repo.Entregas.Add(new EntregaExamen
        {
            Id = entrega1Id,
            InstitucionId = InstitucionId,
            EvaluacionId = EvaluacionId,
            SalaId = Guid.NewGuid(),
            AlumnoId = alumno1,
            Estado = EstadoEntregaExamen.CorreccionManual,
            EstadoNota = EstadoNotaEntrega.Aprobada,
            FechaEntrega = DateTimeOffset.UtcNow.AddMinutes(-10),
            RespuestasJson = JsonSerializer.Serialize(new object[]
            {
                new { preguntaId = PreguntaMcId, opcionElegidaId = OpcionCorrectaId.ToString() },
                new { preguntaId = PreguntaAbiertaId, respuesta = "Respuesta 1" }
            })
        });
        repo.CalificacionesDocente.Add(new CalificacionDocente
        {
            Id = Guid.NewGuid(),
            InstitucionId = InstitucionId,
            EntregaId = entrega1Id,
            PreguntaId = PreguntaAbiertaId,
            PuntajeDocente = 4m,
            FechaEdicionUtc = DateTimeOffset.UtcNow
        });

        // Alumno 2 (Aprobada): MC correcta (2/2 = 100%) + abierta docente (2/8 = 25%) -> 4m
        repo.Entregas.Add(new EntregaExamen
        {
            Id = entrega2Id,
            InstitucionId = InstitucionId,
            EvaluacionId = EvaluacionId,
            SalaId = Guid.NewGuid(),
            AlumnoId = alumno2,
            Estado = EstadoEntregaExamen.CorreccionManual,
            EstadoNota = EstadoNotaEntrega.Aprobada,
            FechaEntrega = DateTimeOffset.UtcNow.AddMinutes(-5),
            RespuestasJson = JsonSerializer.Serialize(new object[]
            {
                new { preguntaId = PreguntaMcId, opcionElegidaId = OpcionCorrectaId.ToString() },
                new { preguntaId = PreguntaAbiertaId, respuesta = "Respuesta 2" }
            })
        });
        repo.CalificacionesDocente.Add(new CalificacionDocente
        {
            Id = Guid.NewGuid(),
            InstitucionId = InstitucionId,
            EntregaId = entrega2Id,
            PreguntaId = PreguntaAbiertaId,
            PuntajeDocente = 2m,
            FechaEdicionUtc = DateTimeOffset.UtcNow
        });

        // Puntajes: Alumno 1 = 6m, Alumno 2 = 4m -> Promedio = 5m
        // Pregunta MC: promedio 2.0 / 2 = 100%
        // Pregunta Abierta: promedio 3.0 / 8 = 37.5% (mayor puntaje absoluto 3.0 > 2.0, pero MENOR porcentaje -> es la más difícil)
        var handler = new GetMetricasEvaluacionHandler(repo);
        var resultado = await handler.Handle(
            new GetMetricasEvaluacionQuery(EvaluacionId, InstitucionId),
            CancellationToken.None);

        Assert.Equal(TipoResultado.Ok, resultado.Tipo);
        var metricas = resultado.Valor!;

        Assert.Equal(2, metricas.CantidadAprobadas);
        Assert.Equal(5m, metricas.Promedio);
        Assert.Equal(10m, metricas.PuntajeMaximo);

        Assert.Equal(PreguntaAbiertaId, metricas.PreguntaMasDificilId);

        Assert.Equal(2, metricas.Preguntas.Count);
        var metricaMc = metricas.Preguntas.Single(p => p.PreguntaId == PreguntaMcId);
        Assert.Equal("Pregunta MC", metricaMc.Enunciado);
        Assert.Equal(2m, metricaMc.PuntajeMaximo);
        Assert.Equal(2m, metricaMc.Promedio);
        Assert.Equal(100m, metricaMc.Porcentaje);

        var metricaAbierta = metricas.Preguntas.Single(p => p.PreguntaId == PreguntaAbiertaId);
        Assert.Equal("Pregunta Abierta", metricaAbierta.Enunciado);
        Assert.Equal(8m, metricaAbierta.PuntajeMaximo);
        Assert.Equal(3m, metricaAbierta.Promedio);
        Assert.Equal(37.5m, metricaAbierta.Porcentaje);

        Assert.Equal(2, metricas.Alumnos.Count);
        var metricaAlumno1 = metricas.Alumnos.Single(a => a.AlumnoId == alumno1);
        Assert.Equal(6m, metricaAlumno1.NotaFinal);
        Assert.Equal(1m, metricaAlumno1.DiferenciaConPromedio);

        var metricaAlumno2 = metricas.Alumnos.Single(a => a.AlumnoId == alumno2);
        Assert.Equal(4m, metricaAlumno2.NotaFinal);
        Assert.Equal(-1m, metricaAlumno2.DiferenciaConPromedio);
    }

    private static Evaluacion CrearEvaluacion() => new()
    {
        Id = EvaluacionId,
        InstitucionId = InstitucionId,
        Titulo = "Parcial de Prueba",
        Tipo = "Examen",
        PreguntasJson = $$$"""
        {"version":1,"preguntas":[
          {"id":"{{{PreguntaMcId}}}","enunciado":"Pregunta MC","tipo":"OpcionMultiple","puntajeMaximo":2,
           "opciones":[
             {"id":"{{{OpcionCorrectaId}}}","texto":"Correcta","esCorrecta":true},
             {"id":"{{{OpcionIncorrectaId}}}","texto":"Incorrecta","esCorrecta":false}
           ]},
          {"id":"{{{PreguntaAbiertaId}}}","enunciado":"Pregunta Abierta","tipo":"Abierta","puntajeMaximo":8}
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