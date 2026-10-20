using services.SalaEnVivo.src.PEI.SalaEnVivo.Application.Interfaces;
using services.SalaEnVivo.src.PEI.SalaEnVivo.Application.Interfaces.Repositories;
using services.SalaEnVivo.src.PEI.SalaEnVivo.Application.UseCases.Salas.Handlers;
using services.SalaEnVivo.src.PEI.SalaEnVivo.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<pei_sala_db>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();
builder.Services.AddScoped<ISalaRepository, SalaRepository>();

builder.Services.AddScoped<CrearSalaCommandHandler>();

// Add services to the container.

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
