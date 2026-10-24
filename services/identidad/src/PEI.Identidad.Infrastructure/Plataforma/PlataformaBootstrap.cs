using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using PEI.Identidad.Application.Interfaces.Security;
using PEI.Identidad.Domain.Entities;
using PEI.Identidad.Infrastructure.Persistence;

namespace PEI.Identidad.Infrastructure.Plataforma;

public sealed class PlataformaBootstrap(PlataformaDbContext db, IPasswordHasher passwords)
{
    public async Task EjecutarAsync(string email, string password, string nombre, string apellido, string operador, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(email) || email.Length > 150 || !new EmailAddressAttribute().IsValid(email.Trim()) ||
            string.IsNullOrWhiteSpace(nombre) || nombre.Length > 100 || string.IsNullOrWhiteSpace(apellido) || apellido.Length > 100 ||
            string.IsNullOrWhiteSpace(operador) || operador.Length > 150) throw new ArgumentException("Datos de bootstrap inválidos.");
        var hash = passwords.Hash(password);
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(20061006)", ct);
        var inicializado = await db.Database.SqlQuery<int>($"""
            SELECT "Id" AS "Value" FROM "BootstrapPlataforma"
            """).AnyAsync(ct);
        if (inicializado || await db.CuentasPlataforma.AnyAsync(ct)) throw new InvalidOperationException("La plataforma ya fue inicializada; bootstrap rechazado.");
        var cuenta = new CuentaPlataforma { Email = email, PasswordHash = hash, Nombre = nombre.Trim(), Apellido = apellido.Trim() };
        db.Add(cuenta);
        await db.SaveChangesAsync(ct);
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO "BootstrapPlataforma" ("Id","CuentaId","Operador","Fecha") VALUES (1,{cuenta.Id},{operador},{DateTime.UtcNow})
            """, ct);
        await tx.CommitAsync(ct);
    }
}
