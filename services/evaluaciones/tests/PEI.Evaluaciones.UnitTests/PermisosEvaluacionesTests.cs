using System.Text.Json;
using PEI.Evaluaciones.Application.Evaluaciones;
using PEI.Evaluaciones.Application.Validacion;
using PEI.Evaluaciones.Domain.Entities;

namespace PEI.Evaluaciones.UnitTests;

// S3-EV-02 (#223): solo el docente crea, cada docente ve solo los suyos y la institución sale de la sesión.
public class PermisosEvaluacionesTests
{
    private static readonly Guid Institucion = Guid.NewGuid();
    private static readonly Guid DocenteA = Guid.NewGuid();
    private static readonly Guid DocenteB = Guid.NewGuid();

    [Theory]
    [InlineData("Docente", true)]
    [InlineData("Administrador", false)]
    [InlineData("Alumno", false)]
    [InlineData(null, false)]
    public void PuedeCrear_SoloElDocente(string? rol, bool esperado)
    {
        Assert.Equal(esperado, AccesoEvaluaciones.PuedeCrear(rol));
    }

    [Theory]
    [InlineData("Docente", true)]
    [InlineData("Administrador", true)]
    [InlineData("Alumno", false)]
    public void PuedeLeer_DocenteYAdministrador(string rol, bool esperado)
    {
        Assert.Equal(esperado, AccesoEvaluaciones.PuedeLeer(rol));
    }

    [Theory]
    [InlineData("Docente", true)]
    [InlineData("Administrador", false)]
    [InlineData("Alumno", false)]
    public void PuedeModificar_SoloElDocente(string rol, bool esperado)
    {
        Assert.Equal(esperado, AccesoEvaluaciones.PuedeModificar(rol));
    }

    [Fact]
    public void FiltroDocente_DocenteFiltraPorSuId_AdministradorSinFiltro()
    {
        Assert.Equal(DocenteA, AccesoEvaluaciones.FiltroDocente("Docente", DocenteA));
        Assert.Null(AccesoEvaluaciones.FiltroDocente("Administrador", DocenteA));
    }

    [Fact]
    public async Task Crear_GuardaElDocenteQueLoCrea()
    {
        var repo = new RepositorioEnMemoria();
        var handler = new CrearEvaluacionHandler(repo, new ValidadorPreguntas());

        var resultado = await handler.Handle(
            new CrearEvaluacionCommand(Institucion, "Parcial", "Examen", PreguntasValidas(), DocenteA),
            CancellationToken.None);

        Assert.Equal(TipoResultado.Ok, resultado.Tipo);
        Assert.Equal(DocenteA, Assert.Single(repo.Filas).DocenteId);
        Assert.Equal(DocenteA, resultado.Valor!.DocenteId);
    }

    [Fact]
    public async Task Listar_ElDocenteVeLosSuyosYLosSinDueno_NoLosDeOtroDocente()
    {
        var repo = RepositorioConTresExamenes(out var deA, out var deB, out var sinDueno);
        var handler = new ListarEvaluacionesHandler(repo);

        var lista = await handler.Handle(new ListarEvaluacionesQuery(Institucion, DocenteA), CancellationToken.None);

        var ids = lista.Select(e => e.Id).ToList();
        Assert.Contains(deA.Id, ids);
        Assert.Contains(sinDueno.Id, ids);
        Assert.DoesNotContain(deB.Id, ids);
    }

    [Fact]
    public async Task Listar_ElAdministradorVeTodos()
    {
        var repo = RepositorioConTresExamenes(out _, out _, out _);
        var handler = new ListarEvaluacionesHandler(repo);

        var lista = await handler.Handle(new ListarEvaluacionesQuery(Institucion, DocenteId: null), CancellationToken.None);

        Assert.Equal(3, lista.Count);
    }

    [Fact]
    public async Task ComprobarAcceso_ExamenDeOtroDocente_NoEncontrado()
    {
        var repo = RepositorioConTresExamenes(out _, out var deB, out _);
        var handler = new ComprobarAccesoEvaluacionHandler(repo);

        var resultado = await handler.Handle(
            new ComprobarAccesoEvaluacionQuery(deB.Id, Institucion, DocenteA), CancellationToken.None);

        Assert.Equal(TipoResultado.NoEncontrado, resultado.Tipo);
    }

    [Fact]
    public async Task ComprobarAcceso_ExamenPropioOSinDueno_Ok()
    {
        var repo = RepositorioConTresExamenes(out var deA, out _, out var sinDueno);
        var handler = new ComprobarAccesoEvaluacionHandler(repo);

        var propio = await handler.Handle(new ComprobarAccesoEvaluacionQuery(deA.Id, Institucion, DocenteA), CancellationToken.None);
        var viejo = await handler.Handle(new ComprobarAccesoEvaluacionQuery(sinDueno.Id, Institucion, DocenteA), CancellationToken.None);

        Assert.Equal(TipoResultado.Ok, propio.Tipo);
        Assert.Equal(TipoResultado.Ok, viejo.Tipo);
    }

    [Fact]
    public async Task ComprobarAcceso_AdministradorVeCualquieraDeSuInstitucion()
    {
        var repo = RepositorioConTresExamenes(out _, out var deB, out _);
        var handler = new ComprobarAccesoEvaluacionHandler(repo);

        var resultado = await handler.Handle(new ComprobarAccesoEvaluacionQuery(deB.Id, Institucion, DocenteId: null), CancellationToken.None);

        Assert.Equal(TipoResultado.Ok, resultado.Tipo);
    }

    [Fact]
    public async Task ComprobarAcceso_OtraInstitucion_NoEncontrado()
    {
        var repo = RepositorioConTresExamenes(out var deA, out _, out _);
        var handler = new ComprobarAccesoEvaluacionHandler(repo);

        var resultado = await handler.Handle(new ComprobarAccesoEvaluacionQuery(deA.Id, Guid.NewGuid(), DocenteA), CancellationToken.None);

        Assert.Equal(TipoResultado.NoEncontrado, resultado.Tipo);
    }

    // Mismo JSON válido que ContratoPreguntasTests.
    private static JsonElement PreguntasValidas()
    {
        const string json = """
            {"version":1,"preguntas":[{"id":"00000000-0000-4000-8000-000000000101",
            "enunciado":"Explicar una relación","tipo":"Abierta","puntajeMaximo":10,
            "rubrica":{"criterios":[{"descripcion":"Claridad","puntaje":10}]}}]}
            """;
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.Clone();
    }

    private static RepositorioEnMemoria RepositorioConTresExamenes(out Evaluacion deA, out Evaluacion deB, out Evaluacion sinDueno)
    {
        deA = Examen(DocenteA);
        deB = Examen(DocenteB);
        sinDueno = Examen(null);
        var repo = new RepositorioEnMemoria();
        repo.Filas.AddRange(new[] { deA, deB, sinDueno });
        return repo;
    }

    private static Evaluacion Examen(Guid? docenteId) => new()
    {
        Id = Guid.NewGuid(),
        InstitucionId = Institucion,
        DocenteId = docenteId,
        Titulo = "Parcial",
        Tipo = "Examen",
        PreguntasJson = "{\"version\":1,\"preguntas\":[]}",
        FechaCreacion = DateTimeOffset.UtcNow,
        FechaActualizacion = DateTimeOffset.UtcNow
    };

    private sealed class RepositorioEnMemoria : IEvaluacionRepositorio
    {
        public List<Evaluacion> Filas { get; } = new();

        public Task<Evaluacion?> ObtenerPorIdAsync(Guid id, Guid institucionId, CancellationToken ct) =>
            Task.FromResult(Filas.FirstOrDefault(e => e.Id == id && e.InstitucionId == institucionId));

        public Task<List<Evaluacion>> ListarAsync(Guid institucionId, CancellationToken ct) =>
            Task.FromResult(Filas.Where(e => e.InstitucionId == institucionId).ToList());

        public Task AgregarAsync(Evaluacion evaluacion, CancellationToken ct)
        {
            Filas.Add(evaluacion);
            return Task.CompletedTask;
        }

        public void Eliminar(Evaluacion evaluacion) => Filas.Remove(evaluacion);

        public Task GuardarCambiosAsync(CancellationToken ct) => Task.CompletedTask;

        public Task<List<EntregaExamen>> ListarEntregasPorSalaAsync(Guid salaId, Guid institucionId, CancellationToken ct) =>
            Task.FromResult(new List<EntregaExamen>());

        public Task AgregarEntregaAsync(EntregaExamen entrega, CancellationToken ct) => Task.CompletedTask;
    }
}
