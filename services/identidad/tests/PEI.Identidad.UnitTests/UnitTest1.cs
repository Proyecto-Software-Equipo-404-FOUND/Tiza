
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using PEI.Identidad.Application.Interfaces.Repositories;
using PEI.Identidad.Application.Interfaces.Security;
using PEI.Identidad.Application.Interfaces.Service;
using PEI.Identidad.Application.UseCases.Auth.Login;
using PEI.Identidad.Application.UseCases.Auth.Register;
using PEI.Identidad.Domain.Entities;
using PEI.Identidad.Application.UseCases.Auth.Refresh;

namespace PEI.Identidad.UnitTests;

public class AuthHandlerTests
{
    private static readonly Guid InstitucionId =
        Guid.Parse("11111111-1111-1111-1111-111111111111");

    private static RegisterCommand CrearRegistro() => new()
    {
        InstitucionId = InstitucionId,
        Email = " Prueba@Example.com ",
        Password = "Prueba123!",
        Nombre = " Usuario ",
        Apellido = " Prueba ",
        Rol = "Alumno"
    };

    private static Usuario CrearUsuario(
        bool usuarioActivo = true,
        bool institucionActiva = true) => new()
    {
        Id = Guid.NewGuid(),
        InstitucionId = InstitucionId,
        Email = "prueba@example.com",
        PasswordHash = "HASH:Prueba123!",
        Nombre = "Usuario",
        Apellido = "Prueba",
        Rol = "Alumno",
        Activo = usuarioActivo,
        Institucion = new Institucion
        {
            Id = InstitucionId,
            Nombre = "Institucion de prueba",
            Codigo = "INST-001",
            Activo = institucionActiva
        }
    };

    [Fact]
    public async Task RegistroValido_GuardaUsuarioYDevuelveId()
    {
        var repositorio = new RepositorioFalso(InstitucionId);
        var handler = new RegisterHandler(
            repositorio, new HasherFalso());

        var id = await handler.HandleAsync(CrearRegistro());

        Assert.NotEqual(Guid.Empty, id);
        Assert.Single(repositorio.Usuarios);
        Assert.Equal(id, repositorio.Usuarios[0].Id);
        Assert.Equal("prueba@example.com", repositorio.Usuarios[0].Email);
        Assert.Equal("Alumno", repositorio.Usuarios[0].Rol);
        Assert.Equal("Prueba", repositorio.Usuarios[0].Apellido);
        Assert.Equal("Alumno", repositorio.Usuarios[0].Rol);
        Assert.True(repositorio.Guardado);
    }

    [Fact]
    public async Task RegistroConEmailDuplicado_LanzaExcepcion()
    {
        var repositorio = new RepositorioFalso(InstitucionId);
        repositorio.AgregarExistente(CrearUsuario());

        var handler = new RegisterHandler(
            repositorio, new HasherFalso());

        var error = await Assert.ThrowsAsync<InvalidOperationException>(
            () => handler.HandleAsync(CrearRegistro()));

        Assert.Equal(
            "Ya existe un usuario con ese email.",
            error.Message);
    }

    [Fact]
    public async Task RegistroConInstitucionInactiva_LanzaExcepcion()
    {
        var repositorio = new RepositorioFalso(
            InstitucionId, institucionActiva: false);

        var handler = new RegisterHandler(
            repositorio, new HasherFalso());

        var error = await Assert.ThrowsAsync<InvalidOperationException>(
            () => handler.HandleAsync(CrearRegistro()));

        Assert.Equal(
            "La institución no existe o está inactiva.",
            error.Message);

        Assert.Empty(repositorio.Usuarios);
    }

    [Fact]
    public async Task LoginValido_DevuelveTokensYGuardaRefreshToken()
    {
        var repositorio = new RepositorioFalso(InstitucionId);
        repositorio.AgregarExistente(CrearUsuario());

        var repositorioRefresh = new RefreshTokenRepositorioFalso();

        var handler = new LoginHandler(
            repositorio,
            new HasherFalso(),
            new TokenFalso(),
            repositorioRefresh,
            new RefreshTokenServicioFalso(),
            new RefreshTokenHasherFalso());

        var resultado = await handler.HandleAsync(new LoginCommand
        {
            Email = " Prueba@Example.com ",
            Password = "Prueba123!"
        });

        Assert.Equal("token-de-prueba", resultado.AccessToken);
        Assert.Equal("refresh-token-de-prueba", resultado.RefreshToken);
        Assert.True(resultado.AccessTokenExpiresAt > DateTime.UtcNow);
        Assert.True(resultado.RefreshTokenExpiresAt > resultado.AccessTokenExpiresAt);

        Assert.Single(repositorioRefresh.Tokens);
        Assert.Equal(
            "HASH:refresh-token-de-prueba",
            repositorioRefresh.Tokens[0].Token);
        Assert.Equal(
            repositorio.Usuarios[0].Id,
            repositorioRefresh.Tokens[0].UsuarioId);
        Assert.False(repositorioRefresh.Tokens[0].Revocado);
        Assert.True(repositorioRefresh.Guardado);
    }

    [Fact]
    public async Task LoginConPasswordIncorrecta_LanzaExcepcion()
    {
        var repositorio = new RepositorioFalso(InstitucionId);
        repositorio.AgregarExistente(CrearUsuario());

        var handler = new LoginHandler(
            repositorio,
            new HasherFalso(),
            new TokenFalso(),
            new RefreshTokenRepositorioFalso(),
            new RefreshTokenServicioFalso(),
            new RefreshTokenHasherFalso());

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => handler.HandleAsync(new LoginCommand
            {
                Email = "prueba@example.com",
                Password = "Incorrecta123!"
            }));
    }

    [Fact]
    public async Task LoginConUsuarioInexistente_LanzaExcepcion()
    {
        var repositorio = new RepositorioFalso(InstitucionId);

        var handler = new LoginHandler(
            repositorio,
            new HasherFalso(),
            new TokenFalso(),
            new RefreshTokenRepositorioFalso(),
            new RefreshTokenServicioFalso(),
            new RefreshTokenHasherFalso());

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => handler.HandleAsync(new LoginCommand
            {
                Email = "inexistente@example.com",
                Password = "Prueba123!"
            }));
    }

    [Fact]
    public async Task LoginConUsuarioInactivo_LanzaExcepcion()
    {
        var repositorio = new RepositorioFalso(InstitucionId);
        repositorio.AgregarExistente(
            CrearUsuario(usuarioActivo: false));

        var handler = new LoginHandler(
            repositorio,
            new HasherFalso(),
            new TokenFalso(),
            new RefreshTokenRepositorioFalso(),
            new RefreshTokenServicioFalso(),
            new RefreshTokenHasherFalso());
        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => handler.HandleAsync(new LoginCommand
            {
                Email = "prueba@example.com",
                Password = "Prueba123!"
            }));
    }

    [Fact]
    public async Task LoginConInstitucionInactiva_LanzaExcepcion()
    {
        var repositorio = new RepositorioFalso(InstitucionId);
        repositorio.AgregarExistente(
            CrearUsuario(institucionActiva: false));

        var handler = new LoginHandler(
            repositorio,
            new HasherFalso(),
            new TokenFalso(),
            new RefreshTokenRepositorioFalso(),
            new RefreshTokenServicioFalso(),
            new RefreshTokenHasherFalso());

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => handler.HandleAsync(new LoginCommand
            {
                Email = "prueba@example.com",
                Password = "Prueba123!"
            }));
    }

    [Fact]
    public async Task RefreshValido_RotaTokensYRevocaElAnterior()
    {
        var usuario = CrearUsuario();
        var repositorio = new RefreshTokenRepositorioFalso();

        var tokenAnterior = new RefreshToken
        {
            UsuarioId = usuario.Id,
            Usuario = usuario,
            Token = "HASH:refresh-original",
            FechaExpiracion = DateTime.UtcNow.AddDays(7),
            Revocado = false
        };

        repositorio.AgregarExistente(tokenAnterior);

        var handler = new RefreshHandler(
            repositorio,
            new RefreshTokenServicioFalso(),
            new RefreshTokenHasherFalso(),
            new TokenFalso());

        var resultado = await handler.HandleAsync(new RefreshCommand
        {
            RefreshToken = "refresh-original"
        });

        Assert.Equal("token-de-prueba", resultado.AccessToken);
        Assert.Equal("refresh-token-de-prueba", resultado.RefreshToken);
        Assert.True(tokenAnterior.Revocado);
        Assert.Equal(2, repositorio.Tokens.Count);
        Assert.Equal(
            "HASH:refresh-token-de-prueba",
            repositorio.Tokens[1].Token);
        Assert.False(repositorio.Tokens[1].Revocado);
        Assert.True(repositorio.Guardado);
    }

    [Fact]
    public async Task RefreshConTokenInexistente_LanzaExcepcion()
    {
        var repositorio = new RefreshTokenRepositorioFalso();

        var handler = new RefreshHandler(
            repositorio,
            new RefreshTokenServicioFalso(),
            new RefreshTokenHasherFalso(),
            new TokenFalso());

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => handler.HandleAsync(new RefreshCommand
            {
                RefreshToken = "token-inexistente"
            }));

        Assert.Empty(repositorio.Tokens);
        Assert.False(repositorio.Guardado);
    }

    [Fact]
    public async Task RefreshConTokenVencido_LanzaExcepcion()
    {
        var usuario = CrearUsuario();
        var repositorio = new RefreshTokenRepositorioFalso();

        repositorio.AgregarExistente(new RefreshToken
        {
            UsuarioId = usuario.Id,
            Usuario = usuario,
            Token = "HASH:refresh-vencido",
            FechaExpiracion = DateTime.UtcNow.AddDays(-1),
            Revocado = false
        });

        var handler = new RefreshHandler(
            repositorio,
            new RefreshTokenServicioFalso(),
            new RefreshTokenHasherFalso(),
            new TokenFalso());

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => handler.HandleAsync(new RefreshCommand
            {
                RefreshToken = "refresh-vencido"
            }));

        Assert.False(repositorio.Guardado);
    }

    [Fact]
    public async Task RefreshConTokenRevocado_LanzaExcepcion()
    {
        var usuario = CrearUsuario();
        var repositorio = new RefreshTokenRepositorioFalso();

        repositorio.AgregarExistente(new RefreshToken
        {
            UsuarioId = usuario.Id,
            Usuario = usuario,
            Token = "HASH:refresh-revocado",
            FechaExpiracion = DateTime.UtcNow.AddDays(7),
            Revocado = true
        });

        var handler = new RefreshHandler(
            repositorio,
            new RefreshTokenServicioFalso(),
            new RefreshTokenHasherFalso(),
            new TokenFalso());

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => handler.HandleAsync(new RefreshCommand
            {
                RefreshToken = "refresh-revocado"
            }));

        Assert.False(repositorio.Guardado);
    }

    [Fact]
    public async Task RefreshConUsuarioInactivo_LanzaExcepcion()
    {
        var usuario = CrearUsuario(usuarioActivo: false);
        var repositorio = new RefreshTokenRepositorioFalso();

        repositorio.AgregarExistente(new RefreshToken
        {
            UsuarioId = usuario.Id,
            Usuario = usuario,
            Token = "HASH:refresh-usuario-inactivo",
            FechaExpiracion = DateTime.UtcNow.AddDays(7),
            Revocado = false
        });

        var handler = new RefreshHandler(
            repositorio,
            new RefreshTokenServicioFalso(),
            new RefreshTokenHasherFalso(),
            new TokenFalso());

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => handler.HandleAsync(new RefreshCommand
            {
                RefreshToken = "refresh-usuario-inactivo"
            }));

        Assert.False(repositorio.Guardado);
    }

    [Fact]
    public async Task LogoutValido_RevocaTokenYGuardaCambios()
    {
        var usuario = CrearUsuario();
        var repositorio = new RefreshTokenRepositorioFalso();

        var token = new RefreshToken
        {
            UsuarioId = usuario.Id,
            Usuario = usuario,
            Token = "HASH:refresh-logout",
            FechaExpiracion = DateTime.UtcNow.AddDays(7),
            Revocado = false
        };

        repositorio.AgregarExistente(token);

        var handler = new LogoutHandler(
            repositorio,
            new RefreshTokenHasherFalso());

        await handler.HandleAsync(new LogoutCommand
        {
            RefreshToken = "refresh-logout"
        });

        Assert.True(token.Revocado);
        Assert.True(repositorio.Guardado);
    }

    [Fact]
    public async Task LogoutConTokenInexistente_NoFalla()
    {
        var repositorio = new RefreshTokenRepositorioFalso();

        var handler = new LogoutHandler(
            repositorio,
            new RefreshTokenHasherFalso());

        await handler.HandleAsync(new LogoutCommand
        {
            RefreshToken = "token-inexistente"
        });

        Assert.False(repositorio.Guardado);
    }

    private sealed class RepositorioFalso : IUsuarioRepository
    {
        private readonly List<Usuario> _usuarios = new();
        private readonly Guid _institucionId;
        private readonly bool _institucionActiva;

        public RepositorioFalso(
            Guid institucionId,
            bool institucionActiva = true)
        {
            _institucionId = institucionId;
            _institucionActiva = institucionActiva;
        }

        public IReadOnlyList<Usuario> Usuarios => _usuarios;
        public bool Guardado { get; private set; }

        public void AgregarExistente(Usuario usuario)
        {
            _usuarios.Add(usuario);
        }

        public Task<Usuario?> GetByEmailAsync(string email)
        {
            Usuario? usuario = _usuarios.FirstOrDefault(
                u => u.Email == email);

            return Task.FromResult(usuario);
        }

        public Task<bool> InstitucionActivaExisteAsync(
            Guid institucionId)
        {
            return Task.FromResult(
                institucionId == _institucionId &&
                _institucionActiva);
        }

        public Task AddAsync(Usuario usuario)
        {
            _usuarios.Add(usuario);
            return Task.CompletedTask;
        }

        public Task SaveChangesAsync()
        {
            Guardado = true;
            return Task.CompletedTask;
        }
    }

    private sealed class HasherFalso : IPasswordHasher
    {
        public string Hash(string password)
        {
            return "HASH:" + password;
        }

        public bool Verify(string password, string hashedPassword)
        {
            return Hash(password) == hashedPassword;
        }
    }

    private sealed class TokenFalso : ITokenService
    {
        public string GenerateToken(Usuario usuario)
        {
            return "token-de-prueba";
        }
    }

    private sealed class RefreshTokenRepositorioFalso
    : IRefreshTokenRepository
{
    private readonly List<RefreshToken> _tokens = new();

    public IReadOnlyList<RefreshToken> Tokens => _tokens;
    public bool Guardado { get; private set; }

    public void AgregarExistente(RefreshToken token)
    {
        _tokens.Add(token);
    }

    public Task<RefreshToken?> GetByTokenAsync(string token)
    {
        var refreshToken = _tokens.FirstOrDefault(t => t.Token == token);
        return Task.FromResult(refreshToken);
    }

    public Task AddAsync(RefreshToken refreshToken)
    {
        _tokens.Add(refreshToken);
        return Task.CompletedTask;
    }

    public Task SaveChangesAsync()
    {
        Guardado = true;
        return Task.CompletedTask;
    }
}

    private sealed class RefreshTokenServicioFalso : IRefreshTokenService
    {
        public string GenerateToken()
        {
            return "refresh-token-de-prueba";
        }
    }

    private sealed class RefreshTokenHasherFalso : IRefreshTokenHasher
    {
        public string Hash(string token)
        {
            return "HASH:" + token;
        }
    }
}
