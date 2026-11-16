using System;
using System.Threading.Tasks;
using PEI.Cursos.Application.UseCases.Matriculaciones.Commands;
using PEI.Cursos.Domain.Entities;
using PEI.Cursos.Domain.Enums;
using PEI.Cursos.UnitTests.Common;
using Xunit;

namespace PEI.Cursos.UnitTests.Application.Matriculaciones;

/* Pruebas unitarias para DarDeBajaMatriculacionCommandHandler.
Verifica la baja logica, consistencia de cohorte, excepciones por estado y persistencia atomica. */
public class DarDeBajaMatriculacionCommandHandlerTests
{
    private readonly FakeMatriculacionRepository _matriculacionRepo = new();
    private readonly FakeUnitOfWork _uow = new();

    [Fact]
    public async Task HandleAsync_ConMatriculacionValida_DebeDarDeBajaYConfirmarCommit()
    {
        // Arrange
        var institucionId = Guid.NewGuid();
        var cohorteId = Guid.NewGuid();
        var matriculacion = new Matriculacion(institucionId, cohorteId, Guid.NewGuid());
        await _matriculacionRepo.AddAsync(matriculacion);

        var handler = new DarDeBajaMatriculacionCommandHandler(_matriculacionRepo, _uow);
        var command = new DarDeBajaMatriculacionCommand(cohorteId, matriculacion.Id);

        // Act
        var resultado = await handler.HandleAsync(command);

        // Assert
        Assert.True(resultado);
        Assert.Equal(EstadoMatriculacion.Baja, matriculacion.Estado);
        Assert.Equal(1, _uow.CommitsRealizados);
    }

    [Fact]
    public async Task HandleAsync_ConMatriculacionInexistente_DebeRetornarFalseSinCommit()
    {
        // Arrange
        var handler = new DarDeBajaMatriculacionCommandHandler(_matriculacionRepo, _uow);
        var command = new DarDeBajaMatriculacionCommand(Guid.NewGuid(), Guid.NewGuid());

        // Act
        var resultado = await handler.HandleAsync(command);

        // Assert
        Assert.False(resultado);
        Assert.Equal(0, _uow.CommitsRealizados);
    }

    [Fact]
    public async Task HandleAsync_ConMatriculacionDeOtraCohorte_DebeRetornarFalseSinCommit()
    {
        // Arrange: Regla anti-enumeracion cuando no coincide la cohorte
        var institucionId = Guid.NewGuid();
        var cohorteIdReal = Guid.NewGuid();
        var cohorteIdIntento = Guid.NewGuid();
        var matriculacion = new Matriculacion(institucionId, cohorteIdReal, Guid.NewGuid());
        await _matriculacionRepo.AddAsync(matriculacion);

        var handler = new DarDeBajaMatriculacionCommandHandler(_matriculacionRepo, _uow);
        var command = new DarDeBajaMatriculacionCommand(cohorteIdIntento, matriculacion.Id);

        // Act
        var resultado = await handler.HandleAsync(command);

        // Assert
        Assert.False(resultado);
        Assert.Equal(EstadoMatriculacion.Activa, matriculacion.Estado);
        Assert.Equal(0, _uow.CommitsRealizados);
    }

    [Fact]
    public async Task HandleAsync_ConMatriculacionYaEnBaja_DebeLanzarInvalidOperationException()
    {
        // Arrange
        var cohorteId = Guid.NewGuid();
        var matriculacion = new Matriculacion(Guid.NewGuid(), cohorteId, Guid.NewGuid());
        matriculacion.DarDeBaja();
        await _matriculacionRepo.AddAsync(matriculacion);

        var handler = new DarDeBajaMatriculacionCommandHandler(_matriculacionRepo, _uow);
        var command = new DarDeBajaMatriculacionCommand(cohorteId, matriculacion.Id);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => handler.HandleAsync(command));
        Assert.Contains("ya se encuentra dada de baja", ex.Message);
        Assert.Equal(0, _uow.CommitsRealizados);
    }

    [Fact]
    public async Task HandleAsync_ConMatriculacionCompletada_DebeLanzarInvalidOperationException()
    {
        // Arrange: Cursada ya finalizada y completada no permite baja
        var cohorteId = Guid.NewGuid();
        var matriculacion = new Matriculacion(Guid.NewGuid(), cohorteId, Guid.NewGuid());
        matriculacion.Completar();
        await _matriculacionRepo.AddAsync(matriculacion);

        var handler = new DarDeBajaMatriculacionCommandHandler(_matriculacionRepo, _uow);
        var command = new DarDeBajaMatriculacionCommand(cohorteId, matriculacion.Id);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => handler.HandleAsync(command));
        Assert.Contains("ya ha sido completada", ex.Message);
        Assert.Equal(0, _uow.CommitsRealizados);
    }
}
