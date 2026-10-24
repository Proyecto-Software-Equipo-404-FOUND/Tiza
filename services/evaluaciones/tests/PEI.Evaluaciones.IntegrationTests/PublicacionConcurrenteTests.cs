using Microsoft.EntityFrameworkCore;
using PEI.Evaluaciones.Application.Evaluaciones;
using PEI.Evaluaciones.Infrastructure.Persistence;
using QA07;
using Xunit;

namespace PEI.Evaluaciones.IntegrationTests;

public sealed class PostgreSqlFactAttribute : FactAttribute
{
    public PostgreSqlFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("QA07_CONNECTION")))
            Skip = "BLOCKED: configurar QA07_CONNECTION hacia PostgreSQL Docker exclusivo de QA-07.";
    }
}

public class PublicacionConcurrenteTests
{
    [PostgreSqlFact]
    public async Task DosLecturasDeVersionUno_SoloUnaPublicacionConfirmada()
    {
        var database = Environment.GetEnvironmentVariable("QA07_DATABASE") ?? "pei_evaluaciones_qa07";
        await using var setup = QaSupport.Open(database);
        await QaSupport.VerifyDatabase(setup, database);
        var row = QaSupport.New(Guid.NewGuid());
        setup.Add(row);
        await setup.SaveChangesAsync();
        var before = await setup.Evaluaciones.AsNoTracking().SingleAsync(e => e.Id == row.Id);
        QaSupport.Initial(before, row.InstitucionId);
        await using var a = QaSupport.Open(database);
        await using var b = QaSupport.Open(database);
        var barrier = new CursosConBarrera();
        var cohort = Guid.NewGuid();
        var command = new PublicarEvaluacionCommand(row.Id, row.InstitucionId, 1, cohort);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var results = await Task.WhenAll(
            new PublicarEvaluacionHandler(new EvaluacionRepositorio(a), barrier).Handle(command, timeout.Token),
            new PublicarEvaluacionHandler(new EvaluacionRepositorio(b), barrier).Handle(command, timeout.Token));
        Assert.Equal(2, barrier.Readers);
        Assert.Single(results, r => r.Tipo == TipoResultado.Ok);
        Assert.Single(results, r => r.Tipo == TipoResultado.Conflicto);
        var after = await setup.Evaluaciones.AsNoTracking().SingleAsync(e => e.Id == row.Id);
        QaSupport.Final(before, after, cohort);
        // Una nueva lectura obsoleta tampoco puede modificar el registro confirmado.
        await using var retry = QaSupport.Open(database);
        var rejected = await new PublicarEvaluacionHandler(new EvaluacionRepositorio(retry), barrier)
            .Handle(command, timeout.Token);
        Assert.Equal(TipoResultado.Conflicto, rejected.Tipo);
        var unchanged = await setup.Evaluaciones.AsNoTracking().SingleAsync(e => e.Id == row.Id);
        Assert.Equal(after.FechaActualizacion, unchanged.FechaActualizacion);
        QaSupport.Final(before, unchanged, cohort);
    }

    private sealed class CursosConBarrera : ICursosClient
    {
        private readonly TaskCompletionSource _both = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _readers;
        public int Readers => _readers;
        public async Task<bool> ExisteCohorteAsync(Guid cohort, Guid institution, CancellationToken ct)
        {
            // El handler llega aquí después de leer y validar Version=1 en su propio DbContext.
            if (Interlocked.Increment(ref _readers) == 2) _both.TrySetResult();
            await _both.Task.WaitAsync(ct);
            return true;
        }
    }
}
