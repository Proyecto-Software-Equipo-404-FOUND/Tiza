using System.ComponentModel.DataAnnotations;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PEI.Identidad.Application.Abstractions;
using PEI.Identidad.Application.UseCases.Usuarios;
using PEI.Identidad.Domain.Entities;
using PEI.Identidad.Domain.Security;
using Xunit;

namespace PEI.Identidad.UnitTests;

// S2-ID-10 (#91): listar y obtener usuarios de la institución por rol.
public class UsuariosConsultaTests
{
    private sealed class ConsultaFalsa : IUsuariosConsultaDataAccess
    {
        public string? RolRecibido { get; private set; }
        public List<UsuarioResumen> Usuarios { get; } = new();
        public Task<IReadOnlyList<UsuarioResumen>> ListarActivosAsync(string? rol, CancellationToken cancellationToken = default)
        {
            RolRecibido = rol;
            IReadOnlyList<UsuarioResumen> lista = Usuarios.Where(u => rol is null || u.Rol == rol).ToList();
            return Task.FromResult(lista);
        }
        public Task<UsuarioResumen?> ObtenerActivoAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult(Usuarios.FirstOrDefault(u => u.Id == id));
    }

    // ---------- Handlers (capa Application) ----------

    [Fact]
    public async Task Listar_ConRolValido_FiltraPorEseRol()
    {
        var data = new ConsultaFalsa();
        data.Usuarios.Add(new UsuarioResumen(Guid.NewGuid(), "Laura", "Gómez", "docente@a.test", IdentityRoles.Docente));
        data.Usuarios.Add(new UsuarioResumen(Guid.NewGuid(), "Ana", "Pérez", "alumno1@a.test", IdentityRoles.Alumno));

        var resultado = await new ListarUsuariosHandler(data).HandleAsync(IdentityRoles.Docente);

        Assert.Equal(IdentityRoles.Docente, data.RolRecibido);
        Assert.Equal("Laura", Assert.Single(resultado).Nombre);
    }

    [Fact]
    public async Task Listar_SinRol_DevuelveTodos()
    {
        var data = new ConsultaFalsa();
        data.Usuarios.Add(new UsuarioResumen(Guid.NewGuid(), "Laura", "Gómez", "docente@a.test", IdentityRoles.Docente));
        data.Usuarios.Add(new UsuarioResumen(Guid.NewGuid(), "Ana", "Pérez", "alumno1@a.test", IdentityRoles.Alumno));

        var resultado = await new ListarUsuariosHandler(data).HandleAsync(null);

        Assert.Equal(2, resultado.Count);
    }

    [Fact]
    public async Task Listar_ConRolInexistente_LanzaValidationException()
    {
        await Assert.ThrowsAsync<ValidationException>(() => new ListarUsuariosHandler(new ConsultaFalsa()).HandleAsync("Director"));
    }

    [Fact]
    public async Task Obtener_ConIdVacio_DevuelveNull()
    {
        Assert.Null(await new ObtenerUsuarioHandler(new ConsultaFalsa()).HandleAsync(Guid.Empty));
    }

    [Fact]
    public async Task Obtener_Inexistente_DevuelveNull()
    {
        Assert.Null(await new ObtenerUsuarioHandler(new ConsultaFalsa()).HandleAsync(Guid.NewGuid()));
    }

    // ---------- HTTP (capa Api): permisos y respuesta ----------

    // Arma un cliente HTTP con la consulta falsa. crearUsuario recibe el store del factory
    // para que el usuario quede en la misma institución que el administrador de prueba.
    private static (IdentityHttpFactory factory, HttpClient client) Cliente(
        ConsultaFalsa data, Func<FakeIdentityStore, Usuario?>? crearUsuario = null, bool conToken = true)
    {
        var factory = new IdentityHttpFactory();
        var usuario = crearUsuario?.Invoke(factory.Store);
        if (usuario is not null) factory.Store.Users[usuario.Id] = usuario;
        var app = factory.WithWebHostBuilder(b => b.ConfigureTestServices(s =>
        {
            s.RemoveAll<IUsuariosConsultaDataAccess>();
            s.AddSingleton<IUsuariosConsultaDataAccess>(data);
        }));
        var client = app.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        if (conToken) client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", factory.Token(usuario));
        return (factory, client);
    }

    [Fact]
    public async Task Http_SinToken_Devuelve401()
    {
        var (factory, client) = Cliente(new ConsultaFalsa(), conToken: false);
        using (factory) Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/usuarios")).StatusCode);
    }

    [Fact]
    public async Task Http_Alumno_Devuelve403()
    {
        var (factory, client) = Cliente(new ConsultaFalsa(), store => new Usuario
        {
            InstitucionId = store.Admin.InstitucionId, Email = "alumno@example.org", Rol = IdentityRoles.Alumno
        });
        using (factory) Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/usuarios")).StatusCode);
    }

    [Fact]
    public async Task Http_Admin_ListaSinExponerPasswordHash()
    {
        var data = new ConsultaFalsa();
        data.Usuarios.Add(new UsuarioResumen(Guid.NewGuid(), "Laura", "Gómez", "docente@a.test", IdentityRoles.Docente));
        var (factory, client) = Cliente(data);
        using (factory)
        {
            var respuesta = await client.GetAsync("/api/usuarios?rol=Docente");
            Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
            var texto = await respuesta.Content.ReadAsStringAsync();
            Assert.DoesNotContain("password", texto, StringComparison.OrdinalIgnoreCase);
            var json = JsonDocument.Parse(texto).RootElement;
            Assert.Equal("Laura", json[0].GetProperty("nombre").GetString());
        }
    }

    [Fact]
    public async Task Http_RolInvalido_Devuelve400()
    {
        var (factory, client) = Cliente(new ConsultaFalsa());
        using (factory) Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/api/usuarios?rol=Director")).StatusCode);
    }

    [Fact]
    public async Task Http_UsuarioInexistente_Devuelve404()
    {
        var (factory, client) = Cliente(new ConsultaFalsa());
        using (factory) Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/usuarios/{Guid.NewGuid()}")).StatusCode);
    }
}
