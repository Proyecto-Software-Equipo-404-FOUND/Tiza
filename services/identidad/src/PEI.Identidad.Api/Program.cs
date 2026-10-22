using Microsoft.EntityFrameworkCore;
using PEI.Identidad.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

// Conexion a PostgreSQL
var connectionString = builder.Configuration
    .GetConnectionString("IdentidadDb")
    ?? throw new InvalidOperationException(
        "No se configuro la conexion IdentidadDb.");

builder.Services.AddDbContext<IdentidadDbContext>(options =>
    options.UseNpgsql(connectionString));

// Servicios de la API
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Construccion de la aplicacion
var app = builder.Build();

// Configuracion del pipeline HTTP
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
