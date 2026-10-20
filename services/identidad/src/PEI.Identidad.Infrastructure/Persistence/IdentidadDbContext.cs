using PEI.Identidad.Application.Exceptions;
using Microsoft.EntityFrameworkCore;
using PEI.Identidad.Application.Abstractions;
using PEI.Identidad.Domain.Entities;

namespace PEI.Identidad.Infrastructure.Persistence;

public class IdentidadDbContext : DbContext
{
    private readonly ITenantContext _tenantContext;

    public IdentidadDbContext(
        DbContextOptions<IdentidadDbContext> options,
        ITenantContext tenantContext) : base(options)
    {
        _tenantContext = tenantContext;
    }

    public Guid? InstitucionActualId =>
        _tenantContext.InstitucionId;

    public DbSet<Institucion> Instituciones => Set<Institucion>();
    public DbSet<Usuario> Usuarios => Set<Usuario>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(
            typeof(IdentidadDbContext).Assembly);

        modelBuilder.HasDbFunction(typeof(EmailDbFunctions).GetMethod(nameof(EmailDbFunctions.Normalize))!)
            .HasName("pei_normalize_email");

        // xmin es una columna de sistema PostgreSQL: no agrega columnas de negocio.
        modelBuilder.Entity<Institucion>().Property<uint>("xmin").IsRowVersion();
        modelBuilder.Entity<Usuario>().Property<uint>("xmin").IsRowVersion();
        modelBuilder.Entity<RefreshToken>().Property<uint>("xmin").IsRowVersion();


        // Solo permite consultar la institucion autenticada.
        modelBuilder.Entity<Institucion>()
            .HasQueryFilter(institucion =>
                InstitucionActualId != null &&
                institucion.Id == InstitucionActualId);

        // Solo usuarios de la institucion autenticada.
        modelBuilder.Entity<Usuario>()
            .HasQueryFilter(usuario =>
                InstitucionActualId != null &&
                usuario.InstitucionId == InstitucionActualId);

        // Solo tokens de usuarios de la institucion autenticada.
        modelBuilder.Entity<RefreshToken>()
            .HasQueryFilter(token =>
                InstitucionActualId != null &&
                token.Usuario.InstitucionId == InstitucionActualId);
    }

    private async Task ValidarEscriturasUsuariosAsync(
        CancellationToken cancellationToken = default)
    {
        var institucionActualId = _tenantContext.InstitucionId;

        var entradas = ChangeTracker.Entries<Usuario>()
            .Where(e => e.State is EntityState.Added
                or EntityState.Modified
                or EntityState.Deleted)
            .ToList();

        foreach (var entrada in entradas)
        {
            if (!institucionActualId.HasValue ||
                entrada.Entity.InstitucionId != institucionActualId.Value)
            {
                throw new TenantAccessDeniedException();
            }

            if (entrada.State is EntityState.Modified
                or EntityState.Deleted)
            {
                var valoresOriginales = await entrada
                    .GetDatabaseValuesAsync(cancellationToken);

                var institucionOriginal = valoresOriginales?
                    .GetValue<Guid>(nameof(Usuario.InstitucionId));

                if (institucionOriginal != institucionActualId.Value)
                {
                    throw new TenantAccessDeniedException();
                }
            }
        }
    }


    private async Task ValidarEscriturasInstitucionesAsync(
        CancellationToken cancellationToken = default)
    {
        var institucionActualId = _tenantContext.InstitucionId;

        var entradas = ChangeTracker.Entries<Institucion>()
            .Where(entrada => entrada.State is EntityState.Added
                or EntityState.Modified
                or EntityState.Deleted)
            .ToList();

        foreach (var entrada in entradas)
        {
            // La creacion de instituciones requiere un flujo
            // administrativo separado.
            if (entrada.State == EntityState.Added)
                throw new TenantAccessDeniedException();

            if (!institucionActualId.HasValue ||
                entrada.Entity.Id != institucionActualId.Value)
            {
                throw new TenantAccessDeniedException();
            }

            // Comprobar que el registro existe en PostgreSQL.
            var valoresBaseDatos = await entrada
                .GetDatabaseValuesAsync(cancellationToken);

            if (valoresBaseDatos is null ||
                valoresBaseDatos.GetValue<Guid>(
                    nameof(Institucion.Id)) != institucionActualId.Value)
            {
                throw new TenantAccessDeniedException();
            }
        }
    }

    public override int SaveChanges()
    {
        throw new NotSupportedException(
            "Las escrituras deben realizarse mediante SaveChangesAsync.");
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        throw new NotSupportedException(
            "Las escrituras deben realizarse mediante SaveChangesAsync.");
    }



    public override Task<int> SaveChangesAsync(
        CancellationToken cancellationToken = default)
    {
        return SaveChangesAsync(
            acceptAllChangesOnSuccess: true,
            cancellationToken);
    }

    public override async Task<int> SaveChangesAsync(
        bool acceptAllChangesOnSuccess,
        CancellationToken cancellationToken = default)
    {
        ChangeTracker.DetectChanges();
        // Validar creacion, modificacion y eliminacion de instituciones.
        await ValidarEscriturasInstitucionesAsync(cancellationToken);

        // Validar creacion, modificacion y eliminacion de usuarios.
        await ValidarEscriturasUsuariosAsync(cancellationToken);

        // Validar creacion de refresh tokens.
        await ValidarCreacionRefreshTokensAsync(cancellationToken);

        // Validar modificacion y eliminacion de refresh tokens.
        await ValidarModificacionRefreshTokensAsync(cancellationToken);

        // Guardar solamente si todas las validaciones fueron exitosas.
        return await base.SaveChangesAsync(
            acceptAllChangesOnSuccess,
            cancellationToken);
    }

    private async Task<Guid?> ObtenerInstitucionDeUsuarioAsync(
        Guid usuarioId,
        CancellationToken cancellationToken = default)
    {
        var nuevo = ChangeTracker.Entries<Usuario>()
            .FirstOrDefault(e => e.State == EntityState.Added && e.Entity.Id == usuarioId);
        if (nuevo is not null)
            return nuevo.Entity.InstitucionId;

        return await Usuarios
            .AsNoTracking()
            .Where(usuario => usuario.Id == usuarioId)
            .Select(usuario => (Guid?)usuario.InstitucionId)
            .FirstOrDefaultAsync(cancellationToken);
    }


    private async Task ValidarCreacionRefreshTokensAsync(
        CancellationToken cancellationToken = default)
    {
        var institucionActualId = _tenantContext.InstitucionId;

        var tokensNuevos = ChangeTracker.Entries<RefreshToken>()
            .Where(entrada => entrada.State == EntityState.Added)
            .ToList();

        foreach (var entrada in tokensNuevos)
        {
            if (string.IsNullOrWhiteSpace(entrada.Entity.Token) || entrada.Entity.Token.Length > 255 ||
                entrada.Entity.Token.Any(char.IsWhiteSpace) ||
                entrada.Entity.FechaExpiracion.Kind != DateTimeKind.Utc ||
                entrada.Entity.FechaExpiracion <= DateTime.UtcNow)
                throw new ArgumentException("Hash sin espacios de hasta 255 caracteres y vencimiento UTC futuro requeridos.");

            if (!institucionActualId.HasValue)
                throw new TenantAccessDeniedException();

            var usuarioId = entrada.Entity.UsuarioId;

            var institucionUsuario =
                await ObtenerInstitucionDeUsuarioAsync(
                    usuarioId,
                    cancellationToken);

            if (institucionUsuario != institucionActualId.Value)
                throw new TenantAccessDeniedException();
        }
    }


    private async Task ValidarModificacionRefreshTokensAsync(
        CancellationToken cancellationToken = default)
    {
        var institucionActualId = _tenantContext.InstitucionId;

        var entradas = ChangeTracker.Entries<RefreshToken>()
            .Where(entrada => entrada.State is EntityState.Modified
                or EntityState.Deleted)
            .ToList();

        foreach (var entrada in entradas)
        {
            if (!institucionActualId.HasValue)
                throw new TenantAccessDeniedException();

            // Consultar el propietario real almacenado en PostgreSQL.
            var valoresBaseDatos = await entrada
                .GetDatabaseValuesAsync(cancellationToken);

            if (valoresBaseDatos is null)
                throw new TenantAccessDeniedException();

            var usuarioOriginalId = valoresBaseDatos
                .GetValue<Guid>(nameof(RefreshToken.UsuarioId));

            if (entrada.Entity.UsuarioId != usuarioOriginalId ||
                entrada.Entity.Token != valoresBaseDatos.GetValue<string>(nameof(RefreshToken.Token)) ||
                entrada.Entity.FechaExpiracion != valoresBaseDatos.GetValue<DateTime>(nameof(RefreshToken.FechaExpiracion)) ||
                (valoresBaseDatos.GetValue<bool>(nameof(RefreshToken.Revocado)) && !entrada.Entity.Revocado))
                throw new TenantAccessDeniedException();

            // Verificar que el propietario original sea del tenant actual.
            var institucionOriginal =
                await ObtenerInstitucionDeUsuarioAsync(
                    usuarioOriginalId,
                    cancellationToken);

            if (institucionOriginal != institucionActualId.Value)
                throw new TenantAccessDeniedException();

            // Impedir que se cambie el propietario a otro tenant.
            var institucionNuevoPropietario =
                await ObtenerInstitucionDeUsuarioAsync(
                    entrada.Entity.UsuarioId,
                    cancellationToken);

            if (institucionNuevoPropietario != institucionActualId.Value)
                throw new TenantAccessDeniedException();
        }
    }


}
