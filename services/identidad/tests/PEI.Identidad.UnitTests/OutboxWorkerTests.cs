using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PEI.Identidad.Infrastructure.Plataforma;

namespace PEI.Identidad.UnitTests;

public sealed class OutboxWorkerTests
{
    private sealed class Captura : ILogger<InvitacionesWorker>
    {
        public readonly TaskCompletionSource<(string Message, Exception? Error)> Warning = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel level) => true;
        public void Log<TState>(LogLevel level, EventId id, TState state, Exception? error, Func<TState, Exception?, string> formatter)
        {
            if (level == LogLevel.Warning) Warning.TrySetResult((formatter(state, error), error));
        }
    }

    [Fact]
    public async Task WorkerNoPublicaExcepcionNiContenidoSensibleAlFallarProcesamiento()
    {
        const string secreto = "token-sintetico-confidencial-no-publicar";
        await using var services = new ServiceCollection()
            .AddScoped<InvitacionesOutbox>(_ => throw new InvalidOperationException(secreto))
            .BuildServiceProvider();
        var logger = new Captura();
        using var worker = new InvitacionesWorker(services.GetRequiredService<IServiceScopeFactory>(), logger);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await worker.StartAsync(timeout.Token);
        try
        {
            var result = await logger.Warning.Task.WaitAsync(timeout.Token);
            Assert.Equal("No se pudo procesar el outbox de invitaciones; se reintentará.", result.Message);
            Assert.DoesNotContain(secreto, result.Message);
            Assert.Null(result.Error);
        }
        finally { await worker.StopAsync(timeout.Token); }
    }
}
