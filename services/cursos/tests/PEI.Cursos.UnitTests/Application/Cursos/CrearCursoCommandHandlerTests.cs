using System;
using System.Threading.Tasks;
using PEI.Cursos.Application.UseCases.Cursos.Commands;
using PEI.Cursos.Domain.Entities;
using PEI.Cursos.Domain.Exceptions;
using PEI.Cursos.UnitTests.Common;
using Xunit;

namespace PEI.Cursos.UnitTests.Application.Cursos;

/* Pruebas unitarias para CrearCursoCommandHandler.
Verifica la orquestacion de reglas de negocio, aislamiento tenant y persistencia atomica. */
public class CrearCursoCommandHandlerTests
{
    private readonly FakeCursoRepository _repo = new();
    private readonly FakeUnitOfWork _uow = new();
    private readonly FakeTenantContext _tenant = new();

    [Fact]
    public async Task HandleAsync_ConDatosValidos_DebeCrearCursoYConfirmar()
    {
        // Arrange
        var institucionId = Guid.NewGuid();
        _tenant.InstitucionId = institucionId;
        var handler = new CrearCursoCommandHandler(_repo, _uow, _tenant);
        var command = new CrearCursoCommand("Algoritmos 1", "Informatica");

        // Act
        var resultado = await handler.HandleAsync(command);

        // Assert
        Assert.NotNull(resultado);
        Assert.Equal("Algoritmos 1", resultado.Nombre);
        Assert.Equal("Informatica", resultado.Area);
        Assert.Equal(institucionId, resultado.InstitucionId);
        Assert.True(resultado.Activo);
        Assert.Single(_repo.Items);
        Assert.Equal(1, _uow.CommitsRealizados);
    }

    [Fact]
    public async Task HandleAsync_SinTenantAutenticado_DebeLanzarDomainException()
    {
        // Arrange
        _tenant.InstitucionId = null;
        var handler = new CrearCursoCommandHandler(_repo, _uow, _tenant);
        var command = new CrearCursoCommand("Fisica 1", "Ciencias Exactas");

        // Act & Assert
        var ex = await Assert.ThrowsAsync<DomainException>(() => handler.HandleAsync(command));
        Assert.Contains("No se pudo identificar la institucion", ex.Message);
        Assert.Empty(_repo.Items);
        Assert.Equal(0, _uow.CommitsRealizados);
    }

    [Fact]
    public async Task HandleAsync_ConNombreDuplicadoEnInstitucion_DebeLanzarDomainException()
    {
        // Arrange
        var institucionId = Guid.NewGuid();
        _tenant.InstitucionId = institucionId;
        await _repo.AddAsync(new Curso(institucionId, "Quimica General", "Exactas"));

        var handler = new CrearCursoCommandHandler(_repo, _uow, _tenant);
        var command = new CrearCursoCommand("Quimica General", "Exactas");

        // Act & Assert
        var ex = await Assert.ThrowsAsync<DomainException>(() => handler.HandleAsync(command));
        Assert.Contains("Ya existe un curso con el nombre", ex.Message);
        Assert.Equal(0, _uow.CommitsRealizados);
    }
}
