using System;
using System.Threading.Tasks;
using PEI.Cursos.Application.UseCases.Cursos.Commands;
using PEI.Cursos.Domain.Entities;
using PEI.Cursos.UnitTests.Common;
using Xunit;

namespace PEI.Cursos.UnitTests.Application.Cursos;

/* Pruebas unitarias para ActualizarCursoCommandHandler.
Verifica la actualizacion de datos, la validacion preventiva de nombres repetidos y la respuesta nula para 404. */
public class ActualizarCursoCommandHandlerTests
{
    private readonly FakeCursoRepository _repo = new();
    private readonly FakeUnitOfWork _uow = new();

    [Fact]
    public async Task HandleAsync_ConDatosValidos_DebeActualizarCursoYConfirmar()
    {
        // Arrange
        var institucionId = Guid.NewGuid();
        var curso = new Curso(institucionId, "Programacion 1", "Sistemas");
        await _repo.AddAsync(curso);

        var handler = new ActualizarCursoCommandHandler(_repo, _uow);
        var command = new ActualizarCursoCommand(curso.Id, "Programacion Avanzada", "Computacion");

        // Act
        var resultado = await handler.HandleAsync(command);

        // Assert
        Assert.NotNull(resultado);
        Assert.Equal(curso.Id, resultado.Id);
        Assert.Equal("Programacion Avanzada", resultado.Nombre);
        Assert.Equal("Computacion", resultado.Area);
        Assert.Equal(1, _uow.CommitsRealizados);
    }

    [Fact]
    public async Task HandleAsync_ConCursoInexistente_DebeRetornarNull()
    {
        // Arrange
        var handler = new ActualizarCursoCommandHandler(_repo, _uow);
        var command = new ActualizarCursoCommand(Guid.NewGuid(), "Nombre Nuevo", "Area Nueva");

        // Act
        var resultado = await handler.HandleAsync(command);

        // Assert
        Assert.Null(resultado);
        Assert.Equal(0, _uow.CommitsRealizados);
    }

    [Fact]
    public async Task HandleAsync_ConNombreDuplicadoEnOtraMateria_DebeLanzarInvalidOperationException()
    {
        // Arrange
        var institucionId = Guid.NewGuid();
        var curso1 = new Curso(institucionId, "Algebra A", "Exactas");
        var curso2 = new Curso(institucionId, "Algebra B", "Exactas");
        await _repo.AddAsync(curso1);
        await _repo.AddAsync(curso2);

        var handler = new ActualizarCursoCommandHandler(_repo, _uow);
        var command = new ActualizarCursoCommand(curso1.Id, "Algebra B", "Exactas");

        // Act & Assert
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => handler.HandleAsync(command));
        Assert.Contains("Ya existe un curso con el nombre", ex.Message);
        Assert.Equal(0, _uow.CommitsRealizados);
    }
}
