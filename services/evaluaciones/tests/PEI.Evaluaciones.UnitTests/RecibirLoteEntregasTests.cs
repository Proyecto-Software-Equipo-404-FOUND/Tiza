using PEI.Evaluaciones.Application.Evaluaciones;
using PEI.Evaluaciones.Domain.Entities;

namespace PEI.Evaluaciones.UnitTests;

public class RecibirLoteEntregasTests
{
    private static readonly Guid InstitucionId =
        Guid.Parse("11111111-1111-4111-8111-111111111111");
    private static readonly Guid EvaluacionId =
        Guid.Parse("22222222-2222-4222-8222-222222222222");
    private static readonly Guid SalaId =
        Guid.Parse("33333333-3333-4333-8333-333333333333");
    private static readonly Guid AlumnoId =
        Guid.Parse("44444444-4444-4444-8444-444444444444");

    private static readonly Guid PreguntaMultipleId =
        Guid.Parse("aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa");
    private static readonly Guid PreguntaAbiertaId =
        Guid.Parse("bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb");
    private static readonly Guid OpcionCorrectaId =
        Guid.Parse("cccccccc-cccc-4ccc-8ccc-cccccccccccc");
    private static readonly Guid OpcionIncorrectaId =
        Guid.Parse("dddddddd-dddd-4ddd-8ddd-dddddddddddd");

    [Fact]
    public async Task RecibirLote_CorrigeMultipleChoiceYDejaAbiertasPendientes()
    {
        var repositorio = CrearRepositorio();
        var handler = new RecibirLoteEntregasHandler(repositorio);
        var lote = CrearLote(
            new RespuestaLoteRequest(
                PreguntaMultipleId,
                OpcionIncorrectaId.ToString(),
                1,
                DateTimeOffset.UtcNow),
            new RespuestaLoteRequest(
                PreguntaMultipleId,
                OpcionCorrectaId.ToString(),
                2,
                DateTimeOffset.UtcNow),
            new RespuestaLoteRequest(
                PreguntaAbiertaId,
                "Respuesta desarrollada",
                3,
                DateTimeOffset.UtcNow));

        var resultado = await handler.Handle(
            new RecibirLoteEntregasCommand(SalaId, lote),
            CancellationToken.None);

        Assert.Equal(TipoResultado.Ok, resultado.Tipo);
        Assert.Equal(1, resultado.Valor!.EntregasNuevas);
        Assert.Equal(0, resultado.Valor.EntregasYaExistentes);
        Assert.Equal(2m, resultado.Valor.Entregas[0].NotaMultipleChoice);
        Assert.Equal(
            EstadoEntregaExamen.PendienteCorreccion.ToString(),
            resultado.Valor.Entregas[0].Estado);
    }

    [Fact]
    public async Task RecibirLote_ReintentoNoDuplicaLaEntrega()
    {
        var repositorio = CrearRepositorio();
        var handler = new RecibirLoteEntregasHandler(repositorio);
        var lote = CrearLote(
            new RespuestaLoteRequest(
                PreguntaMultipleId,
                OpcionCorrectaId.ToString(),
                1,
                DateTimeOffset.UtcNow));

        var primerResultado = await handler.Handle(
            new RecibirLoteEntregasCommand(SalaId, lote),
            CancellationToken.None);
        var reintento = await handler.Handle(
            new RecibirLoteEntregasCommand(SalaId, lote),
            CancellationToken.None);

        Assert.Equal(TipoResultado.Ok, primerResultado.Tipo);
        Assert.Equal(TipoResultado.Ok, reintento.Tipo);
        Assert.Equal(1, primerResultado.Valor!.EntregasNuevas);
        Assert.Equal(0, reintento.Valor!.EntregasNuevas);
        Assert.Equal(1, reintento.Valor.EntregasYaExistentes);
        Assert.Equal(
            primerResultado.Valor.Entregas[0].EntregaId,
            reintento.Valor.Entregas[0].EntregaId);
        Assert.Single(repositorio.Entregas);
    }

    [Fact]
    public async Task RecibirLote_SinRespuestaAbiertaQuedaCorregida()
    {
        var repositorio = CrearRepositorio();
        var handler = new RecibirLoteEntregasHandler(repositorio);
        var lote = CrearLote(
            new RespuestaLoteRequest(
                PreguntaMultipleId,
                OpcionCorrectaId.ToString(),
                1,
                DateTimeOffset.UtcNow));

        var resultado = await handler.Handle(
            new RecibirLoteEntregasCommand(SalaId, lote),
            CancellationToken.None);

        Assert.Equal(TipoResultado.Ok, resultado.Tipo);
        Assert.Equal(
            EstadoEntregaExamen.Corregida.ToString(),
            resultado.Valor!.Entregas[0].Estado);
    }

    [Fact]
    public async Task RecibirLote_SalaDeRutaDistintaEsInvalido()
    {
        var handler = new RecibirLoteEntregasHandler(CrearRepositorio());
        var lote = CrearLote();

        var resultado = await handler.Handle(
            new RecibirLoteEntregasCommand(Guid.NewGuid(), lote),
            CancellationToken.None);

        Assert.Equal(TipoResultado.Invalido, resultado.Tipo);
    }

    [Fact]
    public async Task RecibirLote_EvaluacionNoPublicadaNoSeEncuentra()
    {
        var evaluacion = CrearEvaluacion();
        evaluacion.Estado = EstadoEvaluacion.Borrador;
        var handler = new RecibirLoteEntregasHandler(
            new RepositorioFalso(evaluacion));

        var resultado = await handler.Handle(
            new RecibirLoteEntregasCommand(SalaId, CrearLote()),
            CancellationToken.None);

        Assert.Equal(TipoResultado.NoEncontrado, resultado.Tipo);
    }

    private static LoteEntregasRequest CrearLote(
        params RespuestaLoteRequest[] respuestas)
    {
        return new LoteEntregasRequest(
            Version: 1,
            LoteId: Guid.NewGuid(),
            SalaId: SalaId,
            EvaluacionId: EvaluacionId,
            InstitucionId: InstitucionId,
            FinalizadaUtc: DateTimeOffset.UtcNow,
            MotivoCierre: "Docente",
            Entregas:
            [
                new EntregaLoteRequest(
                    AlumnoId,
                    respuestas.ToList(),
                    [])
            ]);
    }

    private static RepositorioFalso CrearRepositorio() =>
        new(CrearEvaluacion());

    private static Evaluacion CrearEvaluacion() =>
        new()
        {
            Id = EvaluacionId,
            InstitucionId = InstitucionId,
            Estado = EstadoEvaluacion.Publicada,
            PreguntasJson = $$"""
                {
                  "version": 1,
                  "preguntas": [
                    {
                      "id": "{{PreguntaMultipleId}}",
                      "enunciado": "Pregunta de opción múltiple",
                      "tipo": "OpcionMultiple",
                      "puntajeMaximo": 2,
                      "opciones": [
                        {
                          "id": "{{OpcionCorrectaId}}",
                          "texto": "Correcta",
                          "esCorrecta": true
                        },
                        {
                          "id": "{{OpcionIncorrectaId}}",
                          "texto": "Incorrecta",
                          "esCorrecta": false
                        }
                      ]
                    },
                    {
                      "id": "{{PreguntaAbiertaId}}",
                      "enunciado": "Pregunta abierta",
                      "tipo": "Abierta",
                      "puntajeMaximo": 5
                    }
                  ]
                }
                """
        };

    private sealed class RepositorioFalso : IEvaluacionRepositorio
    {
        private readonly Evaluacion _evaluacion;

        public RepositorioFalso(Evaluacion evaluacion)
        {
            _evaluacion = evaluacion;
        }

        public List<EntregaExamen> Entregas { get; } = [];

        public Task<Evaluacion?> ObtenerPorIdAsync(
            Guid id,
            Guid institucionId,
            CancellationToken ct)
        {
            Evaluacion? encontrada =
                _evaluacion.Id == id && _evaluacion.InstitucionId == institucionId
                    ? _evaluacion
                    : null;

            return Task.FromResult(encontrada);
        }

        public Task<List<EntregaExamen>> ListarEntregasPorSalaAsync(
            Guid salaId,
            Guid institucionId,
            CancellationToken ct)
        {
            return Task.FromResult(
                Entregas.Where(e => e.SalaId == salaId && e.InstitucionId == institucionId).ToList());
        }

        public Task AgregarEntregaAsync(
            EntregaExamen entrega,
            CancellationToken ct)
        {
            Entregas.Add(entrega);
            return Task.CompletedTask;
        }

        public Task GuardarCambiosAsync(CancellationToken ct) =>
            Task.CompletedTask;

        public Task<List<Evaluacion>> ListarAsync(
            Guid institucionId,
            CancellationToken ct) =>
            throw new NotSupportedException();

        public Task AgregarAsync(Evaluacion evaluacion, CancellationToken ct) =>
            throw new NotSupportedException();

        public void Eliminar(Evaluacion evaluacion) =>
            throw new NotSupportedException();
    }
}