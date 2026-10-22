using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using PEI.Cursos.Application.UseCases.Matriculaciones.Commands;
using PEI.Cursos.Domain.Entities;
using PEI.Cursos.Domain.Exceptions;
using PEI.Cursos.UnitTests.Common;
using Xunit;

namespace PEI.Cursos.UnitTests.Application.Matriculaciones;

/* Pruebas unitarias para MatricularAlumnoCommandHandler (Caso de Uso CU-04).
Verifica pertenencia al tenant, estado activo de cohorte, unicidad preventiva y persistencia atomica. */
public class MatricularAlumnoCommandHandlerTests
{
    private readonly FakeCohorteRepository _cohorteRepo = new();
    private readonly FakeMatriculacionRepository _matriculacionRepo = new();
    private readonly FakeUnitOfWork _uow = new();
    private readonly FakeTenantContext _tenant = new();

    [Fact]
    public async Task HandleAsync_ConDatosValidos_DebeMatricularExitosamenteYConfirmarCommit()
    {
        // Arrange
        var institucionId = Guid.NewGuid();
        _tenant.InstitucionId = institucionId;
        var cursoId = Guid.NewGuid();
        var docenteId = Guid.NewGuid();
        var alumnoId = Guid.NewGuid();

        var cohorte = new Cohorte(institucionId, cursoId, docenteId, "Comision 1A", "2026-2C");
        await _cohorteRepo.AddAsync(cohorte);

        var handler = new MatricularAlumnoCommandHandler(_cohorteRepo, _matriculacionRepo, _uow, _tenant);
        var command = new MatricularAlumnoCommand(cohorte.Id, alumnoId);

        // Act
        var resultado = await handler.HandleAsync(command);

        // Assert
        Assert.NotNull(resultado);
        Assert.Equal(institucionId, resultado.InstitucionId);
        Assert.Equal(cohorte.Id, resultado.CohorteId);
        Assert.Equal(alumnoId, resultado.AlumnoId);
        Assert.Equal("Activa", resultado.Estado);
        Assert.Single(_matriculacionRepo.Items);
        Assert.Equal(1, _uow.CommitsRealizados);
    }

    [Fact]
    public async Task HandleAsync_ConCohorteInexistente_DebeLanzarKeyNotFoundException()
    {
        // Arrange
        _tenant.InstitucionId = Guid.NewGuid();
        var handler = new MatricularAlumnoCommandHandler(_cohorteRepo, _matriculacionRepo, _uow, _tenant);
        var command = new MatricularAlumnoCommand(Guid.NewGuid(), Guid.NewGuid());

        // Act & Assert
        var ex = await Assert.ThrowsAsync<KeyNotFoundException>(() => handler.HandleAsync(command));
        Assert.Contains("no existe o no pertenece a la institucion", ex.Message);
        Assert.Empty(_matriculacionRepo.Items);
        Assert.Equal(0, _uow.CommitsRealizados);
    }

    [Fact]
    public async Task HandleAsync_ConCohorteInactiva_DebeLanzarDomainException()
    {
        // Arrange
        var institucionId = Guid.NewGuid();
        _tenant.InstitucionId = institucionId;
        var cohorte = new Cohorte(institucionId, Guid.NewGuid(), Guid.NewGuid(), "Comision 1B", "2026-2C");
        cohorte.Desactivar();
        await _cohorteRepo.AddAsync(cohorte);

        var handler = new MatricularAlumnoCommandHandler(_cohorteRepo, _matriculacionRepo, _uow, _tenant);
        var command = new MatricularAlumnoCommand(cohorte.Id, Guid.NewGuid());

        // Act & Assert
        var ex = await Assert.ThrowsAsync<DomainException>(() => handler.HandleAsync(command));
        Assert.Contains("No es posible matricular alumnos en una cohorte inactiva", ex.Message);
        Assert.Empty(_matriculacionRepo.Items);
        Assert.Equal(0, _uow.CommitsRealizados);
    }

    [Fact]
    public async Task HandleAsync_ConAlumnoPreviamenteMatriculado_DebeLanzarInvalidOperationException()
    {
        // Arrange
        var institucionId = Guid.NewGuid();
        _tenant.InstitucionId = institucionId;
        var cohorte = new Cohorte(institucionId, Guid.NewGuid(), Guid.NewGuid(), "Comision 1C", "2026-2C");
        await _cohorteRepo.AddAsync(cohorte);

        var alumnoId = Guid.NewGuid();
        var matriculaPrevia = new Matriculacion(institucionId, cohorte.Id, alumnoId);
        await _matriculacionRepo.AddAsync(matriculaPrevia);

        var handler = new MatricularAlumnoCommandHandler(_cohorteRepo, _matriculacionRepo, _uow, _tenant);
        var command = new MatricularAlumnoCommand(cohorte.Id, alumnoId);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => handler.HandleAsync(command));
        Assert.Contains("El alumno ya se encuentra matriculado en la cohorte", ex.Message);
        Assert.Equal(0, _uow.CommitsRealizados);
    }

    [Fact]
    public async Task HandleAsync_ConTenantNoAutenticado_DebeLanzarDomainException()
    {
        // Arrange
        _tenant.InstitucionId = null;
        var handler = new MatricularAlumnoCommandHandler(_cohorteRepo, _matriculacionRepo, _uow, _tenant);
        var command = new MatricularAlumnoCommand(Guid.NewGuid(), Guid.NewGuid());

        // Act & Assert
        var ex = await Assert.ThrowsAsync<DomainException>(() => handler.HandleAsync(command));
        Assert.Contains("No se pudo identificar la institucion del usuario autenticado", ex.Message);
    }

    [Fact]
    public async Task HandleAsync_ConAlumnoIdVacio_DebeLanzarDomainException()
    {
        // Arrange
        var institucionId = Guid.NewGuid();
        _tenant.InstitucionId = institucionId;
        var cohorte = new Cohorte(institucionId, Guid.NewGuid(), Guid.NewGuid(), "Comision 1D", "2026-2C");
        await _cohorteRepo.AddAsync(cohorte);

        var handler = new MatricularAlumnoCommandHandler(_cohorteRepo, _matriculacionRepo, _uow, _tenant);
        var command = new MatricularAlumnoCommand(cohorte.Id, Guid.Empty);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<DomainException>(() => handler.HandleAsync(command));
        Assert.Contains("El alumno es obligatorio", ex.Message);
    }
}
