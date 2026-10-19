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

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

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
