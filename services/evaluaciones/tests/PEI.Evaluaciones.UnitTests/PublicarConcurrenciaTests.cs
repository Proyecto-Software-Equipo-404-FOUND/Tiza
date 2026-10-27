using PEI.Evaluaciones.Application.Evaluaciones;
using PEI.Evaluaciones.Domain.Entities;

namespace PEI.Evaluaciones.UnitTests;

public class PublicarConcurrenciaTests
{
    [Fact]
    public async Task ConflictoDePersistencia_SeDevuelveComoConflictoSinReintentar()
    {
        var repo = new RepositorioQueFalla(new ConflictoConcurrenciaException(new Exception("colision")));
        var result = await new PublicarEvaluacionHandler(repo, new Cursos())
            .Handle(new(repo.Row.Id, repo.Row.InstitucionId, 1, Guid.NewGuid()), default);
        Assert.Equal(TipoResultado.Conflicto, result.Tipo);
        Assert.Null(result.Valor);
        Assert.Equal(1, repo.Saves);
    }

    [Fact]
    public async Task ErrorDistintoDeConcurrencia_NoSeDisfrazaDeConflicto()
    {
        var error = new InvalidOperationException("almacenamiento no disponible");
        var repo = new RepositorioQueFalla(error);
        var caught = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new PublicarEvaluacionHandler(repo, new Cursos())
                .Handle(new(repo.Row.Id, repo.Row.InstitucionId, 1, Guid.NewGuid()), default));
        Assert.Same(error, caught);
    }

    private sealed class Cursos : ICursosClient
    {
        public Task<bool> ExisteCohorteAsync(Guid cohort, Guid institution, CancellationToken ct) => Task.FromResult(true);
    }

    private sealed class RepositorioQueFalla(Exception error) : IEvaluacionRepositorio
    {
        public Evaluacion Row { get; } = new()
        {
            Id = Guid.NewGuid(), InstitucionId = Guid.NewGuid(), Version = 1,
            Estado = EstadoEvaluacion.Borrador, PreguntasJson = "{\"preguntas\":[{}]}"
        };
        public int Saves { get; private set; }
        public Task<Evaluacion?> ObtenerPorIdAsync(Guid id, Guid institution, CancellationToken ct) => Task.FromResult<Evaluacion?>(Row);
        public Task GuardarCambiosAsync(CancellationToken ct) { Saves++; return Task.FromException(error); }
        public Task<List<Evaluacion>> ListarAsync(Guid institution, CancellationToken ct) => throw new NotSupportedException();
        public Task AgregarAsync(Evaluacion row, CancellationToken ct) => throw new NotSupportedException();
        public void Eliminar(Evaluacion row) => throw new NotSupportedException();
    }
}
