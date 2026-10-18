using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PEI.Cursos.Infrastructure.Persistence;

namespace PEI.Cursos.Infrastructure;

/* Extensiones de IServiceCollection para el registro de dependencias de Infrastructure.
Configura CursosDbContext con el proveedor Npgsql de PostgreSQL. */
public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("CursosDb")
            ?? throw new InvalidOperationException("La cadena de conexión 'CursosDb' no fue encontrada.");

        services.AddDbContext<CursosDbContext>(options =>
            options.UseNpgsql(connectionString));

        return services;
    }
}
