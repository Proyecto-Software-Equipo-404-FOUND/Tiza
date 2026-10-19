using System;
using System.Threading.Tasks;
using PEI.Cursos.Application.UseCases.Cohortes.Commands;
using PEI.Cursos.Domain.Entities;
using PEI.Cursos.Domain.Exceptions;
using PEI.Cursos.UnitTests.Common;
using Xunit;

namespace PEI.Cursos.UnitTests.Application.Cohortes;

/* Pruebas unitarias para CrearCohorteCommandHandler.
Verifica las invariantes de relacion curso-cohorte, no duplicidad y confirmacion transaccional. */
public class CrearCohorteCommandHandlerTests
{
    private readonly FakeCursoRepository _cursoRepo = new();
    private readonly FakeCohorteRepository _cohorteRepo = new();
    private readonly FakeUnitOfWork _uow = new();
    private readonly FakeTenantContext _tenant = new();

    [Fact]
    public async Task HandleAsync_ConDatosValidos_DebeCrearCohorteYConfirmar()
    {
        // Arrange
        var institucionId = Guid.NewGuid();
        _tenant.InstitucionId = institucionId;
        var curso = new Curso(institucionId, "Paradigmas de Programacion", "Sistemas");
        await _cursoRepo.AddAsync(curso);

        var docenteId = Guid.NewGuid();
        var handler = new CrearCohorteCommandHandler(_cursoRepo, _cohorteRepo, _uow, _tenant);
        var command = new CrearCohorteCommand(curso.Id, "Comision K3001", "2026-1C", docenteId);

        // Act
        var resultado = await handler.HandleAsync(command);

        // Assert
        Assert.NotNull(resultado);
        Assert.Equal("Comision K3001", resultado.Nombre);
        Assert.Equal("2026-1C", resultado.Periodo);
        Assert.Equal(curso.Id, resultado.CursoId);
        Assert.Equal(docenteId, resultado.DocenteId);
        Assert.True(resultado.Activo);
        Assert.Single(_cohorteRepo.Items);
        Assert.Equal(1, _uow.CommitsRealizados);
    }

    [Fact]
    public async Task HandleAsync_ConCursoInexistente_DebeLanzarDomainException()
    {
        // Arrange
        _tenant.InstitucionId = Guid.NewGuid();
        var handler = new CrearCohorteCommandHandler(_cursoRepo, _cohorteRepo, _uow, _tenant);
        var command = new CrearCohorteCommand(Guid.NewGuid(), "Comision 1", "2026-1C", Guid.NewGuid());

        // Act & Assert
        var ex = await Assert.ThrowsAsync<DomainException>(() => handler.HandleAsync(command));
        Assert.Contains("El curso especificado no existe", ex.Message);
        Assert.Empty(_cohorteRepo.Items);
        Assert.Equal(0, _uow.CommitsRealizados);
    }

    [Fact]
    public async Task HandleAsync_ConCursoInactivo_DebeLanzarDomainException()
    {
        // Arrange
        var institucionId = Guid.NewGuid();
        _tenant.InstitucionId = institucionId;
        var curso = new Curso(institucionId, "Redes de Informacion", "Sistemas");
        curso.Desactivar();
        await _cursoRepo.AddAsync(curso);

        var handler = new CrearCohorteCommandHandler(_cursoRepo, _cohorteRepo, _uow, _tenant);
        var command = new CrearCohorteCommand(curso.Id, "Comision 101", "2026-1C", Guid.NewGuid());

        // Act & Assert
        var ex = await Assert.ThrowsAsync<DomainException>(() => handler.HandleAsync(command));
        Assert.Contains("No es posible registrar cohortes en un curso inactivo", ex.Message);
        Assert.Empty(_cohorteRepo.Items);
        Assert.Equal(0, _uow.CommitsRealizados);
    }

    [Fact]
    public async Task HandleAsync_ConComisionDuplicadaEnPeriodo_DebeLanzarDomainException()
    {
        // Arrange
        var institucionId = Guid.NewGuid();
        _tenant.InstitucionId = institucionId;
        var curso = new Curso(institucionId, "Sistemas Operativos", "Sistemas");
        await _cursoRepo.AddAsync(curso);

        var docenteId = Guid.NewGuid();
        await _cohorteRepo.AddAsync(new Cohorte(institucionId, curso.Id, docenteId, "Division A", "2026-1C"));

        var handler = new CrearCohorteCommandHandler(_cursoRepo, _cohorteRepo, _uow, _tenant);
        var command = new CrearCohorteCommand(curso.Id, "Division A", "2026-1C", docenteId);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<DomainException>(() => handler.HandleAsync(command));
        Assert.Contains("Ya existe una comision llamada", ex.Message);
        Assert.Equal(0, _uow.CommitsRealizados);
    }
}
