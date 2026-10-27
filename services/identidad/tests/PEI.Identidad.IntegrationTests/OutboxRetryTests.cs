using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using PEI.Identidad.Application.Plataforma;
using PEI.Identidad.Domain.Entities;
using PEI.Identidad.Infrastructure.Plataforma;
using PEI.Identidad.Infrastructure.Persistence;

namespace PEI.Identidad.IntegrationTests;

public sealed partial class TenantIsolationTests
{
    private sealed class SenderControlado(Func<MensajeEmail, CancellationToken, Task> send) : IEmailSender
    {
        public Task EnviarAsync(MensajeEmail mensaje, CancellationToken ct) => send(mensaje, ct);
    }

    private static Task<DateTime> HoraOutboxAsync(PlataformaDbContext db) =>
        db.Database.SqlQuery<DateTime>($"SELECT clock_timestamp() AS \"Value\"").SingleAsync();

    [DatabaseFact]
    public async Task H1_FalloAntiguoEsperaYPermiteEntregaPosteriorConMismoIdAlReintentar()
    {
        var actor = await InicializarPlataformaAsync();
        Guid primera, segunda; string cifrado;
        await using (var setup = PlatformContext())
        {
            await PlatformData(setup).CrearInstitucionAsync(actor, Alta("fallida@tiza.test"), default);
            primera = (await setup.Set<EntregaInvitacion>().SingleAsync()).Id;
            await setup.Set<EntregaInvitacion>().Where(e => e.Id == primera)
                .ExecuteUpdateAsync(s => s.SetProperty(e => e.Creada, DateTime.UtcNow.AddHours(-2)));
            await PlatformData(setup).CrearInstitucionAsync(actor, Alta("valida@tiza.test"), default);
            segunda = await setup.Set<EntregaInvitacion>().Where(e => e.Id != primera).Select(e => e.Id).SingleAsync();
            cifrado = await setup.Set<EntregaInvitacion>().Where(e => e.Id == primera).Select(e => e.ContenidoProtegido).SingleAsync();
        }
        var llamadas = new List<Guid>(); var fallar = true;
        var sender = new SenderControlado((mensaje, _) =>
        {
            llamadas.Add(mensaje.Id);
            if (mensaje.Id == primera && fallar) throw new IOException("error-proveedor-confidencial " + mensaje.Contenido);
            return Task.CompletedTask;
        });
        await using (var db = PlatformContext())
        {
            var antes = await HoraOutboxAsync(db);
            Assert.True(await new InvitacionesOutbox(db, sender, platformProtection).ProcesarUnaAsync(default));
            var despues = await HoraOutboxAsync(db);
            var fila = await db.Set<EntregaInvitacion>().AsNoTracking().SingleAsync(e => e.Id == primera);
            Assert.Equal(1, fila.Intentos); Assert.Null(fila.Entregada);
            Assert.InRange(fila.ProximoIntento!.Value, antes.AddMinutes(1), despues.AddMinutes(1));
            Assert.Equal(cifrado, fila.ContenidoProtegido);
        }
        await using (var db = PlatformContext())
        {
            var outbox = new InvitacionesOutbox(db, sender, platformProtection);
            Assert.True(await outbox.ProcesarUnaAsync(default));
            Assert.False(await outbox.ProcesarUnaAsync(default));
        }
        Assert.Equal(new[] { primera, segunda }, llamadas);
        await using (var setup = PlatformContext())
            await setup.Set<EntregaInvitacion>().Where(e => e.Id == primera)
                .ExecuteUpdateAsync(s => s.SetProperty(e => e.ProximoIntento, DateTime.UtcNow.AddMinutes(-1)));
        fallar = false;
        await using (var db = PlatformContext())
        {
            Assert.True(await new InvitacionesOutbox(db, sender, platformProtection).ProcesarUnaAsync(default));
            var fila = await db.Set<EntregaInvitacion>().AsNoTracking().SingleAsync(e => e.Id == primera);
            Assert.NotNull(fila.Entregada); Assert.Equal("", fila.ContenidoProtegido);
        }
        Assert.Equal(new[] { primera, segunda, primera }, llamadas);
    }

    [DatabaseFact]
    public async Task H1_IntervalosCrecientesAcotadosDesdeElFalloNoDesdeCreada()
    {
        var actor = await InicializarPlataformaAsync();
        await using (var setup = PlatformContext())
            await PlatformData(setup).CrearInstitucionAsync(actor, Alta(), default);
        foreach (var intentos in new[] { 0, 1, 58, 59, 60, int.MaxValue })
        {
            await using (var setup = PlatformContext())
                await setup.Set<EntregaInvitacion>().ExecuteUpdateAsync(s => s
                    .SetProperty(e => e.Intentos, intentos)
                    .SetProperty(e => e.Creada, DateTime.UtcNow.AddHours(-3))
                    .SetProperty(e => e.ProximoIntento, (DateTime?)null));
            await using var db = PlatformContext();
            DateTime fallo = default;
            var sender = new SenderControlado(async (_, _) =>
            {
                // Conexion distinta: consulta el reloj al ocurrir el fallo, sin sleeps.
                await using var reloj = PlatformContext(); fallo = await HoraOutboxAsync(reloj);
                throw new IOException("fallo-sintetico");
            });
            Assert.True(await new InvitacionesOutbox(db, sender, platformProtection).ProcesarUnaAsync(default));
            var despues = await HoraOutboxAsync(db);
            var fila = await db.Set<EntregaInvitacion>().AsNoTracking().SingleAsync();
            var minutos = (int)Math.Min((long)intentos + 1, 60);
            Assert.Equal((int)Math.Min((long)intentos + 1, int.MaxValue), fila.Intentos);
            Assert.InRange(fila.ProximoIntento!.Value, fallo.AddMinutes(minutos), despues.AddMinutes(minutos));
            Assert.False(await new InvitacionesOutbox(db, sender, platformProtection).ProcesarUnaAsync(default));
        }
    }

    [DatabaseFact]
    public async Task H1_DosWorkersOmitenFilaBloqueadaYNoDuplicanEntregas()
    {
        var actor = await InicializarPlataformaAsync();
        Guid primera, segunda;
        await using (var setup = PlatformContext())
        {
            await PlatformData(setup).CrearInstitucionAsync(actor, Alta("primera@tiza.test"), default);
            primera = (await setup.Set<EntregaInvitacion>().SingleAsync()).Id;
            await setup.Set<EntregaInvitacion>().ExecuteUpdateAsync(s => s.SetProperty(e => e.Creada, DateTime.UtcNow.AddHours(-1)));
            await PlatformData(setup).CrearInstitucionAsync(actor, Alta("segunda@tiza.test"), default);
            segunda = await setup.Set<EntregaInvitacion>().Where(e => e.Id != primera).Select(e => e.Id).SingleAsync();
        }
        var entered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var ids = new System.Collections.Concurrent.ConcurrentBag<Guid>();
        var sender = new SenderControlado(async (mensaje, ct) =>
        {
            ids.Add(mensaje.Id);
            if (mensaje.Id == primera) { entered.TrySetResult(true); await release.Task.WaitAsync(ct); }
        });
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        await using var db1 = PlatformContext(); await using var db2 = PlatformContext();
        var task = new InvitacionesOutbox(db1, sender, platformProtection).ProcesarUnaAsync(timeout.Token);
        try
        {
            await entered.Task.WaitAsync(timeout.Token); // Worker 1 conserva el lock durante el envio.
            var worker2 = new InvitacionesOutbox(db2, sender, platformProtection);
            Assert.True(await worker2.ProcesarUnaAsync(timeout.Token));
            Assert.False(await worker2.ProcesarUnaAsync(timeout.Token));
            Assert.Contains(segunda, ids);
        }
        finally { release.TrySetResult(true); await task; }
        Assert.Equal(new[] { primera, segunda }.OrderBy(x => x), ids.OrderBy(x => x));
        await using var verificar = PlatformContext();
        Assert.Equal(2, await verificar.Set<EntregaInvitacion>().CountAsync(e => e.Entregada != null));
    }

    [DatabaseFact]
    public async Task H1_FalloDePersistenciaRevierteFilaYConservaIdParaReenvio()
    {
        var actor = await InicializarPlataformaAsync(); EntregaInvitacion original;
        await using (var setup = PlatformContext())
        {
            await PlatformData(setup).CrearInstitucionAsync(actor, Alta(), default);
            original = await setup.Set<EntregaInvitacion>().AsNoTracking().SingleAsync();
            await setup.Database.ExecuteSqlRawAsync("""
                CREATE FUNCTION h1_fallo_guardado() RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN RAISE EXCEPTION 'fallo-escritura-sintetico'; END; $$;
                CREATE TRIGGER h1_fallo BEFORE UPDATE ON "EntregaInvitacion"
                FOR EACH ROW EXECUTE FUNCTION h1_fallo_guardado();
                """);
        }
        var ids = new List<Guid>();
        var sender = new SenderControlado((mensaje, _) => { ids.Add(mensaje.Id); return Task.CompletedTask; });
        await using (var db = PlatformContext())
            await Assert.ThrowsAsync<DbUpdateException>(() => new InvitacionesOutbox(db, sender, platformProtection).ProcesarUnaAsync(default));
        await using (var verificar = PlatformContext())
        {
            var fila = await verificar.Set<EntregaInvitacion>().SingleAsync();
            Assert.Equal(original.Id, fila.Id); Assert.Equal(0, fila.Intentos);
            Assert.Null(fila.Entregada); Assert.Null(fila.ProximoIntento);
            Assert.Equal(original.ContenidoProtegido, fila.ContenidoProtegido);
            await verificar.Database.ExecuteSqlRawAsync("DROP TRIGGER h1_fallo ON \"EntregaInvitacion\"; DROP FUNCTION h1_fallo_guardado();");
        }
        await using (var db = PlatformContext())
            Assert.True(await new InvitacionesOutbox(db, sender, platformProtection).ProcesarUnaAsync(default));
        Assert.Equal(new[] { original.Id, original.Id }, ids); // El proveedor debe deduplicar por este Id estable.
    }

    [DatabaseFact]
    public async Task H1_MigracionPreservaEntregaExistenteEIndiceCoincideConConsulta()
    {
        var actor = await InicializarPlataformaAsync(); EntregaInvitacion original;
        await using (var setup = PlatformContext())
        {
            await PlatformData(setup).CrearInstitucionAsync(actor, Alta(), default);
            original = await setup.Set<EntregaInvitacion>().AsNoTracking().SingleAsync();
            await setup.GetService<IMigrator>().MigrateAsync("20261021031817_PlatformInvitations");
            await setup.Database.MigrateAsync();
        }
        await using var db = PlatformContext();
        var fila = await db.Set<EntregaInvitacion>().AsNoTracking().SingleAsync();
        Assert.Equal(original.Id, fila.Id); Assert.Equal(original.ContenidoProtegido, fila.ContenidoProtegido);
        Assert.Equal(original.Creada, fila.Creada); Assert.Equal(original.Intentos, fila.Intentos);
        Assert.Equal(original.Destinatario, fila.Destinatario); Assert.Null(fila.ProximoIntento);
        Assert.Equal(original.InvitacionId, fila.InvitacionId); Assert.Equal(original.Entregada, fila.Entregada);
        Assert.False(db.Database.HasPendingModelChanges());
        var index = await db.Database.SqlQuery<string>($"""
            SELECT indexdef AS "Value" FROM pg_indexes
            WHERE schemaname = {schema} AND indexname = 'IX_EntregaInvitacion_PendienteElegible'
            """).SingleAsync();
        Assert.Contains("COALESCE", index); Assert.Contains("WHERE", index);
        Assert.True(await new InvitacionesOutbox(db, new SenderControlado((_, _) => Task.CompletedTask), platformProtection).ProcesarUnaAsync(default));
    }
}
