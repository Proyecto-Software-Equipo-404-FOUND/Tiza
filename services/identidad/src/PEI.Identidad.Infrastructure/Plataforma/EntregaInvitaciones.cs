using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PEI.Identidad.Application.Plataforma;
using PEI.Identidad.Domain.Entities;
using PEI.Identidad.Infrastructure.Persistence;
using PEI.Identidad.Infrastructure.Security;

namespace PEI.Identidad.Infrastructure.Plataforma;

// Buzón cifrado local; nunca escribe enlaces de activación en logs ni archivos en claro.
public sealed class InvitationSenderRegistrationValidator(IServiceProvider services) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        using var scope = services.CreateScope();
        // El Outbox puede acumular entregas cifradas mientras el despliegue no tenga
        // proveedor configurado. Si existe uno, validar que pueda construirse al iniciar.
        _ = scope.ServiceProvider.GetService<IEmailSender>();
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}

public sealed class DevelopmentEmailSender(PlataformaOptions options, IDataProtectionProvider protection) : IEmailSender
{
    public const string Purpose = "Tiza.ID06.BuzonDesarrollo.v1";
    public async Task EnviarAsync(MensajeEmail mensaje, CancellationToken ct)
    {
        var directory = string.IsNullOrWhiteSpace(options.DevelopmentMailDirectory)
            ? Path.Combine(Path.GetTempPath(), "tiza-identidad-development-mail")
            : options.DevelopmentMailDirectory;
        Directory.CreateDirectory(directory);
        var destino = Path.Combine(directory, mensaje.Id.ToString("N") + ".mail");
        if (File.Exists(destino)) return; // Idempotencia para reintentos tras un envío exitoso.
        var temporal = destino + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            var cifrado = protection.CreateProtector(Purpose).Protect($"Para: {mensaje.Destinatario}\n{mensaje.Contenido}");
            await File.WriteAllTextAsync(temporal, cifrado, ct);
            File.Move(temporal, destino, false);
        }
        finally { if (File.Exists(temporal)) File.Delete(temporal); }
    }
}

public sealed class InvitacionesOutbox(PlataformaDbContext db, IEmailSender sender, IDataProtectionProvider protection)
{
    public async Task<bool> ProcesarUnaAsync(CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var entregas = await db.Set<EntregaInvitacion>().FromSqlRaw("""
            SELECT e.* FROM "EntregaInvitacion" e
            WHERE e."Entregada" IS NULL AND e."ContenidoProtegido" <> ''
              AND COALESCE(e."ProximoIntento", e."Creada") <= statement_timestamp()
            ORDER BY COALESCE(e."ProximoIntento", e."Creada"), e."Creada", e."Id"
            LIMIT 1 FOR UPDATE OF e SKIP LOCKED
            """).ToListAsync(ct);
        if (entregas.Count == 0) return false;
        var entrega = entregas[0];
        var invitacion = await db.Set<InvitacionAdministrador>().SingleAsync(x => x.Id == entrega.InvitacionId, ct);
        entrega.Intentos = (int)Math.Min((long)entrega.Intentos + 1, int.MaxValue);
        if (invitacion.Utilizada.HasValue || invitacion.Vencimiento <= DateTime.UtcNow)
            entrega.ContenidoProtegido = "";
        else
        {
            try
            {
                var contenido = protection.CreateProtector(PlataformaDataAccess.ProtectionPurpose).Unprotect(entrega.ContenidoProtegido);
                await sender.EnviarAsync(new(entrega.Id, entrega.Destinatario, contenido), ct);
                entrega.Entregada = DateTime.UtcNow;
                entrega.ContenidoProtegido = "";
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
            catch
            {
                // Capturar la hora despues del fallo; seleccion y programacion usan PostgreSQL.
                var minutos = Math.Clamp(entrega.Intentos, 1, 60);
                entrega.ProximoIntento = await db.Database.SqlQuery<DateTime>($"""
                    SELECT clock_timestamp() + ({minutos} * interval '1 minute') AS "Value"
                    """).SingleAsync(ct);
                // Nunca publicar cuerpo, token ni excepcion del proveedor.
            }
        }
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return true;
    }
}

public sealed class InvitacionesWorker(IServiceScopeFactory scopes, ILogger<InvitacionesWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(10));
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                if (scope.ServiceProvider.GetService<IEmailSender>() is null)
                {
                    if (!await timer.WaitForNextTickAsync(stoppingToken)) break;
                    continue;
                }
                await scope.ServiceProvider.GetRequiredService<InvitacionesOutbox>().ProcesarUnaAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch { logger.LogWarning("No se pudo procesar el outbox de invitaciones; se reintentará."); }
            if (!await timer.WaitForNextTickAsync(stoppingToken)) break;
        }
    }
}
