using PEI.Evaluaciones.Application.Evaluaciones;
using PEI.Evaluaciones.Domain.Entities;
using Xunit;

namespace PEI.Evaluaciones.UnitTests;

// S3-EV-05: el alumno ve sus errores en el examen corregido
public class MiExamenDetalleTests
{
    private static readonly Guid PreguntaMc = Guid.NewGuid();
    private static readonly Guid OpcionBien = Guid.NewGuid();
    private static readonly Guid OpcionMal = Guid.NewGuid();
    private static readonly Guid PreguntaAbierta1 = Guid.NewGuid();
    private static readonly Guid PreguntaAbierta2 = Guid.NewGuid();

    private sealed class RepoFalso : IResultadosRepositorio
    {
        public List<(EntregaExamen Entrega, Evaluacion Evaluacion)> Filas { get; } = new();
        public List<CalificacionSugerida> Sugeridas { get; } = new();
        public List<CalificacionDocente> Docente { get; } = new();

        public Task<List<(EntregaExamen Entrega, Evaluacion Evaluacion)>> ListarEntregasPorAlumnoAsync(Guid alumnoId, CancellationToken ct)
            => Task.FromResult(Filas.Where(x => x.Entrega.AlumnoId == alumnoId).ToList());
        public Task<List<CalificacionSugerida>> ListarCalificacionesPorEntregaAsync(Guid entregaId, CancellationToken ct)
            => Task.FromResult(Sugeridas.Where(c => c.EntregaId == entregaId).ToList());
        public Task<List<CalificacionDocente>> ListarCalificacionesDocentePorEntregaAsync(Guid entregaId, CancellationToken ct)
            => Task.FromResult(Docente.Where(c => c.EntregaId == entregaId).ToList());

        public Task<Evaluacion?> ObtenerEvaluacionAsync(Guid evaluacionId, Guid institucionId, CancellationToken ct) => Task.FromResult<Evaluacion?>(null);
        public Task<List<EntregaExamen>> ListarEntregasPorEvaluacionAsync(Guid evaluacionId, Guid institucionId, CancellationToken ct) => Task.FromResult(new List<EntregaExamen>());
        public Task<EntregaExamen?> ObtenerEntregaPorIdAsync(Guid evaluacionId, Guid entregaId, Guid institucionId, CancellationToken ct) => Task.FromResult<EntregaExamen?>(null);
        public Task<List<CalificacionSugerida>> ListarCalificacionesPorEntregasAsync(List<Guid> entregaIds, CancellationToken ct) => Task.FromResult(new List<CalificacionSugerida>());
        public Task<CalificacionDocente?> ObtenerCalificacionDocenteAsync(Guid entregaId, Guid preguntaId, CancellationToken ct) => Task.FromResult<CalificacionDocente?>(null);
        public Task<List<CalificacionDocente>> ListarCalificacionesDocentePorEntregasAsync(List<Guid> entregaIds, CancellationToken ct) => Task.FromResult(new List<CalificacionDocente>());
        public Task GuardarCalificacionDocenteAsync(CalificacionDocente calificacion, CancellationToken ct) => Task.CompletedTask;
        public Task GuardarCambiosAsync(CancellationToken ct) => Task.CompletedTask;
    }

    private static (RepoFalso Repo, Guid AlumnoId, Guid EntregaId) Preparar(EstadoNotaEntrega estadoNota = EstadoNotaEntrega.Aprobada)
    {
        var alumnoId = Guid.NewGuid();
        var evaluacionId = Guid.NewGuid();
        var entregaId = Guid.NewGuid();
        var preguntas = $$"""
            {"preguntas":[
              {"id":"{{PreguntaMc}}","enunciado":"¿2+2?","tipo":"OpcionMultiple","puntajeMaximo":2,
               "opciones":[{"id":"{{OpcionBien}}","texto":"4","esCorrecta":true},{"id":"{{OpcionMal}}","texto":"5","esCorrecta":false}]},
              {"id":"{{PreguntaAbierta1}}","enunciado":"Explicá la fotosíntesis","tipo":"Abierta","puntajeMaximo":4},
              {"id":"{{PreguntaAbierta2}}","enunciado":"Explicá la respiración","tipo":"Abierta","puntajeMaximo":4}
            ]}
            """;
        var respuestas = $$"""
            [{"preguntaId":"{{PreguntaMc}}","opcionElegidaId":"{{OpcionMal}}"},
             {"preguntaId":"{{PreguntaAbierta1}}","respuesta":"Las plantas usan luz"},
             {"preguntaId":"{{PreguntaAbierta2}}","respuesta":"Las células usan oxígeno"}]
            """;

        var repo = new RepoFalso();
        repo.Filas.Add((
            new EntregaExamen
            {
                Id = entregaId, EvaluacionId = evaluacionId, AlumnoId = alumnoId, FechaEntrega = DateTimeOffset.UtcNow,
                RespuestasJson = respuestas, AlertasFocoJson = "[]", EstadoNota = estadoNota, NotaFinal = 5m, PuntajeMaximo = 10m
            },
            new Evaluacion { Id = evaluacionId, Titulo = "Parcial 1", PreguntasJson = preguntas }));
        repo.Sugeridas.Add(new CalificacionSugerida { EntregaId = entregaId, PreguntaId = PreguntaAbierta1, NotaSugerida = 3m, Justificacion = "Faltó mencionar el CO2." });
        repo.Sugeridas.Add(new CalificacionSugerida { EntregaId = entregaId, PreguntaId = PreguntaAbierta2, NotaSugerida = 1m, Justificacion = "Muy incompleta." });
        repo.Docente.Add(new CalificacionDocente { EntregaId = entregaId, PreguntaId = PreguntaAbierta2, PuntajeDocente = 2m, ComentarioDocente = "Comentario interno: le subo un punto." });
        return (repo, alumnoId, entregaId);
    }

    [Fact]
    public async Task NotaAprobada_MuestraCadaPreguntaSinElComentarioDelDocente()
    {
        var (repo, alumnoId, entregaId) = Preparar();
        var handler = new ConsultarResultadosHandler(repo);

        var resultado = await handler.Handle(new ObtenerMiExamenDetalleQuery(entregaId, alumnoId), CancellationToken.None);

        Assert.Equal(TipoResultado.Ok, resultado.Tipo);
        var detalle = resultado.Valor!;
        Assert.Equal("Parcial 1", detalle.Titulo);
        Assert.Equal(5m, detalle.NotaFinal);
        Assert.Equal(3, detalle.Preguntas.Count);

        var mc = detalle.Preguntas[0];
        Assert.Equal("5", mc.OpcionElegidaTexto);
        Assert.Equal("4", mc.OpcionCorrectaTexto);
        Assert.False(mc.EsCorrecta);
        Assert.Equal(0m, mc.PuntajeObtenido);
        Assert.Null(mc.Justificacion);

        var abiertaIa = detalle.Preguntas[1];
        Assert.Equal("Las plantas usan luz", abiertaIa.Respuesta);
        Assert.Equal(3m, abiertaIa.PuntajeObtenido);
        Assert.Equal("Faltó mencionar el CO2.", abiertaIa.Justificacion);
        Assert.Null(abiertaIa.OpcionCorrectaTexto);

        // Editada por el docente: solo el puntaje, sin la justificación de la IA ni el comentario interno
        var abiertaEditada = detalle.Preguntas[2];
        Assert.Equal(2m, abiertaEditada.PuntajeObtenido);
        Assert.Null(abiertaEditada.Justificacion);
        Assert.DoesNotContain(detalle.Preguntas, p => (p.Justificacion ?? "").Contains("Comentario interno"));
    }

    [Theory]
    [InlineData(EstadoNotaEntrega.Sugerida)]
    [InlineData(EstadoNotaEntrega.Editada)]
    public async Task NotaSinAprobar_NoMuestraNada(EstadoNotaEntrega estado)
    {
        var (repo, alumnoId, entregaId) = Preparar(estado);
        var handler = new ConsultarResultadosHandler(repo);

        var resultado = await handler.Handle(new ObtenerMiExamenDetalleQuery(entregaId, alumnoId), CancellationToken.None);

        Assert.Equal(TipoResultado.Conflicto, resultado.Tipo);
        Assert.Null(resultado.Valor);
    }

    [Fact]
    public async Task EntregaDeOtroAlumno_NoSeEncuentra()
    {
        var (repo, _, entregaId) = Preparar();
        var handler = new ConsultarResultadosHandler(repo);

        var resultado = await handler.Handle(new ObtenerMiExamenDetalleQuery(entregaId, Guid.NewGuid()), CancellationToken.None);

        Assert.Equal(TipoResultado.NoEncontrado, resultado.Tipo);
    }
}
