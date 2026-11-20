using Microsoft.EntityFrameworkCore;
using Npgsql;
using PEI.Cursos.Application.UseCases.Matriculaciones;
using PEI.Cursos.Domain.Entities;
using PEI.Cursos.Infrastructure.Persistence.Configurations;
using System.Threading;
using System.Threading.Tasks;
using PEI.Cursos.Application.Interfaces;
using PEI.Cursos.Infrastructure.Persistence;

namespace PEI.Cursos.Infrastructure.Persistence;

/* Implementacion concreta del patron Unit of Work sobre el DbContext de EF Core.
Concentra y confirma en una unica transaccion atonica todos los cambios acumulados por los repositorios. */
public class UnitOfWork : IUnitOfWork
{
    private readonly CursosDbContext _context;

    public UnitOfWork(CursosDbContext context)
    {
        _context = context;
    }

    public async Task<int> CommitAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            return await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException
            { SqlState: PostgresErrorCodes.UniqueViolation, TableName: "Matriculaciones" } violation &&
            violation.ConstraintName is MatriculacionConfiguration.UnaPorCohorte or MatriculacionConfiguration.UnaActivaPorCurso)
        {
            // Un alta simultánea en la misma cohorte puede chocar con cualquiera de los índices.
            var alta = ex.Entries.SingleOrDefault(e => e.State == EntityState.Added)?.Entity as Matriculacion;
            if (violation.ConstraintName == MatriculacionConfiguration.UnaPorCohorte ||
                (alta is not null && await _context.Matriculaciones.AnyAsync(m =>
                    m.InstitucionId == alta.InstitucionId && m.CohorteId == alta.CohorteId &&
                    m.AlumnoId == alta.AlumnoId, cancellationToken)))
                throw new InvalidOperationException("El alumno ya se encuentra matriculado en la cohorte.");

            throw new AlumnoEnOtraComisionException();
        }
    }
}
