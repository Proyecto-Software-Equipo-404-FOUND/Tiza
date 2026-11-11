using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using PEI.Evaluaciones.Application.Correccion;
using PEI.Evaluaciones.Application.Evaluaciones;
using PEI.Evaluaciones.Domain.Entities;

namespace PEI.Evaluaciones.UnitTests;

// S2-EV-02 (#144): registrar la nota sugerida por IA y procesar entregas pendientes.
public class CorreccionIaTests
{
    private static readonly Guid InstitucionId = Guid.Parse("11111111-1111-4111-8111-111111111111");
    private static readonly Guid EvaluacionId = Guid.Parse("22222222-2222-4222-8222-222222222222");
    private static readonly Guid MultipleId = Guid.Parse("aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa");
    private static readonly Guid AbiertaUnoId = Guid.Parse("bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb");
    private static readonly Guid AbiertaDosId = Guid.Parse("eeeeeeee-eeee-4eee-8eee-eeeeeeeeeeee");
    private static readonly Guid OpcionId = Guid.Parse("cccccccc-cccc-4ccc-8ccc-cccccccccccc");

    // ---------- Registrar la nota sugerida (callback de IA) ----------

    [Fact]
    public async Task Registrar_UnicaAbierta_GuardaLaNotaYPasaACorregida()
    {
        var (repo, entrega) = Escenario(AbiertaUnoId);
        var r = await new RegistrarCalificacionSugeridaHandler(repo).Handle(Comando(entrega.Id, AbiertaUnoId, 3.5m), default);

        Assert.Equal(TipoResultado.Ok, r.Tipo);
        Assert.False(r.Valor!.YaExistia);
        Assert.Equal("Corregida", r.Valor.EstadoEntrega);
        Assert.Single(repo.Calificaciones);
        Assert.Equal(EstadoEntregaExamen.Corregida, entrega.Estado);
    }

    [Fact]
    public async Task Registrar_DosAbiertas_RecienConLaSegundaPasaACorregida()
    {
        var (repo, entrega) = Escenario(AbiertaUnoId, AbiertaDosId);
        var handler = new RegistrarCalificacionSugeridaHandler(repo);

        var primera = await handler.Handle(Comando(entrega.Id, AbiertaUnoId, 2m), default);
        Assert.Equal("PendienteCorreccion", primera.Valor!.EstadoEntrega);

        var segunda = await handler.Handle(Comando(entrega.Id, AbiertaDosId, 1m), default);
        Assert.Equal("Corregida", segunda.Valor!.EstadoEntrega);
    }

    [Fact]
    public async Task Registrar_Reintento_NoDuplicaNiPisaLaNota()
    {
        var (repo, entrega) = Escenario(AbiertaUnoId);
        var handler = new RegistrarCalificacionSugeridaHandler(repo);
        await handler.Handle(Comando(entrega.Id, AbiertaUnoId, 3m), default);

        var r = await handler.Handle(Comando(entrega.Id, AbiertaUnoId, 1m), default);

        Assert.True(r.Valor!.YaExistia);
        Assert.Equal(3m, r.Valor.NotaSugerida);
        Assert.Single(repo.Calificaciones);
    }

    [Fact]
    public async Task Registrar_NotaMayorAlMaximo_EsInvalida()
    {
        var (repo, entrega) = Escenario(AbiertaUnoId);
        var r = await new RegistrarCalificacionSugeridaHandler(repo).Handle(Comando(entrega.Id, AbiertaUnoId, 5m), default);
        Assert.Equal(TipoResultado.Invalido, r.Tipo);
        Assert.Empty(repo.Calificaciones);
    }

    [Fact]
    public async Task Registrar_JustificacionVacia_EsInvalida()
    {
        var (repo, entrega) = Escenario(AbiertaUnoId);
        var r = await new RegistrarCalificacionSugeridaHandler(repo).Handle(
            new RegistrarCalificacionSugeridaCommand(entrega.Id, AbiertaUnoId, 2m, "  ", null), default);
        Assert.Equal(TipoResultado.Invalido, r.Tipo);
    }

    [Fact]
    public async Task Registrar_PreguntaDeOpcionMultiple_EsInvalida()
    {
        var (repo, entrega) = Escenario(AbiertaUnoId);
        var r = await new RegistrarCalificacionSugeridaHandler(repo).Handle(Comando(entrega.Id, MultipleId, 1m), default);
        Assert.Equal(TipoResultado.Invalido, r.Tipo);
    }

    [Fact]
    public async Task Registrar_PreguntaOEntregaInexistente_NoSeEncuentra()
    {
        var (repo, entrega) = Escenario(AbiertaUnoId);
        var handler = new RegistrarCalificacionSugeridaHandler(repo);
        Assert.Equal(TipoResultado.NoEncontrado, (await handler.Handle(Comando(entrega.Id, Guid.NewGuid(), 1m), default)).Tipo);
        Assert.Equal(TipoResultado.NoEncontrado, (await handler.Handle(Comando(Guid.NewGuid(), AbiertaUnoId, 1m), default)).Tipo);
    }

    // ---------- Procesar pendientes (worker) ----------

    [Fact]
    public async Task Procesar_MandaSoloLasAbiertasRespondidas_SinDatosDelAlumno()
    {
        var (repo, entrega) = Escenario(AbiertaUnoId); // AbiertaDos no fue respondida
        var ia = new IaFalsa(repo, registraNota: true);

        await Procesar(repo, ia);

        var solicitud = Assert.Single(ia.Solicitudes);
        Assert.Equal(AbiertaUnoId, solicitud.PreguntaId);
        Assert.Equal(entrega.Id, solicitud.EntregaId);
        Assert.DoesNotContain(entrega.AlumnoId.ToString(), JsonSerializer.Serialize(solicitud));
    }

    [Fact]
    public async Task Procesar_IaRegistraLaNota_LaEntregaQuedaCorregida()
    {
        var (repo, entrega) = Escenario(AbiertaUnoId);
        await Procesar(repo, new IaFalsa(repo, registraNota: true));
        Assert.Equal(EstadoEntregaExamen.Corregida, entrega.Estado);
    }

    [Fact]
    public async Task Procesar_IaFalla_PasaACorreccionManual()
    {
        var (repo, entrega) = Escenario(AbiertaUnoId);
        await Procesar(repo, new IaFalsa(repo, registraNota: false, falla: true));
        Assert.Equal(EstadoEntregaExamen.CorreccionManual, entrega.Estado);
    }

    [Fact]
    public async Task Procesar_IaRespondeSinRegistrarLaNota_PasaACorreccionManualYNoReintentaEnBucle()
    {
        var (repo, entrega) = Escenario(AbiertaUnoId);
        var ia = new IaFalsa(repo, registraNota: false);

        await Procesar(repo, ia);
        await Procesar(repo, ia); // segunda vuelta del worker: ya no está pendiente

        Assert.Equal(EstadoEntregaExamen.CorreccionManual, entrega.Estado);
        Assert.Single(ia.Solicitudes);
    }

    [Fact]
    public async Task Procesar_PreguntaYaCalificada_NoSeReenviaAIa()
    {
        var (repo, entrega) = Escenario(AbiertaUnoId, AbiertaDosId);
        await new RegistrarCalificacionSugeridaHandler(repo).Handle(Comando(entrega.Id, AbiertaUnoId, 2m), default);
        var ia = new IaFalsa(repo, registraNota: true);

        await Procesar(repo, ia);

        Assert.Equal(AbiertaDosId, Assert.Single(ia.Solicitudes).PreguntaId);
        Assert.Equal(EstadoEntregaExamen.Corregida, entrega.Estado);
    }

    // ---------- Apoyo ----------

    private static Task Procesar(RepositorioFalso repo, IaFalsa ia) =>
        new ProcesarCorreccionesPendientesHandler(repo, ia, NullLogger<ProcesarCorreccionesPendientesHandler>.Instance)
            .Handle(new ProcesarCorreccionesPendientesCommand(), default);

    private static RegistrarCalificacionSugeridaCommand Comando(Guid entregaId, Guid preguntaId, decimal nota) =>
        new(entregaId, preguntaId, nota, "Explica el concepto pero le falta un ejemplo.", null);

    // Evaluación con 1 multiple choice y 2 abiertas (puntaje máximo 4 y 2).
    // La entrega responde la multiple choice y las abiertas indicadas.
    private static (RepositorioFalso, EntregaExamen) Escenario(params Guid[] abiertasRespondidas)
    {
        var evaluacion = new Evaluacion
        {
            Id = EvaluacionId,
            InstitucionId = InstitucionId,
            Estado = EstadoEvaluacion.Publicada,
            PreguntasJson = $$$"""
            {"version":1,"preguntas":[
              {"id":"{{{MultipleId}}}","enunciado":"¿Qué es un volcán?","tipo":"OpcionMultiple","puntajeMaximo":1,
               "opciones":[{"id":"{{{OpcionId}}}","texto":"Una montaña con magma","esCorrecta":true}]},
              {"id":"{{{AbiertaUnoId}}}","enunciado":"Explicá cómo se forma un volcán.","tipo":"Abierta","puntajeMaximo":4,
               "rubrica":{"criterios":[{"descripcion":"Menciona el magma","puntaje":4}]}},
              {"id":"{{{AbiertaDosId}}}","enunciado":"Nombrá un volcán argentino.","tipo":"Abierta","puntajeMaximo":2}
            ]}
            """
        };

        var respuestas = new List<object> { new { preguntaId = MultipleId, respuesta = OpcionId.ToString(), secuencia = 1 } };
        var secuencia = 2;
        foreach (var id in abiertasRespondidas)
            respuestas.Add(new { preguntaId = id, respuesta = "Respuesta del alumno", secuencia = secuencia++ });

        var entrega = new EntregaExamen
        {
            Id = Guid.NewGuid(),
            InstitucionId = InstitucionId,
            EvaluacionId = EvaluacionId,
            SalaId = Guid.NewGuid(),
            AlumnoId = Guid.Parse("44444444-4444-4444-8444-444444444444"),
            RespuestasJson = JsonSerializer.Serialize(respuestas),
            AlertasFocoJson = "[]",
            NotaMultipleChoice = 1,
            Estado = EstadoEntregaExamen.PendienteCorreccion,
            FechaEntrega = DateTimeOffset.UtcNow
        };

        var repo = new RepositorioFalso(evaluacion);
        repo.Entregas.Add(entrega);
        return (repo, entrega);
    }

    private sealed class RepositorioFalso : ICorreccionRepositorio
    {
        private readonly Evaluacion _evaluacion;
        public RepositorioFalso(Evaluacion evaluacion) => _evaluacion = evaluacion;

        public List<EntregaExamen> Entregas { get; } = new();
        public List<CalificacionSugerida> Calificaciones { get; } = new();

        public Task<List<EntregaExamen>> ListarPendientesAsync(int maximo, CancellationToken ct) =>
            Task.FromResult(Entregas.Where(e => e.Estado == EstadoEntregaExamen.PendienteCorreccion).Take(maximo).ToList());

        public Task<EntregaExamen?> ObtenerEntregaAsync(Guid entregaId, CancellationToken ct) =>
            Task.FromResult(Entregas.FirstOrDefault(e => e.Id == entregaId));

        public Task<Evaluacion?> ObtenerEvaluacionAsync(Guid evaluacionId, Guid institucionId, CancellationToken ct) =>
            Task.FromResult<Evaluacion?>(_evaluacion.Id == evaluacionId && _evaluacion.InstitucionId == institucionId ? _evaluacion : null);

        public Task<List<CalificacionSugerida>> ListarCalificacionesAsync(Guid entregaId, CancellationToken ct) =>
            Task.FromResult(Calificaciones.Where(c => c.EntregaId == entregaId).ToList());

        public Task AgregarCalificacionAsync(CalificacionSugerida calificacion, CancellationToken ct)
        {
            Calificaciones.Add(calificacion);
            return Task.CompletedTask;
        }

        public Task GuardarCambiosAsync(CancellationToken ct) => Task.CompletedTask;
    }

    // Simula a IA: si registraNota, hace el "callback" igual que la tool MCP real.
    private sealed class IaFalsa : ICorreccionIaClient
    {
        private readonly RepositorioFalso _repo;
        private readonly bool _registraNota;
        private readonly bool _falla;

        public IaFalsa(RepositorioFalso repo, bool registraNota, bool falla = false)
        {
            _repo = repo;
            _registraNota = registraNota;
            _falla = falla;
        }

        public List<SolicitudCorreccionIa> Solicitudes { get; } = new();

        public async Task<bool> SolicitarCorreccionAsync(SolicitudCorreccionIa solicitud, CancellationToken ct)
        {
            Solicitudes.Add(solicitud);
            if (_falla) return false;
            if (_registraNota)
            {
                await new RegistrarCalificacionSugeridaHandler(_repo).Handle(
                    new RegistrarCalificacionSugeridaCommand(solicitud.EntregaId, solicitud.PreguntaId, 1m, "Sugerida por IA.", null), ct);
            }
            return true;
        }
    }
}
