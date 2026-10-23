using System;
using System.Threading.Tasks;
using PEI.Cursos.Application.UseCases.Cohortes.Commands;
using PEI.Cursos.Domain.Entities;
using PEI.Cursos.Domain.Exceptions;
using PEI.Cursos.UnitTests.Common;
using Xunit;

namespace PEI.Cursos.UnitTests.Application.Cohortes;

/* Pruebas unitarias para ActualizarCohorteCommandHandler.
Valida la modificacion descriptiva, reasignacion docente, no duplicidad y proteccion de inactivas. */
public class ActualizarCohorteCommandHandlerTests
{
    private readonly FakeCohorteRepository _cohorteRepo = new();
    private readonly FakeUnitOfWork _uow = new();

    [Fact]
    public async Task HandleAsync_CohorteExistente_ActualizaDatosYReasignaDocenteConExito()
    {
        // Arrange
        var institucionId = Guid.NewGuid();
        var cursoId = Guid.NewGuid();
        var docenteInicial = Guid.NewGuid();
        var cohorte = new Cohorte(institucionId, cursoId, docenteInicial, "Comision 1A", "2026-1C");
        await _cohorteRepo.AddAsync(cohorte);

        var nuevoDocente = Guid.NewGuid();
        var handler = new ActualizarCohorteCommandHandler(_cohorteRepo, _uow);
        var command = new ActualizarCohorteCommand(cursoId, cohorte.Id, "Comision 1B - Modificada", "2026-2C", nuevoDocente);

        // Act
        var resultado = await handler.HandleAsync(command);

        // Assert
        Assert.NotNull(resultado);
        Assert.Equal("Comision 1B - Modificada", resultado.Nombre);
        Assert.Equal("2026-2C", resultado.Periodo);
        Assert.Equal(nuevoDocente, resultado.DocenteId);
        Assert.Equal(1, _uow.CommitsRealizados);
    }

    [Fact]
    public async Task HandleAsync_CohorteInexistente_RetornaNull()
    {
        // Arrange
        var handler = new ActualizarCohorteCommandHandler(_cohorteRepo, _uow);
        var command = new ActualizarCohorteCommand(Guid.NewGuid(), Guid.NewGuid(), "Comision X", "2026-1C", Guid.NewGuid());

        // Act
        var resultado = await handler.HandleAsync(command);

        // Assert
        Assert.Null(resultado);
        Assert.Equal(0, _uow.CommitsRealizados);
    }

    [Fact]
    public async Task HandleAsync_CursoIdNoCoincide_RetornaNull()
    {
        // Arrange
        var institucionId = Guid.NewGuid();
        var cursoIdReal = Guid.NewGuid();
        var cursoIdErroneo = Guid.NewGuid();
        var cohorte = new Cohorte(institucionId, cursoIdReal, Guid.NewGuid(), "Comision 1A", "2026-1C");
        await _cohorteRepo.AddAsync(cohorte);

        var handler = new ActualizarCohorteCommandHandler(_cohorteRepo, _uow);
        var command = new ActualizarCohorteCommand(cursoIdErroneo, cohorte.Id, "Comision 1A", "2026-1C", Guid.NewGuid());

        // Act
        var resultado = await handler.HandleAsync(command);

        // Assert
        Assert.Null(resultado);
        Assert.Equal(0, _uow.CommitsRealizados);
    }

    [Fact]
    public async Task HandleAsync_NombreYPeriodoDuplicadoEnMismoCurso_LanzaInvalidOperationException()
    {
        // Arrange
        var institucionId = Guid.NewGuid();
        var cursoId = Guid.NewGuid();
        var cohorte1 = new Cohorte(institucionId, cursoId, Guid.NewGuid(), "Comision 1A", "2026-1C");
        var cohorte2 = new Cohorte(institucionId, cursoId, Guid.NewGuid(), "Comision 1B", "2026-1C");
        await _cohorteRepo.AddAsync(cohorte1);
        await _cohorteRepo.AddAsync(cohorte2);

        var handler = new ActualizarCohorteCommandHandler(_cohorteRepo, _uow);
        // Intentamos renombrar la cohorte2 con el nombre y periodo de la cohorte1
        var command = new ActualizarCohorteCommand(cursoId, cohorte2.Id, "Comision 1A", "2026-1C", Guid.NewGuid());

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => handler.HandleAsync(command));
        Assert.Equal(0, _uow.CommitsRealizados);
    }

    [Fact]
    public async Task HandleAsync_MismoNombreYPeriodoDeLaPropiaCohorte_ActualizaDocenteSinConflicto()
    {
        // Arrange
        var institucionId = Guid.NewGuid();
        var cursoId = Guid.NewGuid();
        var docenteOriginal = Guid.NewGuid();
        var cohorte = new Cohorte(institucionId, cursoId, docenteOriginal, "Comision Unica", "2026-1C");
        await _cohorteRepo.AddAsync(cohorte);

        var nuevoDocente = Guid.NewGuid();
        var handler = new ActualizarCohorteCommandHandler(_cohorteRepo, _uow);
        // Mantiene el mismo nombre y periodo, pero reasigna docente
        var command = new ActualizarCohorteCommand(cursoId, cohorte.Id, "Comision Unica", "2026-1C", nuevoDocente);

        // Act
        var resultado = await handler.HandleAsync(command);

        // Assert
        Assert.NotNull(resultado);
        Assert.Equal(nuevoDocente, resultado.DocenteId);
        Assert.Equal(1, _uow.CommitsRealizados);
    }

    [Fact]
    public async Task HandleAsync_CohorteInactiva_LanzaDomainException()
    {
        // Arrange
        var institucionId = Guid.NewGuid();
        var cursoId = Guid.NewGuid();
        var cohorte = new Cohorte(institucionId, cursoId, Guid.NewGuid(), "Comision Cerrada", "2026-1C");
        cohorte.Desactivar();
        await _cohorteRepo.AddAsync(cohorte);

        var handler = new ActualizarCohorteCommandHandler(_cohorteRepo, _uow);
        var command = new ActualizarCohorteCommand(cursoId, cohorte.Id, "Comision Intentando Modificar", "2026-2C", Guid.NewGuid());

        // Act & Assert
        await Assert.ThrowsAsync<DomainException>(() => handler.HandleAsync(command));
    }
}
