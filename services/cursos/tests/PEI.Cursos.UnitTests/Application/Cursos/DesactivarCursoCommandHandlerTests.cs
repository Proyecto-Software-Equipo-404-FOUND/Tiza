using System;
using System.Threading.Tasks;
using PEI.Cursos.Application.UseCases.Cursos.Commands;
using PEI.Cursos.Domain.Entities;
using PEI.Cursos.Domain.Exceptions;
using PEI.Cursos.UnitTests.Common;
using Xunit;

namespace PEI.Cursos.UnitTests.Application.Cursos;

/* Pruebas unitarias para DesactivarCursoCommandHandler.
Verifica la baja logica, la transicion de estado y el retorno de false para 404 Not Found. */
public class DesactivarCursoCommandHandlerTests
{
    private readonly FakeCursoRepository _repo = new();
    private readonly FakeUnitOfWork _uow = new();

    [Fact]
    public async Task HandleAsync_ConCursoActivo_DebeDesactivarYConfirmar()
    {
        // Arrange
        var institucionId = Guid.NewGuid();
        var curso = new Curso(institucionId, "Laboratorio 1", "Sistemas");
        await _repo.AddAsync(curso);

        var handler = new DesactivarCursoCommandHandler(_repo, _uow);
        var command = new DesactivarCursoCommand(curso.Id);

        // Act
        var resultado = await handler.HandleAsync(command);

        // Assert
        Assert.True(resultado);
        Assert.False(curso.Activo);
        Assert.NotNull(curso.FechaModificacionUtc);
        Assert.Equal(1, _uow.CommitsRealizados);
    }

    [Fact]
    public async Task HandleAsync_ConCursoInexistente_DebeRetornarFalse()
    {
        // Arrange
        var handler = new DesactivarCursoCommandHandler(_repo, _uow);
        var command = new DesactivarCursoCommand(Guid.NewGuid());

        // Act
        var resultado = await handler.HandleAsync(command);

        // Assert
        Assert.False(resultado);
        Assert.Equal(0, _uow.CommitsRealizados);
    }

    [Fact]
    public async Task HandleAsync_ConCursoYaInactivo_DebeLanzarDomainException()
    {
        // Arrange
        var institucionId = Guid.NewGuid();
        var curso = new Curso(institucionId, "Seminario", "Sistemas");
        curso.Desactivar();
        await _repo.AddAsync(curso);

        var handler = new DesactivarCursoCommandHandler(_repo, _uow);
        var command = new DesactivarCursoCommand(curso.Id);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<DomainException>(() => handler.HandleAsync(command));
        Assert.Contains("El curso ya se encuentra inactivo", ex.Message);
        Assert.Equal(0, _uow.CommitsRealizados);
    }
}
