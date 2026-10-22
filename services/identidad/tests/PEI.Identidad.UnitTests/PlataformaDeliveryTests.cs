using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using PEI.Identidad.Application.Plataforma;
using PEI.Identidad.Domain.Entities;
using PEI.Identidad.Infrastructure.Plataforma;
using PEI.Identidad.Infrastructure.Security;
using PEI.Identidad.Infrastructure.Tenancy;
using System.Security.Claims;

namespace PEI.Identidad.UnitTests;

public sealed class PlataformaDeliveryTests
{
    [Fact]
    public async Task BuzonDesarrolloCifraMensajeYDeduplicaPorId()
    {
        var directory = Path.Combine(Path.GetTempPath(), "tiza-mail-" + Guid.NewGuid().ToString("N"));
        var provider = new EphemeralDataProtectionProvider();
        var sender = new DevelopmentEmailSender(new PlataformaOptions { DevelopmentMailDirectory = directory }, provider);
        var id = Guid.NewGuid(); var mensaje = new MensajeEmail(id, "admin@tiza.test", "enlace-confidencial-de-prueba");
        var path = Path.Combine(directory, id.ToString("N") + ".mail");
        try
        {
            await sender.EnviarAsync(mensaje, default);
            var antes = await File.ReadAllTextAsync(path);
            await sender.EnviarAsync(mensaje, default);
            var despues = await File.ReadAllTextAsync(path);
            Assert.Equal(antes, despues); Assert.DoesNotContain(mensaje.Contenido, despues);
            Assert.Contains(mensaje.Contenido, provider.CreateProtector(DevelopmentEmailSender.Purpose).Unprotect(despues));
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
            if (Directory.Exists(directory)) Directory.Delete(directory);
        }
    }

    [Fact]
    public void IdentidadPlataformaNoTieneTenantNiRelacionInstitucional()
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim("sub", Guid.NewGuid().ToString()),
            new Claim("role", "AdministradorGlobal"), new Claim("identity_type", "platform") }, "PlatformBearer", "sub", "role"));
        Assert.True(PlataformaJwtContract.TryRead(principal, out _));
        var tenant = new TenantContext(new HttpContextAccessor { HttpContext = new DefaultHttpContext { User = principal } });
        Assert.Null(tenant.InstitucionId);
        Assert.Null(typeof(CuentaPlataforma).GetProperty("InstitucionId"));
        Assert.False(JwtContract.TryRead(principal, out _, out _, out _));
    }
}
