using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.Extensions.Options;
using PEI.Identidad.Application.UseCases.Auth.Invitado;
using PEI.Identidad.Infrastructure.Security;
using Xunit;

namespace PEI.Identidad.UnitTests;

// S3-SA-01 (#231): ingreso de alumno invitado con PIN + nombre.
public sealed class IngresoInvitadoTests
{
    private static readonly JwtOptions Jwt = new()
    {
        SecretKey = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64)),
        Issuer = "invitado-test",
        Audience = "invitado-api"
    };

    private readonly Guid _salaId = Guid.NewGuid();
    private readonly Guid _institucionId = Guid.NewGuid();

    private IngresoInvitadoHandler Handler(SalaParaInvitado? sala, bool falla = false) =>
        new(new SalasFalsas(sala, falla), new JwtTokenService(Options.Create(Jwt)));

    private SalaParaInvitado Sala(bool permite = true, string estado = "Abierta") =>
        new(_salaId, _institucionId, permite, estado);

    [Fact]
    public async Task SalaQueAdmiteInvitados_EmiteTokenDeInvitadoParaEsaSala()
    {
        var antes = DateTime.UtcNow;

        var respuesta = await Handler(Sala()).HandleAsync(new IngresoInvitadoCommand(482913, "  Lucía Fernández "), default);

        Assert.Equal(ResultadoIngresoInvitado.Ok, respuesta.Resultado);
        var sesion = respuesta.Sesion!;
        Assert.Equal(_salaId, sesion.SalaId);
        Assert.Equal("Lucía Fernández", sesion.Nombre);
        Assert.InRange(sesion.ExpiraEnUtc, antes.AddHours(4).AddSeconds(-5), DateTime.UtcNow.AddHours(4).AddSeconds(5));

        var token = new JwtSecurityTokenHandler().ReadJwtToken(sesion.AccessToken);
        Assert.Equal("Invitado", token.Claims.Single(c => c.Type == "role").Value);
        Assert.Equal(_salaId.ToString(), token.Claims.Single(c => c.Type == "sala_id").Value);
        Assert.Equal(_institucionId.ToString(), token.Claims.Single(c => c.Type == "institucion_id").Value);
        Assert.Equal("Lucía Fernández", token.Claims.Single(c => c.Type == "name").Value);
        Assert.DoesNotContain(token.Claims, c => c.Type == "email");
    }

    [Fact]
    public async Task TokenDeInvitado_NoEsUnaIdentidadInstitucionalValida()
    {
        // El rol Invitado no es canónico: Identidad (y los servicios que validan igual) lo rechazan.
        var respuesta = await Handler(Sala()).HandleAsync(new IngresoInvitadoCommand(482913, "Lucía"), default);
        var token = new JwtSecurityTokenHandler().ReadJwtToken(respuesta.Sesion!.AccessToken);
        var principal = new ClaimsPrincipal(new ClaimsIdentity(token.Claims, "test"));

        Assert.False(JwtContract.TryRead(principal, out _, out _, out _));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" A ")]
    [InlineData("1234567890123456789012345678901234567890123456789012345678901")]
    public async Task NombreFueraDeRango_NombreInvalido(string? nombre)
    {
        var respuesta = await Handler(Sala()).HandleAsync(new IngresoInvitadoCommand(482913, nombre), default);
        Assert.Equal(ResultadoIngresoInvitado.NombreInvalido, respuesta.Resultado);
        Assert.Null(respuesta.Sesion);
    }

    [Fact]
    public async Task SalaSinInvitados_SalaSinInvitados()
    {
        var respuesta = await Handler(Sala(permite: false)).HandleAsync(new IngresoInvitadoCommand(482913, "Lucía"), default);
        Assert.Equal(ResultadoIngresoInvitado.SalaSinInvitados, respuesta.Resultado);
    }

    [Fact]
    public async Task SalaInexistente_SalaNoEncontrada()
    {
        var respuesta = await Handler(null).HandleAsync(new IngresoInvitadoCommand(482913, "Lucía"), default);
        Assert.Equal(ResultadoIngresoInvitado.SalaNoEncontrada, respuesta.Resultado);
    }

    [Fact]
    public async Task SalaFinalizada_SalaFinalizada()
    {
        var respuesta = await Handler(Sala(estado: "Finalizada")).HandleAsync(new IngresoInvitadoCommand(482913, "Lucía"), default);
        Assert.Equal(ResultadoIngresoInvitado.SalaFinalizada, respuesta.Resultado);
    }

    [Fact]
    public async Task SalaNoResponde_SalaNoDisponible()
    {
        var respuesta = await Handler(Sala(), falla: true).HandleAsync(new IngresoInvitadoCommand(482913, "Lucía"), default);
        Assert.Equal(ResultadoIngresoInvitado.SalaNoDisponible, respuesta.Resultado);
    }

    private sealed class SalasFalsas(SalaParaInvitado? sala, bool falla) : ISalaInvitadosClient
    {
        public Task<SalaParaInvitado?> ObtenerPorPinAsync(int pin, CancellationToken ct) =>
            falla ? throw new HttpRequestException("Sala caída") : Task.FromResult(sala);
    }
}
