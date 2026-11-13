using System;
using System.Threading.Tasks;
using PEI.Cursos.Application.UseCases.Matriculaciones.Commands;
using PEI.Cursos.Domain.Entities;
using PEI.Cursos.Domain.Enums;
using PEI.Cursos.UnitTests.Common;
using Xunit;

namespace PEI.Cursos.UnitTests.Application.Matriculaciones;

/* Pruebas unitarias para ReactivarMatriculacionCommandHandler.
Verifica la restauracion del estado activo, validacion de cohorte y excepciones de negocio. */
public class ReactivarMatriculacionCommandHandlerTests
{
    private readonly FakeMatriculacionRepository _matriculacionRepo = new();
    private readonly FakeUnitOfWork _uow = new();

    [Fact]
    public async Task HandleAsync_ConMatriculacionEnBaja_DebeReactivarYConfirmarCommit()
    {
        // Arrange: Alumno previamente dado de baja se reincorpora
        var institucionId = Guid.NewGuid();
        var cohorteId = Guid.NewGuid();
        var matriculacion = new Matriculacion(institucionId, cohorteId, Guid.NewGuid());
        matriculacion.DarDeBaja();
        await _matriculacionRepo.AddAsync(matriculacion);

        var handler = new ReactivarMatriculacionCommandHandler(_matriculacionRepo, _uow);
        var command = new ReactivarMatriculacionCommand(cohorteId, matriculacion.Id);

        // Act
        var resultado = await handler.HandleAsync(command);

        // Assert
        Assert.True(resultado);
        Assert.Equal(EstadoMatriculacion.Activa, matriculacion.Estado);
        Assert.Equal(1, _uow.CommitsRealizados);
    }

    [Fact]
    public async Task HandleAsync_ConMatriculacionInexistente_DebeRetornarFalseSinCommit()
    {
        // Arrange
        var handler = new ReactivarMatriculacionCommandHandler(_matriculacionRepo, _uow);
        var command = new ReactivarMatriculacionCommand(Guid.NewGuid(), Guid.NewGuid());

        // Act
        var resultado = await handler.HandleAsync(command);

        // Assert
        Assert.False(resultado);
        Assert.Equal(0, _uow.CommitsRealizados);
    }

    [Fact]
    public async Task HandleAsync_ConMatriculacionDeOtraCohorte_DebeRetornarFalseSinCommit()
    {
        // Arrange: Validacion anti-enumeracion inter-cohorte
        var institucionId = Guid.NewGuid();
        var cohorteIdReal = Guid.NewGuid();
        var cohorteIdIntento = Guid.NewGuid();
        var matriculacion = new Matriculacion(institucionId, cohorteIdReal, Guid.NewGuid());
        matriculacion.DarDeBaja();
        await _matriculacionRepo.AddAsync(matriculacion);

        var handler = new ReactivarMatriculacionCommandHandler(_matriculacionRepo, _uow);
        var command = new ReactivarMatriculacionCommand(cohorteIdIntento, matriculacion.Id);

        // Act
        var resultado = await handler.HandleAsync(command);

        // Assert
        Assert.False(resultado);
        Assert.Equal(EstadoMatriculacion.Baja, matriculacion.Estado);
        Assert.Equal(0, _uow.CommitsRealizados);
    }

    [Fact]
    public async Task HandleAsync_ConMatriculacionYaActiva_DebeLanzarInvalidOperationException()
    {
        // Arrange: Ya se encuentra en estado activo
        var cohorteId = Guid.NewGuid();
        var matriculacion = new Matriculacion(Guid.NewGuid(), cohorteId, Guid.NewGuid());
        await _matriculacionRepo.AddAsync(matriculacion);

        var handler = new ReactivarMatriculacionCommandHandler(_matriculacionRepo, _uow);
        var command = new ReactivarMatriculacionCommand(cohorteId, matriculacion.Id);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => handler.HandleAsync(command));
        Assert.Contains("ya se encuentra activa", ex.Message);
        Assert.Equal(0, _uow.CommitsRealizados);
    }

    [Fact]
    public async Task HandleAsync_ConMatriculacionCompletada_DebeLanzarInvalidOperationException()
    {
        // Arrange: Cursada completada no admite reactivacion
        var cohorteId = Guid.NewGuid();
        var matriculacion = new Matriculacion(Guid.NewGuid(), cohorteId, Guid.NewGuid());
        matriculacion.Completar();
        await _matriculacionRepo.AddAsync(matriculacion);

        var handler = new ReactivarMatriculacionCommandHandler(_matriculacionRepo, _uow);
        var command = new ReactivarMatriculacionCommand(cohorteId, matriculacion.Id);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => handler.HandleAsync(command));
        Assert.Contains("completada", ex.Message);
        Assert.Equal(0, _uow.CommitsRealizados);
    }
}
