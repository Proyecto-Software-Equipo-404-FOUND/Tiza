using System;
using System.Threading.Tasks;
using PEI.Cursos.Application.UseCases.Cohortes.Commands;
using PEI.Cursos.Domain.Entities;
using PEI.Cursos.Domain.Exceptions;
using PEI.Cursos.UnitTests.Common;
using Xunit;

namespace PEI.Cursos.UnitTests.Application.Cohortes;

/* Pruebas unitarias para DesactivarCohorteCommandHandler.
Verifica la baja logica, aislamiento por curso y proteccion ante comisiones ya inactivas. */
public class DesactivarCohorteCommandHandlerTests
{
    private readonly FakeCohorteRepository _cohorteRepo = new();
    private readonly FakeUnitOfWork _uow = new();

    [Fact]
    public async Task HandleAsync_CohorteActiva_DesactivaConExitoYConfirma()
    {
        // Arrange
        var institucionId = Guid.NewGuid();
        var cursoId = Guid.NewGuid();
        var cohorte = new Cohorte(institucionId, cursoId, Guid.NewGuid(), "Comision 1A", "2026-1C");
        await _cohorteRepo.AddAsync(cohorte);

        var handler = new DesactivarCohorteCommandHandler(_cohorteRepo, _uow);
        var command = new DesactivarCohorteCommand(cursoId, cohorte.Id);

        // Act
        var resultado = await handler.HandleAsync(command);

        // Assert
        Assert.True(resultado);
        Assert.False(cohorte.Activo);
        Assert.Equal(1, _uow.CommitsRealizados);
    }

    [Fact]
    public async Task HandleAsync_CohorteInexistente_RetornaFalse()
    {
        // Arrange
        var handler = new DesactivarCohorteCommandHandler(_cohorteRepo, _uow);
        var command = new DesactivarCohorteCommand(Guid.NewGuid(), Guid.NewGuid());

        // Act
        var resultado = await handler.HandleAsync(command);

        // Assert
        Assert.False(resultado);
        Assert.Equal(0, _uow.CommitsRealizados);
    }

    [Fact]
    public async Task HandleAsync_CursoIdNoCoincide_RetornaFalse()
    {
        // Arrange
        var institucionId = Guid.NewGuid();
        var cursoIdReal = Guid.NewGuid();
        var cursoIdErroneo = Guid.NewGuid();
        var cohorte = new Cohorte(institucionId, cursoIdReal, Guid.NewGuid(), "Comision 1A", "2026-1C");
        await _cohorteRepo.AddAsync(cohorte);

        var handler = new DesactivarCohorteCommandHandler(_cohorteRepo, _uow);
        var command = new DesactivarCohorteCommand(cursoIdErroneo, cohorte.Id);

        // Act
        var resultado = await handler.HandleAsync(command);

        // Assert
        Assert.False(resultado);
        Assert.Equal(0, _uow.CommitsRealizados);
        Assert.True(cohorte.Activo);
    }

    [Fact]
    public async Task HandleAsync_CohorteYaInactiva_LanzaDomainException()
    {
        // Arrange
        var institucionId = Guid.NewGuid();
        var cursoId = Guid.NewGuid();
        var cohorte = new Cohorte(institucionId, cursoId, Guid.NewGuid(), "Comision Ya Cerrada", "2026-1C");
        cohorte.Desactivar();
        await _cohorteRepo.AddAsync(cohorte);

        var handler = new DesactivarCohorteCommandHandler(_cohorteRepo, _uow);
        var command = new DesactivarCohorteCommand(cursoId, cohorte.Id);

        // Act & Assert
        await Assert.ThrowsAsync<DomainException>(() => handler.HandleAsync(command));
    }
}
