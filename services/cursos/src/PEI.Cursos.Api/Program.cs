using PEI.BuildingBlocks.Errors;
using PEI.BuildingBlocks.Logging;
using PEI.BuildingBlocks.MultiTenancy;
using PEI.Cursos.Application;
using PEI.Cursos.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

// Configuracion de servicios
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Registro de capas de Application e Infrastructure
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

// Contexto multi-tenant por solicitud
builder.Services.AddScoped<TenantContext>();
builder.Services.AddScoped<ITenantContext>(sp =>
    sp.GetRequiredService<TenantContext>());

var app = builder.Build();

// Middlewares transversales de BuildingBlocks en pipeline ordenado
app.UseMiddleware<CorrelationIdMiddleware>();
app.UseMiddleware<ProblemDetailsMiddleware>();
app.UseMiddleware<TenantContextMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();

    // Siembra de datos semilla en segundo plano para desarrollo local
    app.Lifetime.ApplicationStarted.Register(() => _ = Task.Run(async () =>
    {
        using var scope = app.Services.CreateScope();
        try
        {
            var db = scope.ServiceProvider.GetRequiredService<PEI.Cursos.Infrastructure.Persistence.CursosDbContext>();
            await PEI.Cursos.Infrastructure.Persistence.DataSeeder.SembrarAsync(db, app.Logger);
        }
        catch (Exception ex)
        {
            app.Logger.LogWarning(ex, "No se pudo sembrar la base de datos inicial de Cursos.");
        }
    }));
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
