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

    [Fact]
    public async Task HandleAsync_ComoDocente_ConCursoPropio_DebeActualizarYConfirmar()
    {
        // Arrange
        var institucionId = Guid.NewGuid();
        var docenteId = Guid.NewGuid();

        var curso = new Curso(institucionId, "Física 2", "Exactas");
        await _repo.AddAsync(curso);

        var cohorteRepo = new FakeCohorteRepository();
        var cohorte = new Cohorte(institucionId, curso.Id, docenteId, "Comisión F2", "2026-1C");
        await cohorteRepo.AddAsync(cohorte);

        var tenantContext = new FakeTenantContext
        {
            InstitucionId = institucionId,
            UsuarioId = docenteId,
            Rol = "Docente"
        };

        var handler = new ActualizarCursoCommandHandler(_repo, _uow, cohorteRepo, tenantContext);
        var command = new ActualizarCursoCommand(curso.Id, "Física Teórica 2", "Física");

        // Act
        var resultado = await handler.HandleAsync(command);

        // Assert
        Assert.NotNull(resultado);
        Assert.Equal("Física Teórica 2", resultado.Nombre);
        Assert.Equal("Física", resultado.Area);
        Assert.Equal(1, _uow.CommitsRealizados);
    }

    [Fact]
    public async Task HandleAsync_ComoDocente_ConCursoAjeno_DebeRetornarNull_AntiEnumeracion404()
    {
        // Arrange
        var institucionId = Guid.NewGuid();
        var docenteSolicitanteId = Guid.NewGuid();
        var otroDocenteId = Guid.NewGuid();

        var cursoAjeno = new Curso(institucionId, "Diseño Gráfico", "Diseño");
        await _repo.AddAsync(cursoAjeno);

        var cohorteRepo = new FakeCohorteRepository();
        var cohorte = new Cohorte(institucionId, cursoAjeno.Id, otroDocenteId, "Comisión DG", "2026-1C");
        await cohorteRepo.AddAsync(cohorte);

        var tenantContext = new FakeTenantContext
        {
            InstitucionId = institucionId,
            UsuarioId = docenteSolicitanteId,
            Rol = "Docente"
        };

        var handler = new ActualizarCursoCommandHandler(_repo, _uow, cohorteRepo, tenantContext);
        var command = new ActualizarCursoCommand(cursoAjeno.Id, "Diseño Multimedial", "Diseño");

        // Act
        var resultado = await handler.HandleAsync(command);

        // Assert (Regla anti-enumeración: si no es su curso, debe responder null -> 404 sin confirmar UoW)
        Assert.Null(resultado);
        Assert.Equal(0, _uow.CommitsRealizados);
    }

    [Fact]
    public async Task HandleAsync_ComoAdministrador_ConCualquierCurso_DebeActualizarYConfirmar()
    {
        // Arrange
        var institucionId = Guid.NewGuid();
        var adminId = Guid.NewGuid();
        var docenteId = Guid.NewGuid();

        var curso = new Curso(institucionId, "Gestión de Proyectos", "Gestión");
        await _repo.AddAsync(curso);

        var cohorteRepo = new FakeCohorteRepository();
        var cohorte = new Cohorte(institucionId, curso.Id, docenteId, "Comisión GP", "2026-1C");
        await cohorteRepo.AddAsync(cohorte);

        var tenantContext = new FakeTenantContext
        {
            InstitucionId = institucionId,
            UsuarioId = adminId,
            Rol = "Administrador"
        };

        var handler = new ActualizarCursoCommandHandler(_repo, _uow, cohorteRepo, tenantContext);
        var command = new ActualizarCursoCommand(curso.Id, "Dirección y Gestión de Proyectos", "Gestión");

        // Act
        var resultado = await handler.HandleAsync(command);

        // Assert
        Assert.NotNull(resultado);
        Assert.Equal("Dirección y Gestión de Proyectos", resultado.Nombre);
        Assert.Equal(1, _uow.CommitsRealizados);
    }
}
