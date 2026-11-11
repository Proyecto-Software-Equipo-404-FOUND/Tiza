using MediatR;
using PEI.Evaluaciones.Application.Correccion;

namespace PEI.Evaluaciones.Api.Workers;

// S2-EV-02 (#144): cada CorreccionIa:IntervaloSegundos busca entregas en PendienteCorreccion
// y las manda a IA (RN-COR-05: por base de datos, sin colas). La lógica vive en Application;
// el worker solo agenda. Se apaga con CorreccionIa:Habilitada=false.
public sealed class CorreccionIaWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopes;
    private readonly IConfiguration _configuracion;
    private readonly ILogger<CorreccionIaWorker> _logger;

    public CorreccionIaWorker(IServiceScopeFactory scopes, IConfiguration configuracion, ILogger<CorreccionIaWorker> logger)
    {
        _scopes = scopes;
        _configuracion = configuracion;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_configuracion.GetValue("CorreccionIa:Habilitada", true))
        {
            _logger.LogInformation("Worker de corrección con IA deshabilitado (CorreccionIa:Habilitada=false).");
            return;
        }

        var intervalo = TimeSpan.FromSeconds(_configuracion.GetValue("CorreccionIa:IntervaloSegundos", 5));
        var maximo = _configuracion.GetValue("CorreccionIa:LoteMaximo", 10);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopes.CreateScope();
                var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
                var procesadas = await mediator.Send(new ProcesarCorreccionesPendientesCommand(maximo), stoppingToken);
                if (procesadas > 0) _logger.LogInformation("Corrección con IA: {Cantidad} entregas procesadas.", procesadas);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                // Un error (por ejemplo, la base caída) no debe matar el worker.
                _logger.LogError(ex, "Error en el ciclo del worker de corrección con IA.");
            }

            try { await Task.Delay(intervalo, stoppingToken); }
            catch (OperationCanceledException) { break; }
        }
    }
}
