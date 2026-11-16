using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using PEI.Identidad.Api.Security;
using PEI.Identidad.Domain.Entities;
using PEI.Identidad.Infrastructure.Persistence;
using PEI.Identidad.Infrastructure.Plataforma;

namespace PEI.Identidad.IntegrationTests;

public sealed partial class TenantIsolationTests
{
    private async Task<(Guid UsuarioId, string Token)> CrearInvitacionPendienteAsync(
        string rol = "Alumno", DateTime? vence = null)
    {
        var usuarioId = Guid.NewGuid();
        var invitacionId = Guid.NewGuid();
        var token = Base64UrlEncoder.Encode(RandomNumberGenerator.GetBytes(32));
        await using (var migraciones = PlatformContext())
            await migraciones.Database.MigrateAsync();
        await using var db = Context(null);
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO "Usuario" ("Id","InstitucionId","Email","PasswordHash","Nombre","Apellido","Rol","FechaCreacion","Activo")
            VALUES ({usuarioId},{a},{usuarioId.ToString("N") + "@test.invalid"},'!pendiente-activacion',
                   'Alumno','Prueba',{rol},{DateTime.UtcNow},FALSE)
            """);
        await using var plataforma = PlatformContext();
        plataforma.Add(new InvitacionAdministrador
        {
            Id = invitacionId,
            UsuarioId = usuarioId,
            TokenHash = PlataformaDataAccess.HashToken(token),
            Vencimiento = vence ?? DateTime.UtcNow.AddHours(24)
        });
        await plataforma.SaveChangesAsync();
        return (usuarioId, token);
    }

    [DatabaseFact]
    public async Task ActivacionInstitucionalDefinePasswordUnaSolaVez()
    {
        var (usuarioId, token) = await CrearInvitacionPendienteAsync();
        using var factory = new PlatformPostgresFactory(connection, schema, platformProtection, platformEnabled: false);
        using var client = factory.Client();

        var response = await client.PostAsJsonAsync("/api/auth/invitaciones/alumnos/activar",
            new { Token = token, Password = "AlumnoSeguro123!" });
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/auth/invitaciones/alumnos/activar",
            new { Token = token, Password = "OtraClave123!" })).StatusCode);

        await using var db = Context(a);
        var usuario = await db.Usuarios.SingleAsync(u => u.Id == usuarioId);
        Assert.True(usuario.Activo);
        Assert.Equal("Alumno", usuario.Rol);
        Assert.True(BCrypt.Net.BCrypt.Verify("AlumnoSeguro123!", usuario.PasswordHash));
        await using var plataforma = PlatformContext();
        var invitacion = await plataforma.Set<InvitacionAdministrador>().SingleAsync(i => i.UsuarioId == usuarioId);
        Assert.NotNull(invitacion.Utilizada);
    }

    [DatabaseFact]
    public async Task ActivacionInstitucionalRechazaOtrosRolesYTokensVencidos()
    {
        var administrador = await CrearInvitacionPendienteAsync("Administrador");
        var docente = await CrearInvitacionPendienteAsync("Docente");
        var vencida = await CrearInvitacionPendienteAsync("Alumno", DateTime.UtcNow.AddHours(-1));
        using var factory = new PlatformPostgresFactory(connection, schema, platformProtection, platformEnabled: false);
        using var client = factory.Client();

        foreach (var (_, token) in new[] { administrador, docente, vencida })
        {
            var response = await client.PostAsJsonAsync("/api/auth/invitaciones/alumnos/activar",
                new { Token = token, Password = "AlumnoSeguro123!" });
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }
        await using var db = Context(a);
        foreach (var (usuarioId, _) in new[] { administrador, docente, vencida })
        {
            var usuario = await db.Usuarios.SingleAsync(u => u.Id == usuarioId);
            Assert.False(usuario.Activo);
            Assert.Equal("!pendiente-activacion", usuario.PasswordHash);
        }
    }

    [DatabaseFact]
    public async Task OutboxYSenderOperanConPlatformDeshabilitadoSinHabilitarEndpointsGlobales()
    {
        var actor = await InicializarPlataformaAsync();
        await using (var db = PlatformContext())
            await PlatformData(db).CrearInstitucionAsync(actor, Alta(), default);

        using var factory = new PlatformPostgresFactory(connection, schema, platformProtection, platformEnabled: false);
        using var client = factory.Client();
        Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsJsonAsync("/api/plataforma/auth/login",
            new { Email = "global@tiza.test", Password = "GlobalSegura123!" })).StatusCode);
        Assert.Null(await factory.Services.GetRequiredService<Microsoft.AspNetCore.Authentication.IAuthenticationSchemeProvider>()
            .GetSchemeAsync(PlataformaSetup.Scheme));
        Assert.Contains(factory.Services.GetServices<IHostedService>(), service => service is InvitacionesWorker);
        await using (var scope = factory.Services.CreateAsyncScope())
            Assert.NotNull(scope.ServiceProvider.GetRequiredService<InvitacionesOutbox>());

        for (var attempt = 0; attempt < 50 && factory.Sender.Enviados.Count == 0; attempt++)
            await Task.Delay(100);
        Assert.Single(factory.Sender.Enviados);
        DateTime? entregada = null;
        for (var attempt = 0; attempt < 50 && entregada is null; attempt++)
        {
            await using var dbCheck = PlatformContext();
            entregada = await dbCheck.Set<EntregaInvitacion>()
                .Where(x => x.Entregada != null)
                .Select(x => x.Entregada)
                .SingleOrDefaultAsync();
            if (entregada is null) await Task.Delay(100);
        }
        Assert.NotNull(entregada);
    }
}
