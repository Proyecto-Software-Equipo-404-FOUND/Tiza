using System;
using System.Threading.Tasks;
using PEI.Cursos.Application.UseCases.Cursos.Queries;
using PEI.Cursos.Domain.Entities;
using PEI.Cursos.UnitTests.Common;
using Xunit;

namespace PEI.Cursos.UnitTests.Application.Cursos;

/* Pruebas unitarias para GetCursoByIdQueryHandler.
Verifica la recuperacion individual de cursos y la emision de null para 404 Not Found. */
public class GetCursoByIdQueryHandlerTests
{
    private readonly FakeCursoRepository _repo = new();

    [Fact]
    public async Task HandleAsync_ConCursoExistente_DebeRetornarCursoDto()
    {
        // Arrange
        var institucionId = Guid.NewGuid();
        var curso = new Curso(institucionId, "Arquitectura de Software", "Sistemas");
        await _repo.AddAsync(curso);

        var handler = new GetCursoByIdQueryHandler(_repo);
        var query = new GetCursoByIdQuery(curso.Id);

        // Act
        var resultado = await handler.HandleAsync(query);

        // Assert
        Assert.NotNull(resultado);
        Assert.Equal(curso.Id, resultado.Id);
        Assert.Equal("Arquitectura de Software", resultado.Nombre);
        Assert.Equal("Sistemas", resultado.Area);
    }

    [Fact]
    public async Task HandleAsync_ConCursoInexistente_DebeRetornarNull()
    {
        // Arrange
        var handler = new GetCursoByIdQueryHandler(_repo);
        var query = new GetCursoByIdQuery(Guid.NewGuid());

        // Act
        var resultado = await handler.HandleAsync(query);

        // Assert
        Assert.Null(resultado);
    }

    [Fact]
    public async Task HandleAsync_ComoDocente_ConCursoPropio_DebeRetornarCursoDto()
    {
        // Arrange
        var institucionId = Guid.NewGuid();
        var docenteId = Guid.NewGuid();

        var curso = new Curso(institucionId, "Sistemas Operativos", "Sistemas");
        await _repo.AddAsync(curso);

        var cohorteRepo = new FakeCohorteRepository();
        var cohorte = new Cohorte(institucionId, curso.Id, docenteId, "Comisión SO", "2026-1C");
        await cohorteRepo.AddAsync(cohorte);

        var tenantContext = new FakeTenantContext
        {
            InstitucionId = institucionId,
            UsuarioId = docenteId,
            Rol = "Docente"
        };

        var handler = new GetCursoByIdQueryHandler(_repo, cohorteRepo, tenantContext);
        var query = new GetCursoByIdQuery(curso.Id);

        // Act
        var resultado = await handler.HandleAsync(query);

        // Assert
        Assert.NotNull(resultado);
        Assert.Equal(curso.Id, resultado.Id);
        Assert.Equal("Sistemas Operativos", resultado.Nombre);
    }

    [Fact]
    public async Task HandleAsync_ComoDocente_ConCursoAjeno_DebeRetornarNull_AntiEnumeracion404()
    {
        // Arrange
        var institucionId = Guid.NewGuid();
        var docenteSolicitanteId = Guid.NewGuid();
        var otroDocenteId = Guid.NewGuid();

        var cursoAjeno = new Curso(institucionId, "Probabilidad y Estadística", "Exactas");
        await _repo.AddAsync(cursoAjeno);

        var cohorteRepo = new FakeCohorteRepository();
        var cohorteAjena = new Cohorte(institucionId, cursoAjeno.Id, otroDocenteId, "Comisión PyE", "2026-1C");
        await cohorteRepo.AddAsync(cohorteAjena);

        var tenantContext = new FakeTenantContext
        {
            InstitucionId = institucionId,
            UsuarioId = docenteSolicitanteId,
            Rol = "Docente"
        };

        var handler = new GetCursoByIdQueryHandler(_repo, cohorteRepo, tenantContext);
        var query = new GetCursoByIdQuery(cursoAjeno.Id);

        // Act
        var resultado = await handler.HandleAsync(query);

        // Assert (Regla anti-enumeración: si no dicta ninguna cohorte en el curso, debe responder null -> 404)
        Assert.Null(resultado);
    }

    [Fact]
    public async Task HandleAsync_ComoAdministrador_ConCursoAjenoADocentes_DebeRetornarCursoDto()
    {
        // Arrange
        var institucionId = Guid.NewGuid();
        var adminId = Guid.NewGuid();
        var docenteId = Guid.NewGuid();

        var curso = new Curso(institucionId, "Ingeniería de Requerimientos", "Sistemas");
        await _repo.AddAsync(curso);

        var cohorteRepo = new FakeCohorteRepository();
        var cohorte = new Cohorte(institucionId, curso.Id, docenteId, "Comisión 1", "2026-1C");
        await cohorteRepo.AddAsync(cohorte);

        var tenantContext = new FakeTenantContext
        {
            InstitucionId = institucionId,
            UsuarioId = adminId,
            Rol = "Administrador"
        };

        var handler = new GetCursoByIdQueryHandler(_repo, cohorteRepo, tenantContext);
        var query = new GetCursoByIdQuery(curso.Id);

        // Act
        var resultado = await handler.HandleAsync(query);

        // Assert
        Assert.NotNull(resultado);
        Assert.Equal(curso.Id, resultado.Id);
    }
}
