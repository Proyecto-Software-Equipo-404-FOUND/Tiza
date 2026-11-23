using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PEI.Identidad.Application.Abstractions;
using PEI.Identidad.Application.UseCases.Auth.Login;
using PEI.Identidad.Application.UseCases.Usuarios.Estado;
using PEI.Identidad.Domain.Entities;
using PEI.Identidad.Domain.Security;
using PEI.Identidad.Infrastructure.Security;
using Xunit;

namespace PEI.Identidad.UnitTests;

public class BajaReactivacionUsuariosTests
{
    private sealed class RepositorioModificacionFalso : IUsuariosModificacionDataAccess
    {
        public ConcurrentDictionary<Guid, Usuario> Usuarios { get; } = new();
        public List<Guid> TokensRevocadosPorUsuario { get; } = new();
        public bool Guardado { get; private set; }
        public bool SimularFalloEnBaja { get; set; }

        public Task<Usuario?> ObtenerUsuarioAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult(Usuarios.TryGetValue(id, out var u) ? u : null);

        public Task DarDeBajaAsync(Guid usuarioId, CancellationToken cancellationToken = default)
        {
            if (SimularFalloEnBaja)
                throw new InvalidOperationException("Fallo simulado durante la transaccion de baja.");

            if (Usuarios.TryGetValue(usuarioId, out var u) && u.Activo)
            {
                u.Activo = false;
                TokensRevocadosPorUsuario.Add(usuarioId);
                Guardado = true;
            }
            return Task.CompletedTask;
        }

        public Task SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            Guardado = true;
            return Task.CompletedTask;
        }
    }

    // ==========================================
    // 1. Tests Unitarios de Handlers (Application)
    // ==========================================

    [Theory]
    [InlineData(IdentityRoles.Alumno)]
    [InlineData(IdentityRoles.Docente)]
    public async Task DarBaja_UsuarioAsignableActivo_LoDesactivaRevocaTokensYDevuelveExitoso(string rol)
    {
        var repo = new RepositorioModificacionFalso();
        var usuario = new Usuario
        {
            Id = Guid.NewGuid(),
            Rol = rol,
            Activo = true
        };
        repo.Usuarios[usuario.Id] = usuario;

        var handler = new DarBajaUsuarioHandler(repo);
        var resultado = await handler.HandleAsync(new DarBajaUsuarioCommand(usuario.Id));

        Assert.Equal(ModificarEstadoUsuarioResultado.Exitoso, resultado);
        Assert.False(usuario.Activo);
        Assert.Contains(usuario.Id, repo.TokensRevocadosPorUsuario);
        Assert.True(repo.Guardado);
    }

    [Fact]
    public async Task DarBaja_UsuarioYaInactivo_EsIdempotente()
    {
        var repo = new RepositorioModificacionFalso();
        var usuario = new Usuario
        {
            Id = Guid.NewGuid(),
            Rol = IdentityRoles.Alumno,
            Activo = false
        };
        repo.Usuarios[usuario.Id] = usuario;

        var handler = new DarBajaUsuarioHandler(repo);
        var resultado = await handler.HandleAsync(new DarBajaUsuarioCommand(usuario.Id));

        Assert.Equal(ModificarEstadoUsuarioResultado.Exitoso, resultado);
        Assert.False(usuario.Activo);
        Assert.Empty(repo.TokensRevocadosPorUsuario);
        Assert.False(repo.Guardado);
    }

    [Fact]
    public async Task DarBaja_UsuarioAdministrador_DevuelveRolNoPermitido()
    {
        var repo = new RepositorioModificacionFalso();
        var admin = new Usuario
        {
            Id = Guid.NewGuid(),
            Rol = IdentityRoles.Administrador,
            Activo = true
        };
        repo.Usuarios[admin.Id] = admin;

        var handler = new DarBajaUsuarioHandler(repo);
        var resultado = await handler.HandleAsync(new DarBajaUsuarioCommand(admin.Id));

        Assert.Equal(ModificarEstadoUsuarioResultado.RolNoPermitido, resultado);
        Assert.True(admin.Activo);
        Assert.Empty(repo.TokensRevocadosPorUsuario);
    }

    [Fact]
    public async Task DarBaja_UsuarioInexistente_DevuelveNoEncontrado()
    {
        var repo = new RepositorioModificacionFalso();
        var handler = new DarBajaUsuarioHandler(repo);
        var resultado = await handler.HandleAsync(new DarBajaUsuarioCommand(Guid.NewGuid()));

        Assert.Equal(ModificarEstadoUsuarioResultado.NoEncontrado, resultado);
    }

    [Fact]
    public async Task DarBaja_FalloEnPersistencia_PropagaExcepcionSinOcultarla()
    {
        // Prueba unitaria de propagación de excepciones del DataAccess en el flujo de baja
        var repo = new RepositorioModificacionFalso { SimularFalloEnBaja = true };
        var usuario = new Usuario
        {
            Id = Guid.NewGuid(),
            Rol = IdentityRoles.Alumno,
            Activo = true
        };
        repo.Usuarios[usuario.Id] = usuario;

        var handler = new DarBajaUsuarioHandler(repo);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            handler.HandleAsync(new DarBajaUsuarioCommand(usuario.Id)));
    }

    [Theory]
    [InlineData(IdentityRoles.Alumno)]
    [InlineData(IdentityRoles.Docente)]
    public async Task Reactivar_UsuarioAsignableInactivo_LoActivaYDevuelveExitoso(string rol)
    {
        var repo = new RepositorioModificacionFalso();
        var usuario = new Usuario
        {
            Id = Guid.NewGuid(),
            Rol = rol,
            Activo = false
        };
        repo.Usuarios[usuario.Id] = usuario;

        var handler = new ReactivarUsuarioHandler(repo);
        var resultado = await handler.HandleAsync(new ReactivarUsuarioCommand(usuario.Id));

        Assert.Equal(ModificarEstadoUsuarioResultado.Exitoso, resultado);
        Assert.True(usuario.Activo);
        Assert.True(repo.Guardado);
    }

    [Fact]
    public async Task Reactivar_UsuarioYaActivo_EsIdempotente()
    {
        var repo = new RepositorioModificacionFalso();
        var usuario = new Usuario
        {
            Id = Guid.NewGuid(),
            Rol = IdentityRoles.Docente,
            Activo = true
        };
        repo.Usuarios[usuario.Id] = usuario;

        var handler = new ReactivarUsuarioHandler(repo);
        var resultado = await handler.HandleAsync(new ReactivarUsuarioCommand(usuario.Id));

        Assert.Equal(ModificarEstadoUsuarioResultado.Exitoso, resultado);
        Assert.True(usuario.Activo);
        Assert.False(repo.Guardado);
    }

    [Fact]
    public async Task Reactivar_UsuarioAdministrador_DevuelveRolNoPermitido()
    {
        var repo = new RepositorioModificacionFalso();
        var admin = new Usuario
        {
            Id = Guid.NewGuid(),
            Rol = IdentityRoles.Administrador,
            Activo = false
        };
        repo.Usuarios[admin.Id] = admin;

        var handler = new ReactivarUsuarioHandler(repo);
        var resultado = await handler.HandleAsync(new ReactivarUsuarioCommand(admin.Id));

        Assert.Equal(ModificarEstadoUsuarioResultado.RolNoPermitido, resultado);
        Assert.False(admin.Activo);
    }

    [Fact]
    public async Task Reactivar_UsuarioInexistente_DevuelveNoEncontrado()
    {
        var repo = new RepositorioModificacionFalso();
        var handler = new ReactivarUsuarioHandler(repo);
        var resultado = await handler.HandleAsync(new ReactivarUsuarioCommand(Guid.NewGuid()));

        Assert.Equal(ModificarEstadoUsuarioResultado.NoEncontrado, resultado);
    }

    // ==========================================
    // 2. Tests HTTP (Api): Permisos y Aislamiento
    // ==========================================

    private static (IdentityHttpFactory factory, HttpClient client, FakeIdentityStore store) SetupCliente(
        Usuario? usuarioAutenticado = null,
        bool conToken = true,
        Action<FakeIdentityStore>? configurarStore = null)
    {
        var factory = new IdentityHttpFactory();
        configurarStore?.Invoke(factory.Store);

        var usuarioToken = usuarioAutenticado ?? factory.Store.Admin;
        if (usuarioAutenticado is not null)
            factory.Store.Users[usuarioToken.Id] = usuarioToken;

        var repoFalso = new RepositorioModificacionConStore(factory.Store);

        var app = factory.WithWebHostBuilder(b => b.ConfigureTestServices(s =>
        {
            s.RemoveAll<IUsuariosModificacionDataAccess>();
            s.AddSingleton<IUsuariosModificacionDataAccess>(repoFalso);
        }));

        var client = app.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false
        });

        if (conToken)
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", factory.Token(usuarioToken));

        return (factory, client, factory.Store);
    }

    private sealed class RepositorioModificacionConStore(FakeIdentityStore store) : IUsuariosModificacionDataAccess
    {
        public Task<Usuario?> ObtenerUsuarioAsync(Guid id, CancellationToken cancellationToken = default)
        {
            // Simula el query filter de tenant: solo devuelve si pertenece a la institución del usuario autenticado
            if (store.Users.TryGetValue(id, out var u) && u.InstitucionId == store.Admin.InstitucionId)
                return Task.FromResult<Usuario?>(u);

            return Task.FromResult<Usuario?>(null);
        }

        public Task DarDeBajaAsync(Guid usuarioId, CancellationToken cancellationToken = default)
        {
            if (store.Users.TryGetValue(usuarioId, out var u) && u.InstitucionId == store.Admin.InstitucionId)
            {
                u.Activo = false;
            }
            return Task.CompletedTask;
        }

        public Task SaveChangesAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    [Fact]
    public async Task Http_SinToken_Devuelve401()
    {
        var (factory, client, _) = SetupCliente(conToken: false);
        using (factory)
        {
            var resBaja = await client.PostAsync($"/api/usuarios/{Guid.NewGuid()}/baja", null);
            var resReactivar = await client.PostAsync($"/api/usuarios/{Guid.NewGuid()}/reactivar", null);

            Assert.Equal(HttpStatusCode.Unauthorized, resBaja.StatusCode);
            Assert.Equal(HttpStatusCode.Unauthorized, resReactivar.StatusCode);
        }
    }

    [Fact]
    public async Task Http_Docente_Devuelve403()
    {
        var (factory, client, store) = SetupCliente();
        var docente = new Usuario
        {
            InstitucionId = store.Admin.InstitucionId,
            Email = "docente@institucion.test",
            Rol = IdentityRoles.Docente
        };
        store.Users[docente.Id] = docente;

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", factory.Token(docente));

        using (factory)
        {
            var resBaja = await client.PostAsync($"/api/usuarios/{Guid.NewGuid()}/baja", null);
            var resReactivar = await client.PostAsync($"/api/usuarios/{Guid.NewGuid()}/reactivar", null);

            Assert.Equal(HttpStatusCode.Forbidden, resBaja.StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, resReactivar.StatusCode);
        }
    }

    [Fact]
    public async Task Http_Alumno_Devuelve403()
    {
        var (factory, client, store) = SetupCliente();
        var alumno = new Usuario
        {
            InstitucionId = store.Admin.InstitucionId,
            Email = "alumno@institucion.test",
            Rol = IdentityRoles.Alumno
        };
        store.Users[alumno.Id] = alumno;

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", factory.Token(alumno));

        using (factory)
        {
            var resBaja = await client.PostAsync($"/api/usuarios/{Guid.NewGuid()}/baja", null);
            var resReactivar = await client.PostAsync($"/api/usuarios/{Guid.NewGuid()}/reactivar", null);

            Assert.Equal(HttpStatusCode.Forbidden, resBaja.StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, resReactivar.StatusCode);
        }
    }

    [Fact]
    public async Task Http_Admin_DarBajaARolAdministrador_Devuelve403()
    {
        var (factory, client, store) = SetupCliente();
        var otroAdmin = new Usuario
        {
            InstitucionId = store.Admin.InstitucionId,
            Email = "otro.admin@institucion.test",
            Rol = IdentityRoles.Administrador,
            Activo = true
        };
        store.Users[otroAdmin.Id] = otroAdmin;

        using (factory)
        {
            var resBaja = await client.PostAsync($"/api/usuarios/{otroAdmin.Id}/baja", null);
            Assert.Equal(HttpStatusCode.Forbidden, resBaja.StatusCode);
            Assert.True(otroAdmin.Activo);
        }
    }

    [Fact]
    public async Task Http_Admin_UsuarioDeOtraInstitucion_Devuelve404()
    {
        var (factory, client, store) = SetupCliente();
        var usuarioOtraInstitucion = new Usuario
        {
            InstitucionId = Guid.NewGuid(), // Otra institución distinta
            Email = "alumno.otra@test.invalid",
            Rol = IdentityRoles.Alumno,
            Activo = true
        };
        store.Users[usuarioOtraInstitucion.Id] = usuarioOtraInstitucion;

        using (factory)
        {
            var resBaja = await client.PostAsync($"/api/usuarios/{usuarioOtraInstitucion.Id}/baja", null);
            var resReactivar = await client.PostAsync($"/api/usuarios/{usuarioOtraInstitucion.Id}/reactivar", null);

            Assert.Equal(HttpStatusCode.NotFound, resBaja.StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, resReactivar.StatusCode);
        }
    }

    [Fact]
    public async Task Http_Admin_UsuarioInexistente_Devuelve404()
    {
        var (factory, client, _) = SetupCliente();
        using (factory)
        {
            var idInexistente = Guid.NewGuid();
            var resBaja = await client.PostAsync($"/api/usuarios/{idInexistente}/baja", null);
            var resReactivar = await client.PostAsync($"/api/usuarios/{idInexistente}/reactivar", null);

            Assert.Equal(HttpStatusCode.NotFound, resBaja.StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, resReactivar.StatusCode);
        }
    }

    [Theory]
    [InlineData(IdentityRoles.Alumno)]
    [InlineData(IdentityRoles.Docente)]
    public async Task Http_Admin_BajaYReactivacion_FlujoCompletoEIdempotente(string rol)
    {
        var (factory, client, store) = SetupCliente();
        var usuario = new Usuario
        {
            InstitucionId = store.Admin.InstitucionId,
            Email = $"{rol.ToLowerInvariant()}@institucion.test",
            Rol = rol,
            Activo = true
        };
        store.Users[usuario.Id] = usuario;

        using (factory)
        {
            // 1. Dar de baja inicial -> 204
            var resBaja = await client.PostAsync($"/api/usuarios/{usuario.Id}/baja", null);
            Assert.Equal(HttpStatusCode.NoContent, resBaja.StatusCode);
            Assert.False(usuario.Activo);

            // 2. Repetir baja (idempotencia) -> 204
            var resBaja2 = await client.PostAsync($"/api/usuarios/{usuario.Id}/baja", null);
            Assert.Equal(HttpStatusCode.NoContent, resBaja2.StatusCode);
            Assert.False(usuario.Activo);

            // 3. Reactivar -> 204
            var resReactivar = await client.PostAsync($"/api/usuarios/{usuario.Id}/reactivar", null);
            Assert.Equal(HttpStatusCode.NoContent, resReactivar.StatusCode);
            Assert.True(usuario.Activo);

            // 4. Repetir reactivación (idempotencia) -> 204
            var resReactivar2 = await client.PostAsync($"/api/usuarios/{usuario.Id}/reactivar", null);
            Assert.Equal(HttpStatusCode.NoContent, resReactivar2.StatusCode);
            Assert.True(usuario.Activo);
        }
    }

    // =========================================================================
    // 3. Reglas de Negocio: Bloqueo de Login y Rechazo de JWT para Usuario Inactivo
    // =========================================================================

    [Fact]
    public async Task UsuarioDadoDeBaja_NoPuedeIniciarSesion_YReactivadoSiPuede()
    {
        var (factory, client, store) = SetupCliente();
        var passwordPlano = "Password123!";
        var passwordHash = new BCryptPasswordHasher().Hash(passwordPlano);

        var usuario = new Usuario
        {
            InstitucionId = store.Admin.InstitucionId,
            Email = "alumno.login@test.com",
            PasswordHash = passwordHash,
            Rol = IdentityRoles.Alumno,
            Activo = true
        };
        store.Users[usuario.Id] = usuario;

        using (factory)
        {
            // 1. Login exitoso mientras está activo
            var loginCmd = new LoginCommand { Email = usuario.Email, Password = passwordPlano };
            var resLogin1 = await client.PostAsJsonAsync("/api/auth/login", loginCmd);
            Assert.Equal(HttpStatusCode.OK, resLogin1.StatusCode);

            // 2. Admin da de baja al usuario -> 204
            var resBaja = await client.PostAsync($"/api/usuarios/{usuario.Id}/baja", null);
            Assert.Equal(HttpStatusCode.NoContent, resBaja.StatusCode);
            Assert.False(usuario.Activo);

            // 3. Login falla con 401 mientras está dado de baja
            var resLogin2 = await client.PostAsJsonAsync("/api/auth/login", loginCmd);
            Assert.Equal(HttpStatusCode.Unauthorized, resLogin2.StatusCode);

            // 4. Admin reactiva al usuario -> 204
            var resReactivar = await client.PostAsync($"/api/usuarios/{usuario.Id}/reactivar", null);
            Assert.Equal(HttpStatusCode.NoContent, resReactivar.StatusCode);
            Assert.True(usuario.Activo);

            // 5. Login vuelve a ser exitoso
            var resLogin3 = await client.PostAsJsonAsync("/api/auth/login", loginCmd);
            Assert.Equal(HttpStatusCode.OK, resLogin3.StatusCode);
        }
    }

    [Fact]
    public async Task UsuarioDadoDeBaja_NoPuedeUsarSuJwtVigente()
    {
        var (factory, client, store) = SetupCliente();
        var docente = new Usuario
        {
            InstitucionId = store.Admin.InstitucionId,
            Email = "docente.jwt@test.com",
            Rol = IdentityRoles.Docente,
            Activo = true
        };
        store.Users[docente.Id] = docente;

        // Emitimos token válido para el docente
        var docenteToken = factory.Token(docente);

        using (factory)
        {
            using var docenteClient = factory.CreateClient();
            docenteClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", docenteToken);

            // 1. Llamada autorizada previa a la baja -> 200
            var resPreBaja = await docenteClient.GetAsync("/api/auth/docente");
            Assert.Equal(HttpStatusCode.OK, resPreBaja.StatusCode);

            // 2. Admin da de baja al docente
            var resBaja = await client.PostAsync($"/api/usuarios/{docente.Id}/baja", null);
            Assert.Equal(HttpStatusCode.NoContent, resBaja.StatusCode);
            Assert.False(docente.Activo);

            // 3. Misma llamada con el mismo JWT aún vigente -> rechazada con 401 por IAuthenticatedUserValidator
            var resPostBaja = await docenteClient.GetAsync("/api/auth/docente");
            Assert.Equal(HttpStatusCode.Unauthorized, resPostBaja.StatusCode);
        }
    }
}
