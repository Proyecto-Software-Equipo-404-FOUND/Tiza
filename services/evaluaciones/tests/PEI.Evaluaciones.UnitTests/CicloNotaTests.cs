using System.Text.Json;
using PEI.Evaluaciones.Application.Correccion;
using PEI.Evaluaciones.Application.Evaluaciones;
using PEI.Evaluaciones.Domain.Entities;
using Xunit;

namespace PEI.Evaluaciones.UnitTests;

// S3-EV-03 (#224): Tests del ciclo de la nota (editar, aprobar, inmutabilidad y permisos).
public class CicloNotaTests
{
    private static readonly Guid InstitucionId = Guid.Parse("11111111-1111-4111-8111-111111111111");
    private static readonly Guid DocenteDuenoId = Guid.Parse("22222222-2222-4222-8222-222222222222");
    private static readonly Guid DocenteAjenoId = Guid.Parse("33333333-3333-4333-8333-333333333333");
    private static readonly Guid EvaluacionId = Guid.Parse("44444444-4444-4444-8444-444444444444");
    private static readonly Guid PreguntaAbiertaId = Guid.Parse("aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa");
    private static readonly Guid PreguntaMultipleId = Guid.Parse("bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb");
    private static readonly Guid OpcionCorrectaId = Guid.Parse("cccccccc-cccc-4ccc-8ccc-cccccccccccc");

    // ==========================================
    // 1. EDICIÓN DE NOTA (PUT .../nota)
    // ==========================================

    [Fact]
    public async Task EditarNota_DocenteDueno_PuntajeValido_PasaAEstadoEditadaYGuardaPuntaje()
    {
        var (repo, entrega) = Escenario();
        var handler = new EditarNotaRespuestaHandler(repo);

        var resultado = await handler.Handle(new EditarNotaRespuestaCommand(
            EvaluacionId, entrega.Id, PreguntaAbiertaId, InstitucionId, DocenteDuenoId, "Docente",
            Puntaje: 4.5m, Comentario: "Excelente respuesta", DesglosePorCriterio: null), CancellationToken.None);

        Assert.Equal(TipoResultado.Ok, resultado.Tipo);
        Assert.Equal(4.5m, resultado.Valor!.PuntajeDocente);
        Assert.Equal("Editada", resultado.Valor.EstadoCorreccion);
        Assert.Equal(EstadoNotaEntrega.Editada, entrega.EstadoNota);
        Assert.Single(repo.CalificacionesDocente);
    }

    [Fact]
    public async Task EditarNota_PuntajeMayorAlMaximo_DevuelveInvalido400()
    {
        var (repo, entrega) = Escenario();
        var handler = new EditarNotaRespuestaHandler(repo);

        // La pregunta abierta tiene puntajeMaximo = 5
        var resultado = await handler.Handle(new EditarNotaRespuestaCommand(
            EvaluacionId, entrega.Id, PreguntaAbiertaId, InstitucionId, DocenteDuenoId, "Docente",
            Puntaje: 6m, Comentario: null, DesglosePorCriterio: null), CancellationToken.None);

        Assert.Equal(TipoResultado.Invalido, resultado.Tipo);
        Assert.Contains("El puntaje tiene que estar entre 0 y 5", resultado.Errores[0]);
    }

    [Fact]
    public async Task EditarNota_PuntajeNegativo_DevuelveInvalido400()
    {
        var (repo, entrega) = Escenario();
        var handler = new EditarNotaRespuestaHandler(repo);

        var resultado = await handler.Handle(new EditarNotaRespuestaCommand(
            EvaluacionId, entrega.Id, PreguntaAbiertaId, InstitucionId, DocenteDuenoId, "Docente",
            Puntaje: -1m, Comentario: null, DesglosePorCriterio: null), CancellationToken.None);

        Assert.Equal(TipoResultado.Invalido, resultado.Tipo);
    }

    [Fact]
    public async Task EditarNota_DesgloseNoCoincideConPuntaje_DevuelveInvalido400()
    {
        var (repo, entrega) = Escenario();
        var handler = new EditarNotaRespuestaHandler(repo);

        var desglose = new List<DesgloseItemDto>
        {
            new("Claridad", 2m),
            new("Concepto", 1m) // Suma 3m, pero el puntaje es 4m
        };

        var resultado = await handler.Handle(new EditarNotaRespuestaCommand(
            EvaluacionId, entrega.Id, PreguntaAbiertaId, InstitucionId, DocenteDuenoId, "Docente",
            Puntaje: 4m, Comentario: null, DesglosePorCriterio: desglose), CancellationToken.None);

        Assert.Equal(TipoResultado.Invalido, resultado.Tipo);
        Assert.Contains("coincidir", resultado.Errores[0]);
    }

    [Fact]
    public async Task EditarNota_EntregaYaAprobada_DevuelveConflicto409()
    {
        var (repo, entrega) = Escenario();
        entrega.EstadoNota = EstadoNotaEntrega.Aprobada;
        entrega.FechaAprobacionUtc = DateTimeOffset.UtcNow;
        var handler = new EditarNotaRespuestaHandler(repo);

        var resultado = await handler.Handle(new EditarNotaRespuestaCommand(
            EvaluacionId, entrega.Id, PreguntaAbiertaId, InstitucionId, DocenteDuenoId, "Docente",
            Puntaje: 4m, Comentario: null, DesglosePorCriterio: null), CancellationToken.None);

        Assert.Equal(TipoResultado.Conflicto, resultado.Tipo);
        Assert.Contains("aprobada", resultado.Errores[0]);
    }

    // ==========================================
    // 2. APROBACIÓN DE NOTA (POST .../aprobacion)
    // ==========================================

    [Fact]
    public async Task AprobarNota_DocenteDueno_CalculaNotaFinalSumaPuntajesYGuardaFechaYDocente()
    {
        var (repo, entrega) = Escenario();
        // Calificación sugerida por IA en la abierta: 3m
        repo.CalificacionesIa.Add(new CalificacionSugerida
        {
            Id = Guid.NewGuid(),
            InstitucionId = InstitucionId,
            EntregaId = entrega.Id,
            PreguntaId = PreguntaAbiertaId,
            NotaSugerida = 3m,
            Justificacion = "Bien",
            FechaRegistro = DateTimeOffset.UtcNow
        });

        var handler = new AprobarNotaEntregaHandler(repo);
        var resultado = await handler.Handle(new AprobarNotaEntregaCommand(
            EvaluacionId, entrega.Id, InstitucionId, DocenteDuenoId, "Docente"), CancellationToken.None);

        Assert.Equal(TipoResultado.Ok, resultado.Tipo);
        // MultipleChoice correcta (1m) + IA abierta (3m) = 4m
        Assert.Equal(4m, resultado.Valor!.NotaFinal);
        Assert.Equal(6m, resultado.Valor.PuntajeMaximo); // 1 + 5 = 6
        Assert.Equal(DocenteDuenoId, resultado.Valor.AprobadaPorId);
        Assert.Equal(EstadoNotaEntrega.Aprobada, entrega.EstadoNota);
        Assert.NotNull(entrega.FechaAprobacionUtc);
    }

    [Fact]
    public async Task AprobarNota_PreguntasAbiertasSinCorregir_DevuelveConflicto409FaltanCorrecciones()
    {
        var (repo, entrega) = Escenario(); // Entrega respondió la abierta pero no tiene IA ni edición
        var handler = new AprobarNotaEntregaHandler(repo);

        var resultado = await handler.Handle(new AprobarNotaEntregaCommand(
            EvaluacionId, entrega.Id, InstitucionId, DocenteDuenoId, "Docente"), CancellationToken.None);

        Assert.Equal(TipoResultado.Conflicto, resultado.Tipo);
        Assert.Contains("abiertas sin corregir", resultado.Errores[0]);
    }

    [Fact]
    public async Task AprobarNota_PreguntaAbiertaEditadaPorDocente_ApruebaSinErrorAunqueFalteIa()
    {
        var (repo, entrega) = Escenario();
        // El docente puso su puntaje manual directamente
        repo.CalificacionesDocente.Add(new CalificacionDocente
        {
            Id = Guid.NewGuid(),
            InstitucionId = InstitucionId,
            EntregaId = entrega.Id,
            PreguntaId = PreguntaAbiertaId,
            PuntajeDocente = 5m,
            FechaEdicionUtc = DateTimeOffset.UtcNow
        });

        var handler = new AprobarNotaEntregaHandler(repo);
        var resultado = await handler.Handle(new AprobarNotaEntregaCommand(
            EvaluacionId, entrega.Id, InstitucionId, DocenteDuenoId, "Docente"), CancellationToken.None);

        Assert.Equal(TipoResultado.Ok, resultado.Tipo);
        // Multiple choice (1m) + Docente (5m) = 6m
        Assert.Equal(6m, resultado.Valor!.NotaFinal);
    }

    [Fact]
    public async Task AprobarNota_EntregaYaAprobada_DevuelveConflicto409()
    {
        var (repo, entrega) = Escenario();
        entrega.EstadoNota = EstadoNotaEntrega.Aprobada;
        entrega.FechaAprobacionUtc = DateTimeOffset.UtcNow;
        var handler = new AprobarNotaEntregaHandler(repo);

        var resultado = await handler.Handle(new AprobarNotaEntregaCommand(
            EvaluacionId, entrega.Id, InstitucionId, DocenteDuenoId, "Docente"), CancellationToken.None);

        Assert.Equal(TipoResultado.Conflicto, resultado.Tipo);
    }

    // ==========================================
    // 3. PERMISOS Y ROLES (403 Y 404)
    // ==========================================

    [Fact]
    public async Task EditarNota_DocenteNoDueno_Devuelve403()
    {
        var (repo, entrega) = Escenario();
        var handler = new EditarNotaRespuestaHandler(repo);

        var resultado = await handler.Handle(new EditarNotaRespuestaCommand(
            EvaluacionId, entrega.Id, PreguntaAbiertaId, InstitucionId, DocenteAjenoId, "Docente",
            Puntaje: 4m, Comentario: null, DesglosePorCriterio: null), CancellationToken.None);

        Assert.Equal(TipoResultado.Invalido, resultado.Tipo);
        Assert.Contains("creó el examen", resultado.Errores[0]);
    }

    [Fact]
    public async Task EditarNota_Administrador_Devuelve403()
    {
        var (repo, entrega) = Escenario();
        var handler = new EditarNotaRespuestaHandler(repo);

        var resultado = await handler.Handle(new EditarNotaRespuestaCommand(
            EvaluacionId, entrega.Id, PreguntaAbiertaId, InstitucionId, DocenteDuenoId, "Administrador",
            Puntaje: 4m, Comentario: null, DesglosePorCriterio: null), CancellationToken.None);

        Assert.Equal(TipoResultado.Invalido, resultado.Tipo);
        Assert.Contains("Solo docentes", resultado.Errores[0]);
    }

    [Fact]
    public async Task AprobarNota_DocenteNoDueno_Devuelve403()
    {
        var (repo, entrega) = Escenario();
        var handler = new AprobarNotaEntregaHandler(repo);

        var resultado = await handler.Handle(new AprobarNotaEntregaCommand(
            EvaluacionId, entrega.Id, InstitucionId, DocenteAjenoId, "Docente"), CancellationToken.None);

        Assert.Equal(TipoResultado.Invalido, resultado.Tipo);
    }

    [Fact]
    public async Task EditarNota_OtraInstitucion_DevuelveNoEncontrado404()
    {
        var (repo, entrega) = Escenario();
        var handler = new EditarNotaRespuestaHandler(repo);

        var resultado = await handler.Handle(new EditarNotaRespuestaCommand(
            EvaluacionId, entrega.Id, PreguntaAbiertaId, Guid.NewGuid(), DocenteDuenoId, "Docente",
            Puntaje: 4m, Comentario: null, DesglosePorCriterio: null), CancellationToken.None);

        Assert.Equal(TipoResultado.NoEncontrado, resultado.Tipo);
    }

    // ==========================================
    // 4. CONSULTAR RESULTADOS Y DTOS
    // ==========================================

    [Fact]
    public async Task ConsultarResultados_EntregaAprobada_DevuelveEstadoAprobadaYFecha()
    {
        var (repo, entrega) = Escenario();
        var fecha = DateTimeOffset.UtcNow;
        entrega.EstadoNota = EstadoNotaEntrega.Aprobada;
        entrega.NotaFinal = 8.5m;
        entrega.FechaAprobacionUtc = fecha;

        var handler = new ConsultarResultadosHandler(repo);
        var resultado = await handler.Handle(
            new ListarEntregasEvaluacionQuery(EvaluacionId, InstitucionId), CancellationToken.None);

        Assert.Equal(TipoResultado.Ok, resultado.Tipo);
        var resumen = Assert.Single(resultado.Valor!);
        Assert.Equal("Aprobada", resumen.EstadoNota);
        Assert.Equal(8.5m, resumen.NotaFinal);
        Assert.Equal(fecha, resumen.FechaAprobacionUtc);
    }

    // ==========================================
    // HELPERS Y REPOSITORIO MOCK
    // ==========================================

    private static (FakeResultadosRepositorio, EntregaExamen) Escenario()
    {
        var evaluacion = new Evaluacion
        {
            Id = EvaluacionId,
            InstitucionId = InstitucionId,
            DocenteId = DocenteDuenoId,
            Titulo = "Parcial",
            Tipo = "Examen",
            PreguntasJson = $$$"""
            {"version":1,"preguntas":[
              {"id":"{{{PreguntaMultipleId}}}","enunciado":"¿Capital de Francia?","tipo":"OpcionMultiple","puntajeMaximo":1,
               "opciones":[{"id":"{{{OpcionCorrectaId}}}","texto":"París","esCorrecta":true}]},
              {"id":"{{{PreguntaAbiertaId}}}","enunciado":"Explicá qué es un sismo.","tipo":"Abierta","puntajeMaximo":5}
            ]}
            """
        };

        var entrega = new EntregaExamen
        {
            Id = Guid.NewGuid(),
            InstitucionId = InstitucionId,
            EvaluacionId = EvaluacionId,
            SalaId = Guid.NewGuid(),
            AlumnoId = Guid.NewGuid(),
            Estado = EstadoEntregaExamen.PendienteCorreccion,
            EstadoNota = EstadoNotaEntrega.Sugerida,
            NotaMultipleChoice = 1,
            RespuestasJson = JsonSerializer.Serialize(new object[]
            {
                new { preguntaId = PreguntaMultipleId, opcionElegidaId = OpcionCorrectaId.ToString(), secuencia = 1 },
                new { preguntaId = PreguntaAbiertaId, respuesta = "Un movimiento tectónico", secuencia = 2 }
            }),
            AlertasFocoJson = "[]",
            FechaEntrega = DateTimeOffset.UtcNow
        };

        var repo = new FakeResultadosRepositorio(evaluacion, entrega);
        return (repo, entrega);
    }

    private class FakeResultadosRepositorio : IResultadosRepositorio
    {
        public Evaluacion Evaluacion { get; }
        public List<EntregaExamen> Entregas { get; } = new();
        public List<CalificacionSugerida> CalificacionesIa { get; } = new();
        public List<CalificacionDocente> CalificacionesDocente { get; } = new();

        public FakeResultadosRepositorio(Evaluacion evaluacion, EntregaExamen entrega)
        {
            Evaluacion = evaluacion;
            Entregas.Add(entrega);
        }

        public Task<Evaluacion?> ObtenerEvaluacionAsync(Guid evaluacionId, Guid institucionId, CancellationToken ct) =>
            Task.FromResult<Evaluacion?>(Evaluacion.Id == evaluacionId && Evaluacion.InstitucionId == institucionId ? Evaluacion : null);

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

        public Task<List<(EntregaExamen Entrega, Evaluacion Evaluacion)>> ListarEntregasPorAlumnoAsync(
    Guid alumnoId,
    CancellationToken ct)
    => Task.FromResult(new List<(EntregaExamen Entrega, Evaluacion Evaluacion)>());
    }
}