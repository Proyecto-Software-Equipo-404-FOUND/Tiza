using Microsoft.EntityFrameworkCore;
using Npgsql;
using PEI.Identidad.Application.Abstractions;
using PEI.Identidad.Application.Exceptions;
using PEI.Identidad.Domain.Entities;
using PEI.Identidad.Infrastructure.Authentication;
using PEI.Identidad.Infrastructure.Persistence;

namespace PEI.Identidad.IntegrationTests;

public sealed class DatabaseFactAttribute : FactAttribute
{
    public DatabaseFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("PEI_IDENTIDAD_TEST_CONNECTION")))
            Skip = "Falta conexion dedicada PEI_IDENTIDAD_TEST_CONNECTION.";
    }
}

// Cada caso posee un esquema aleatorio propio. Nunca se borran bases ni datos precargados.
public sealed partial class TenantIsolationTests : IAsyncLifetime
{
    private readonly string schema = "pei_test_" + Guid.NewGuid().ToString("N");
    private string connection = "";
    private bool creado;
    private readonly Guid a = Guid.NewGuid(), b = Guid.NewGuid();
    private readonly Guid ua = Guid.NewGuid(), ub = Guid.NewGuid(), ua2 = Guid.NewGuid();
    private readonly string hash = Guid.NewGuid().ToString("N");
    private sealed record Tenant(Guid? InstitucionId) : ITenantContext;
    private IdentidadDbContext Context(Guid? tenant) => new(
        new DbContextOptionsBuilder<IdentidadDbContext>().UseNpgsql(connection, o => o.MigrationsHistoryTable("__EFMigrationsHistory", schema)).Options, new Tenant(tenant));

    public async Task InitializeAsync()
    {
        var raw = Environment.GetEnvironmentVariable("PEI_IDENTIDAD_TEST_CONNECTION");
        if (string.IsNullOrWhiteSpace(raw)) return;
        var builder = new NpgsqlConnectionStringBuilder(raw);
        if (builder.Database != "pei_identidad_tests" || builder.Port != 5541 ||
            builder.Host is not ("localhost" or "127.0.0.1" or "::1"))
            throw new InvalidOperationException("Solo se autoriza pei_identidad_tests local en puerto 5541.");
        // A different SearchPath per case creates a different pool. Release physical connections
        // on disposal instead of retaining dozens of idle pools until the test process exits.
        builder.Pooling = false;
        await using var cn = new NpgsqlConnection(builder.ConnectionString);
        await cn.OpenAsync();
        await using (var check = new NpgsqlCommand("SELECT current_database()", cn))
            if ((string?)await check.ExecuteScalarAsync() != "pei_identidad_tests")
                throw new InvalidOperationException("Destino real no autorizado.");
        await using (var create = new NpgsqlCommand($"CREATE SCHEMA {schema}", cn))
            await create.ExecuteNonQueryAsync();
        creado = true;
        builder.SearchPath = schema;
        connection = builder.ConnectionString;
        await using var db = Context(null);
        await db.Database.MigrateAsync();
        foreach (var id in new[] { a, b })
            await db.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO "Institucion" ("Id","Nombre","Codigo","FechaCreacion","Activo")
                VALUES ({id}, 'Prueba', {id.ToString("N")[..20]}, {DateTime.UtcNow}, TRUE)
                """);
        foreach (var pair in new[] { (ua,a), (ua2,a), (ub,b) })
            await db.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO "Usuario" ("Id","InstitucionId","Email","PasswordHash","Nombre","Apellido","Rol","FechaCreacion","Activo")
                VALUES ({pair.Item1},{pair.Item2},{pair.Item1.ToString()+"@test.invalid"},'ficticio','Nombre','Apellido','Docente',{DateTime.UtcNow},TRUE)
                """);
        await new AuthenticationTokenWriter(db).CrearRefreshTokenAsync(ua, hash, DateTime.UtcNow.AddHours(1));
    }

    public async Task DisposeAsync()
    {
        if (!creado) return;
        await using var cn = new NpgsqlConnection(connection);
        await cn.OpenAsync();
        // schema solo contiene el prefijo constante y un GUID generado por este caso.
        await using var drop = new NpgsqlCommand($"DROP SCHEMA {schema} CASCADE", cn);
        await drop.ExecuteNonQueryAsync();
    }

    [DatabaseFact]
    public async Task ConsultasAisladas()
    {
        await using var da = Context(a); await using var db = Context(b); await using var dn = Context(null);
        Assert.Equal(2, await da.Usuarios.CountAsync()); Assert.Single(await db.Usuarios.ToListAsync());
        Assert.Empty(await dn.Usuarios.ToListAsync()); Assert.Single(await da.RefreshTokens.ToListAsync());
        Assert.Empty(await db.RefreshTokens.ToListAsync()); Assert.Empty(await dn.Instituciones.ToListAsync());
    }

    [DatabaseFact]
    public async Task TrasladoMasivoRechazado()
    {
        await using (var db = Context(a))
            await Assert.ThrowsAsync<PostgresException>(() => db.Usuarios.Where(u => u.Id == ua)
                .ExecuteUpdateAsync(s => s.SetProperty(u => u.InstitucionId, b)));
        await using var verificar = Context(a);
        Assert.Equal(a, (await verificar.Usuarios.SingleAsync(u => u.Id == ua)).InstitucionId);
    }

    [DatabaseFact]
    public async Task InvariantesTokensEnTodasLasRutas()
    {
        foreach (var propietario in new[] { ua2, ub })
        {
            await using var db = Context(a);
            var token = await db.RefreshTokens.SingleAsync(); token.UsuarioId = propietario;
            await Assert.ThrowsAsync<TenantAccessDeniedException>(() => db.SaveChangesAsync());
            await Assert.ThrowsAsync<PostgresException>(() => db.RefreshTokens.ExecuteUpdateAsync(
                s => s.SetProperty(t => t.UsuarioId, propietario)));
        }
        await using (var db = Context(a))
        {
            await Assert.ThrowsAsync<PostgresException>(() => db.RefreshTokens.ExecuteUpdateAsync(
                s => s.SetProperty(t => t.Token, "otro")));
            await Assert.ThrowsAsync<PostgresException>(() => db.RefreshTokens.ExecuteUpdateAsync(
                s => s.SetProperty(t => t.FechaExpiracion, DateTime.UtcNow.AddDays(10))));
            await db.RefreshTokens.ExecuteUpdateAsync(s => s.SetProperty(t => t.Revocado, true));
            await Assert.ThrowsAsync<PostgresException>(() => db.RefreshTokens.ExecuteUpdateAsync(
                s => s.SetProperty(t => t.Revocado, false)));
        }
        await using var verificar = Context(a);
        var actual = await verificar.RefreshTokens.SingleAsync();
        Assert.Equal(ua, actual.UsuarioId); Assert.Equal(hash, actual.Token); Assert.True(actual.Revocado);
    }

    [DatabaseFact]
    public async Task EscriturasYCreacionConjunta()
    {
        await using (var db = Context(a))
        {
            var usuario = new Usuario { InstitucionId = a, Email = Guid.NewGuid()+"@test.invalid", Nombre="Nuevo", Apellido="Usuario", Rol="Docente", PasswordHash="ficticio" };
            db.Usuarios.Add(usuario);
            db.RefreshTokens.Add(new RefreshToken { UsuarioId=usuario.Id, Token=Guid.NewGuid().ToString("N"), FechaExpiracion=DateTime.UtcNow.AddHours(1) });
            await db.SaveChangesAsync();
        }
        await using var verificar = Context(a);
        Assert.Equal(3, await verificar.Usuarios.CountAsync()); Assert.Equal(2, await verificar.RefreshTokens.CountAsync());
        var ajeno = await verificar.Usuarios.IgnoreQueryFilters().SingleAsync(u => u.Id == ub);
        verificar.Usuarios.Remove(ajeno);
        await Assert.ThrowsAsync<TenantAccessDeniedException>(() => verificar.SaveChangesAsync());
    }

    [DatabaseFact]
    public async Task RevocacionCuentaInactiva()
    {
        await using var db = Context(a);
        await db.Usuarios.ExecuteUpdateAsync(s => s.SetProperty(u => u.Activo, false));
        var writer = new AuthenticationTokenWriter(db);
        Assert.True(await writer.RevocarRefreshTokenAsync(hash));
        await db.Usuarios.ExecuteUpdateAsync(s => s.SetProperty(u => u.Activo, true));
        Assert.Null(await new AuthenticationDataAccess(db).BuscarIdentidadParaRenovacionAsync(hash));
    }

    [DatabaseFact]
    public async Task RotacionConcurrenteUnSoloGanador()
    {
        async Task<bool> Rotar() { await using var db = Context(null); return await new AuthenticationTokenWriter(db)
            .RotarRefreshTokenAsync(hash, Guid.NewGuid().ToString("N"), DateTime.UtcNow.AddHours(1)); }
        var resultados = await Task.WhenAll(Rotar(), Rotar()); Assert.Single(resultados.Where(x => x));
        await using var verificar = Context(a);
        Assert.Equal(1, await verificar.RefreshTokens.CountAsync(t => !t.Revocado));
        Assert.True((await verificar.RefreshTokens.SingleAsync(t => t.Token == hash)).Revocado);
    }

    [DatabaseFact]
    public async Task IntegridadReferencial()
    {
        await using var db = Context(a);
        await Assert.ThrowsAsync<PostgresException>(() => db.Instituciones.ExecuteDeleteAsync());
        await db.Usuarios.Where(u => u.Id == ua).ExecuteDeleteAsync();
        await using var verificar = Context(a);
        Assert.Empty(await verificar.RefreshTokens.ToListAsync());
    }

    [DatabaseFact]
    public async Task AlteracionRastreadaRechazada()
    {
        for (var caso = 0; caso < 3; caso++)
        {
            await using var db = Context(a);
            var token = await db.RefreshTokens.SingleAsync();
            if (caso == 0) token.Token = "alterado";
            if (caso == 1) token.FechaExpiracion = token.FechaExpiracion.AddDays(1);
            if (caso == 2)
            {
                await new AuthenticationTokenWriter(db).RevocarRefreshTokenAsync(hash);
                token.Revocado = false;
                db.Entry(token).Property(t => t.Revocado).IsModified = true;
            }
            await Assert.ThrowsAsync<TenantAccessDeniedException>(() => db.SaveChangesAsync());
        }
        await using var verificar = Context(a);
        Assert.Equal(hash, (await verificar.RefreshTokens.SingleAsync()).Token);
        Assert.True((await verificar.RefreshTokens.SingleAsync()).Revocado);
    }

    [DatabaseFact]
    public async Task SinTenantYInstitucionAjenaNoPermitenEscrituras()
    {
        await using (var db = Context(null))
        {
            db.Usuarios.Add(new Usuario { InstitucionId = a });
            await Assert.ThrowsAsync<TenantAccessDeniedException>(() => db.SaveChangesAsync());
        }
        await using (var db = Context(a))
        {
            var institucion = await db.Instituciones.IgnoreQueryFilters().SingleAsync(i => i.Id == b);
            institucion.Nombre = "Ajena";
            await Assert.ThrowsAsync<TenantAccessDeniedException>(() => db.SaveChangesAsync());
        }
        await using (var db = Context(a))
        {
            db.Instituciones.Add(new Institucion { Nombre = "Nueva", Codigo = "NUEVA" });
            await Assert.ThrowsAsync<TenantAccessDeniedException>(() => db.SaveChangesAsync());
        }
        await using var verificar = Context(b);
        Assert.Equal("Prueba", (await verificar.Instituciones.SingleAsync()).Nombre);
    }

    [DatabaseFact]
    public async Task InstitucionInactivaBloqueaEmisionYPermiteRevocar()
    {
        await using var db = Context(a);
        await db.Instituciones.ExecuteUpdateAsync(s => s.SetProperty(i => i.Activo, false));
        var writer = new AuthenticationTokenWriter(db);
        await Assert.ThrowsAsync<AuthenticationIdentityUnavailableException>(() => writer.CrearRefreshTokenAsync(ua,
            Guid.NewGuid().ToString("N"), DateTime.UtcNow.AddHours(1)));
        Assert.False(await writer.RotarRefreshTokenAsync(hash, Guid.NewGuid().ToString("N"), DateTime.UtcNow.AddHours(1)));
        Assert.Null(await new AuthenticationDataAccess(db).BuscarIdentidadParaRenovacionAsync(hash));
        Assert.True(await writer.RevocarRefreshTokenAsync(hash));
        await db.Instituciones.ExecuteUpdateAsync(s => s.SetProperty(i => i.Activo, true));
        await using var verificar = Context(a);
        Assert.True((await verificar.RefreshTokens.SingleAsync()).Revocado);
    }

    [DatabaseFact]
    public async Task FalloDeReemplazoNoConsumeAnterior()
    {
        await using var db = Context(null);
        var writer = new AuthenticationTokenWriter(db);
        var ocupado = Guid.NewGuid().ToString("N");
        await writer.CrearRefreshTokenAsync(ua2, ocupado, DateTime.UtcNow.AddHours(1));
        var error = await Assert.ThrowsAsync<PostgresException>(() => writer.RotarRefreshTokenAsync(
            hash, ocupado, DateTime.UtcNow.AddHours(1)));
        Assert.Equal(PostgresErrorCodes.UniqueViolation, error.SqlState);
        await using var verificar = Context(a);
        Assert.False((await verificar.RefreshTokens.SingleAsync(t => t.Token == hash)).Revocado);
    }

    [DatabaseFact]
    public async Task SqlDirectoRespetaInvariantes()
    {
        await using var db = Context(null);
        var error = await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE "Usuario" SET "InstitucionId" = {b} WHERE "Id" = {ua}
            """));
        Assert.Equal(PostgresErrorCodes.CheckViolation, error.SqlState);
        await using var verificar = Context(a);
        Assert.Equal(a, (await verificar.Usuarios.SingleAsync(u => u.Id == ua)).InstitucionId);
    }

    [DatabaseFact]
    public async Task ActualizacionObsoletaNoSobrescribeCambios()
    {
        await using var primero = Context(a);
        await using var segundo = Context(a);
        var uno = await primero.Usuarios.SingleAsync(u => u.Id == ua);
        var dos = await segundo.Usuarios.SingleAsync(u => u.Id == ua);
        uno.Nombre = "Ganador";
        await primero.SaveChangesAsync();
        dos.Nombre = "Obsoleto";
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => segundo.SaveChangesAsync());
        await using var verificar = Context(a);
        Assert.Equal("Ganador", (await verificar.Usuarios.SingleAsync(u => u.Id == ua)).Nombre);
    }
}
