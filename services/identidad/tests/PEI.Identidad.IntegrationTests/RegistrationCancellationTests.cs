using System.Data.Common;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Npgsql;
using PEI.Identidad.Application.UseCases.Auth.Register;
using PEI.Identidad.Application.Interfaces.Security;
using PEI.Identidad.Infrastructure.Persistence;
using PEI.Identidad.Infrastructure.Repositories;
using PEI.Identidad.Infrastructure.Security;

namespace PEI.Identidad.IntegrationTests;

public sealed partial class TenantIsolationTests
{
    private HttpContextAccessor H3Actor() => new()
    {
        HttpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(new[]
            {
                new Claim("sub", ua.ToString()), new Claim("role", "Administrador"), new Claim("institucion_id", a.ToString())
            }, "test", "sub", "role"))
        }
    };
    private IdentidadDbContext H3Context(IInterceptor? interceptor = null, string? application = null)
    {
        var builder = new NpgsqlConnectionStringBuilder(connection) { ApplicationName = application ?? "h3-test" };
        var options = new DbContextOptionsBuilder<IdentidadDbContext>().UseNpgsql(builder.ConnectionString,
            o => o.MigrationsHistoryTable("__EFMigrationsHistory", schema));
        if (interceptor is not null) options.AddInterceptors(interceptor);
        return new(options.Options, new Tenant(a));
    }
    private async Task H3PrepararAsync()
    {
        await InicializarPlataformaAsync();
        await using var db = Context(a);
        await db.Usuarios.Where(u => u.Id == ua).ExecuteUpdateAsync(s => s.SetProperty(u => u.Rol, "Administrador"));
    }
    private static RegisterCommand H3Command(string email) => new()
    { Email = email, Password = "Sintetica123!", Nombre = "Prueba", Apellido = "Cancelacion", Rol = "Alumno" };

    private sealed class CancelarDespuesDeGuardar(CancellationTokenSource source, string email) : SaveChangesInterceptor
    {
        public bool UsuarioInsertado { get; private set; }
        public bool ReservaInsertada { get; private set; }
        public CancellationToken Recibido { get; private set; }
        public override async ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData data, int result, CancellationToken ct = default)
        {
            Recibido = ct;
            var db = data.Context!;
            UsuarioInsertado = await db.Database.SqlQuery<int>($"""
                SELECT count(*)::int AS "Value" FROM "Usuario" WHERE "Email" = {email}
                """).SingleAsync() == 1;
            ReservaInsertada = await db.Database.SqlQuery<int>($"""
                SELECT count(*)::int AS "Value" FROM "RegistroEmail" WHERE "EmailNormalizado" = {email}
                """).SingleAsync() == 1;
            source.Cancel(); // INSERT y trigger ejecutados, todavia dentro de la transaccion exterior.
            return result;
        }
    }

    [DatabaseFact]
    public async Task H3_CancelacionDespuesDeInsertarAntesDeCommitRevierteUsuarioYReserva()
    {
        await H3PrepararAsync();
        const string email = "h3-rollback@test.invalid";
        using var cancellation = new CancellationTokenSource();
        var interceptor = new CancelarDespuesDeGuardar(cancellation, email);
        await using (var db = H3Context(interceptor))
        {
            await using var data = new RegistrationDataAccess(db, H3Actor());
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new RegisterHandler(data, new BCryptPasswordHasher(), new Tenant(a))
                .HandleAsync(H3Command(email), cancellation.Token));
            Assert.True(interceptor.UsuarioInsertado); Assert.True(interceptor.ReservaInsertada);
            Assert.Equal(cancellation.Token, interceptor.Recibido);
            Assert.Null(db.Database.CurrentTransaction);
        }
        await using var verificar = Context(null);
        Assert.False(await verificar.Usuarios.IgnoreQueryFilters().AnyAsync(u => u.Email == email));
        await using var plataforma = PlatformContext();
        Assert.False(await plataforma.RegistrosEmail.AnyAsync(r => r.EmailNormalizado == email));
    }

    private sealed class HashCancelado(CancellationTokenSource cancellation) : IPasswordHasher
    {
        public string Hash(string password) { cancellation.Cancel(); return "no-se-persiste"; }
        public bool Verify(string password, string hash) => throw new NotSupportedException();
        public void VerifyDummy(string password) => throw new NotSupportedException();
    }

    [DatabaseFact]
    public async Task H3_CancelacionDuranteHashNoInsertaYScopeLiberaLocks()
    {
        await H3PrepararAsync();
        const string email = "h3-scope@test.invalid";
        using var cancellation = new CancellationTokenSource();
        await using (var db = H3Context())
        {
            await using var data = new RegistrationDataAccess(db, H3Actor());
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new RegisterHandler(data, new HashCancelado(cancellation), new Tenant(a))
                .HandleAsync(H3Command(email), cancellation.Token));
            Assert.Empty(db.ChangeTracker.Entries<PEI.Identidad.Domain.Entities.Usuario>());
            Assert.NotNull(db.Database.CurrentTransaction); // El scope dispone el puerto aun sin llegar a SaveChanges.
        }
        await using var peer = new NpgsqlConnection(connection); await peer.OpenAsync();
        await using (var cmd = new NpgsqlCommand("""
            SELECT u."Id" FROM "Usuario" u JOIN "Institucion" i ON i."Id" = u."InstitucionId"
            WHERE u."Id" = @id FOR UPDATE OF u, i NOWAIT
            """, peer))
        {
            cmd.Parameters.AddWithValue("id", ua); Assert.Equal(ua, await cmd.ExecuteScalarAsync());
        }
        await using var verificar = Context(null);
        Assert.False(await verificar.Usuarios.IgnoreQueryFilters().AnyAsync(u => u.Email == email));
        await using var plataforma = PlatformContext();
        Assert.False(await plataforma.RegistrosEmail.AnyAsync(r => r.EmailNormalizado == email));
    }

    private sealed class CancelarAlConfirmar(CancellationTokenSource source) : DbTransactionInterceptor
    {
        public bool Invocado { get; private set; }
        public CancellationToken Recibido { get; private set; }
        public override ValueTask<InterceptionResult> TransactionCommittingAsync(DbTransaction tx, TransactionEventData data,
            InterceptionResult result, CancellationToken ct = default)
        {
            Invocado = true; Recibido = ct; source.Cancel();
            return ValueTask.FromResult(result);
        }
    }

    [DatabaseFact]
    public async Task H3_CancelacionEnLimiteDeCommitEsperaConfirmacionCompleta()
    {
        await H3PrepararAsync();
        const string email = "h3-commit@test.invalid";
        using var cancellation = new CancellationTokenSource();
        var interceptor = new CancelarAlConfirmar(cancellation);
        Guid id;
        await using (var db = H3Context(interceptor))
        {
            await using var data = new RegistrationDataAccess(db, H3Actor());
            id = await new RegisterHandler(data, new BCryptPasswordHasher(), new Tenant(a)).HandleAsync(H3Command(email), cancellation.Token);
            Assert.True(interceptor.Invocado); Assert.True(cancellation.IsCancellationRequested);
            Assert.Equal(CancellationToken.None, interceptor.Recibido);
            Assert.Null(db.Database.CurrentTransaction);
        }
        await using var verificar = Context(null);
        Assert.True(await verificar.Usuarios.IgnoreQueryFilters().AnyAsync(u => u.Id == id && u.Email == email));
        await using var plataforma = PlatformContext();
        Assert.True(await plataforma.RegistrosEmail.AnyAsync(r => r.EmailNormalizado == email && r.CuentaId == id));
    }

    [DatabaseFact]
    public async Task H3_CancelarEsperaDeLockRevierteTransaccionYNoLlegaAInsertar()
    {
        await H3PrepararAsync(); var nombre = schema + "_h3";
        await using var observer = new NpgsqlConnection(connection); await observer.OpenAsync();
        await using var blocker = new NpgsqlConnection(connection); await blocker.OpenAsync();
        await using var tx = await blocker.BeginTransactionAsync();
        await using (var cmd = new NpgsqlCommand("SELECT \"Id\" FROM \"Usuario\" WHERE \"Id\" = @id FOR UPDATE", blocker, tx))
        {
            cmd.Parameters.AddWithValue("id", ua); Assert.NotNull(await cmd.ExecuteScalarAsync());
        }
        using var cancellation = new CancellationTokenSource();
        await using var db = H3Context(application: nombre);
        await using var data = new RegistrationDataAccess(db, H3Actor());
        var request = new RegisterHandler(data, new BCryptPasswordHasher(), new Tenant(a)).HandleAsync(H3Command("h3-lock@test.invalid"), cancellation.Token);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(25));
        while (true)
        {
            await using var query = new NpgsqlCommand("""
                SELECT count(*) FROM pg_stat_activity WHERE datname = current_database() AND application_name = @name
                  AND wait_event_type = 'Lock' AND cardinality(pg_blocking_pids(pid)) > 0
                """, observer);
            query.Parameters.AddWithValue("name", nombre);
            if (Convert.ToInt32(await query.ExecuteScalarAsync(timeout.Token)) == 1) break;
            await timer.WaitForNextTickAsync(timeout.Token); // Observacion de una barrera real, no orden por sleeps.
        }
        Assert.False(request.IsCompleted);
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => request.WaitAsync(TimeSpan.FromSeconds(10)));
        Assert.Null(db.Database.CurrentTransaction);
        await using (var query = new NpgsqlCommand("SELECT count(*) FROM pg_stat_activity WHERE application_name = @name AND xact_start IS NOT NULL", observer))
        {
            query.Parameters.AddWithValue("name", nombre); Assert.Equal(0L, await query.ExecuteScalarAsync());
        }
        await tx.RollbackAsync();
        await using var verificar = Context(null);
        Assert.False(await verificar.Usuarios.IgnoreQueryFilters().AnyAsync(u => u.Email == "h3-lock@test.invalid"));
    }
}
