using Microsoft.EntityFrameworkCore;
using PEI.Evaluaciones.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddDbContext<EvaluacionDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Default")));

builder.Services.AddMediatR(cfg =>
    cfg.RegisterServicesFromAssembly(typeof(PEI.Evaluaciones.Application.Evaluaciones.CrearEvaluacionCommand).Assembly));
    
builder.Services.AddSingleton<PEI.Evaluaciones.Application.Validacion.ValidadorPreguntas>();

builder.Services.AddScoped<
    PEI.Evaluaciones.Application.Evaluaciones.IEvaluacionRepositorio,
    PEI.Evaluaciones.Infrastructure.Persistence.EvaluacionRepositorio>();

builder.Services.AddScoped<PEI.Evaluaciones.Application.Evaluaciones.ICursosClient, PEI.Evaluaciones.Infrastructure.Clientes.CursosClientProvisorio>();    

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.Lifetime.ApplicationStarted.Register(() => _ = Task.Run(async () =>
    {
        using var scope = app.Services.CreateScope();
        try
        {
            var db = scope.ServiceProvider.GetRequiredService<PEI.Evaluaciones.Infrastructure.Persistence.EvaluacionDbContext>();
            var validador = scope.ServiceProvider.GetRequiredService<PEI.Evaluaciones.Application.Validacion.ValidadorPreguntas>();
            await PEI.Evaluaciones.Infrastructure.Persistence.EvaluacionSeedData.SembrarAsync(db, validador, app.Logger);
        }
        catch (Exception ex)
        {
            app.Logger.LogWarning(ex, "No se pudo sembrar la base de datos de evaluaciones.");
        }
    }));
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
