using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Microsoft.EntityFrameworkCore.Storage;
using PEI.Identidad.Application.Abstractions;
using PEI.Identidad.Application.Exceptions;
using PEI.Identidad.Domain.Entities;
using PEI.Identidad.Domain.Security;
using PEI.Identidad.Infrastructure.Persistence;
namespace PEI.Identidad.Infrastructure.Repositories;
public sealed class RegistrationDataAccess(IdentidadDbContext db, IHttpContextAccessor http) : IRegistrationDataAccess, IAsyncDisposable
{
    private IDbContextTransaction? registrationTransaction;
    public async Task<bool> PuedeRegistrarAsync(Guid institucionId)
    {
        var user = http.HttpContext?.User;
        if (user?.Identity?.IsAuthenticated != true || !user.IsInRole(IdentityRoles.Administrador) ||
            db.InstitucionActualId != institucionId || !Guid.TryParse(user.FindFirst("sub")?.Value, out var actor) || actor == Guid.Empty) return false;
        // Mantener institucion y administrador estables hasta guardar: una consulta previa sin locks no basta.
        registrationTransaction = await db.Database.BeginTransactionAsync();
        var actores = await db.Database.SqlQuery<Guid>($"""
            SELECT u."Id" AS "Value" FROM "Usuario" u
            JOIN "Institucion" i ON i."Id" = u."InstitucionId"
            WHERE u."Id" = {actor} AND i."Id" = {institucionId}
              AND u."Rol" = {IdentityRoles.Administrador} AND u."Activo" AND i."Activo"
            FOR SHARE OF u, i
            """).ToListAsync();
        if (actores.Count == 1) return true;
        await DisposeAsync();
        return false;
    }
    public Task<bool> ExisteEmailAsync(string email) => db.Usuarios.IgnoreQueryFilters().AsNoTracking()
        .AnyAsync(u => EF.Property<string>(u, "EmailNormalizado") == EmailDbFunctions.Normalize(email));
    public async Task AddAsync(Usuario usuario) => await db.Usuarios.AddAsync(usuario);
    public async Task SaveChangesAsync()
    {
        try
        {
            await db.SaveChangesAsync();
            if (registrationTransaction is not null) await registrationTransaction.CommitAsync();
        }
        catch (DbUpdateException e) when (e.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        { throw new RegistrationConflictException(); }
        finally { await DisposeAsync(); }
    }
    public async ValueTask DisposeAsync()
    {
        if (registrationTransaction is not null)
        {
            await registrationTransaction.DisposeAsync();
            registrationTransaction = null;
        }
    }
}
public sealed class AuthenticatedUserValidator(IdentidadDbContext db) : IAuthenticatedUserValidator
{
    public Task<bool> IsActiveAsync(Guid usuarioId, Guid institucionId, string role, CancellationToken cancellationToken) =>
        db.Usuarios.IgnoreQueryFilters().AsNoTracking().AnyAsync(u => u.Id == usuarioId && u.InstitucionId == institucionId &&
            u.Rol == role && u.Activo && u.Institucion.Activo, cancellationToken);
}
