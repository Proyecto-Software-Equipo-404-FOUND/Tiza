using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using PEI.Identidad.Domain.Entities;

namespace PEI.Identidad.Infrastructure.Persistence;

public class IdentidadDbContext : DbContext
{
    public IdentidadDbContext (
        DbContextOptions<IdentidadDbContext> options) : base(options)
    { }

    public DbSet<Institucion> Instituciones => Set<Institucion>();
    public DbSet<Usuario> Usuarios => Set<Usuario>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(
            typeof(IdentidadDbContext).Assembly );
    }
}
