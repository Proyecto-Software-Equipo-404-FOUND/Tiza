
using Microsoft.EntityFrameworkCore;
using PEI.Identidad.Application.Abstractions;
using PEI.Identidad.Infrastructure.Persistence;
using PEI.Identidad.Infrastructure.Tenancy;
using PEI.Identidad.Infrastructure.Authentication;

var builder = WebApplication.CreateBuilder(args);

// Conexion a PostgreSQL
var connectionString = builder.Configuration
    .GetConnectionString("IdentidadDb")
    ?? throw new InvalidOperationException(
        "No se configuro la conexion IdentidadDb.");

builder.Services.AddDbContext<IdentidadDbContext>(options =>
    options.UseNpgsql(connectionString));

// Contexto de institucion (multi-tenant)
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ITenantContext, TenantContext>();

// Acceso controlado a datos para autenticacion
builder.Services.AddScoped<IAuthenticationDataAccess, AuthenticationDataAccess>();

// Escritura de refresh tokens durante autenticacion
builder.Services.AddScoped<IAuthenticationTokenWriter, AuthenticationTokenWriter>();

// Servicios de la API
builder.Services.AddExceptionHandler<PEI.Identidad.Api.Errors.TenantAccessDeniedHandler>();
builder.Services.AddProblemDetails();
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

app.UseExceptionHandler();
app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
