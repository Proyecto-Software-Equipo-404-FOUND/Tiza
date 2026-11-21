using System;
using System.Threading.Tasks;
using PEI.Cursos.Application.UseCases.Cursos.Queries;
using PEI.Cursos.Domain.Entities;
using PEI.Cursos.UnitTests.Common;
using Xunit;

namespace PEI.Cursos.UnitTests.Application.Cursos;

/* Pruebas unitarias para GetCursosQueryHandler.
Verifica la proyeccion inmutable y los calculos de navegacion paginada. */
public class GetCursosQueryHandlerTests
{
    private readonly FakeCursoRepository _repo = new();

    [Fact]
    public async Task HandleAsync_ConCursosExistentes_DebeCalcularPaginacionYProyectar()
    {
        // Arrange
        var institucionId = Guid.NewGuid();
        for (int i = 1; i <= 25; i++)
        {
            await _repo.AddAsync(new Curso(institucionId, $"Materia {i:D2}", "Exactas"));
        }

        var handler = new GetCursosQueryHandler(_repo);
        var query = new GetCursosQuery(Area: null, Busqueda: null, Pagina: 1, TamanoPagina: 10);

        // Act
        var resultado = await handler.HandleAsync(query);

        // Assert
        Assert.NotNull(resultado);
        Assert.Equal(25, resultado.TotalItems);
        Assert.Equal(1, resultado.Pagina);
        Assert.Equal(10, resultado.TamanoPagina);
        Assert.Equal(3, resultado.TotalPaginas);
        Assert.False(resultado.TienePaginaPrevia);
        Assert.True(resultado.TienePaginaSiguiente);
        Assert.Equal(10, resultado.Items.Count);
    }

    [Fact]
    public async Task HandleAsync_SinCoincidencias_DebeRetornarColeccionVaciaYPaginacionCero()
    {
        // Arrange
        var handler = new GetCursosQueryHandler(_repo);
        var query = new GetCursosQuery(Area: "Inexistente", Busqueda: "XYZ", Pagina: 1, TamanoPagina: 10);

        // Act
        var resultado = await handler.HandleAsync(query);

        // Assert
        Assert.NotNull(resultado);
        Assert.Empty(resultado.Items);
        Assert.Equal(0, resultado.TotalItems);
        Assert.Equal(0, resultado.TotalPaginas);
        Assert.False(resultado.TienePaginaPrevia);
        Assert.False(resultado.TienePaginaSiguiente);
    }

    [Fact]
    public async Task HandleAsync_ComoDocente_FiltraCursosDondeDictaComision()
    {
        // Arrange
        var institucionId = Guid.NewGuid();
        var docenteAId = Guid.NewGuid();
        var docenteBId = Guid.NewGuid();

        var cursoDictado = new Curso(institucionId, "Paradigmas de Programación", "Sistemas");
        var cohorteDocenteA = new Cohorte(institucionId, cursoDictado.Id, docenteAId, "Comisión A", "2026-1C");
        cursoDictado.Cohortes.Add(cohorteDocenteA);
        await _repo.AddAsync(cursoDictado);

        var cursoAjeno = new Curso(institucionId, "Química Aplicada", "Química");
        var cohorteDocenteB = new Cohorte(institucionId, cursoAjeno.Id, docenteBId, "Comisión B", "2026-1C");
        cursoAjeno.Cohortes.Add(cohorteDocenteB);
        await _repo.AddAsync(cursoAjeno);

        var tenantContext = new FakeTenantContext
        {
            InstitucionId = institucionId,
            UsuarioId = docenteAId,
            Rol = "Docente"
        };

        var handler = new GetCursosQueryHandler(_repo, tenantContext);
        var query = new GetCursosQuery(Area: null, Busqueda: null, Pagina: 1, TamanoPagina: 10);

        // Act
        var resultado = await handler.HandleAsync(query);

        // Assert
        Assert.NotNull(resultado);
        Assert.Equal(1, resultado.TotalItems);
        var item = Assert.Single(resultado.Items);
        Assert.Equal(cursoDictado.Id, item.Id);
        Assert.Equal("Paradigmas de Programación", item.Nombre);
    }

    [Fact]
    public async Task HandleAsync_ComoDocenteSinCursosAsignados_RetornaColeccionVacia()
    {
        // Arrange
        var institucionId = Guid.NewGuid();
        var docenteId = Guid.NewGuid();
        var otroDocenteId = Guid.NewGuid();

        var cursoExistente = new Curso(institucionId, "Física 1", "Exactas");
        var cohorte = new Cohorte(institucionId, cursoExistente.Id, otroDocenteId, "Comisión 1", "2026-1C");
        cursoExistente.Cohortes.Add(cohorte);
        await _repo.AddAsync(cursoExistente);

        var tenantContext = new FakeTenantContext
        {
            InstitucionId = institucionId,
            UsuarioId = docenteId,
            Rol = "Docente"
        };

        var handler = new GetCursosQueryHandler(_repo, tenantContext);
        var query = new GetCursosQuery(Area: null, Busqueda: null, Pagina: 1, TamanoPagina: 10);

        // Act
        var resultado = await handler.HandleAsync(query);

        // Assert
        Assert.NotNull(resultado);
        Assert.Empty(resultado.Items);
        Assert.Equal(0, resultado.TotalItems);
    }

    [Fact]
    public async Task HandleAsync_ComoAdministrador_RetornaTodosLosCursosDeLaInstitucion()
    {
        // Arrange
        var institucionId = Guid.NewGuid();
        var adminId = Guid.NewGuid();
        var docenteId = Guid.NewGuid();

        var curso1 = new Curso(institucionId, "Bases de Datos 1", "Sistemas");
        curso1.Cohortes.Add(new Cohorte(institucionId, curso1.Id, docenteId, "Comisión 1", "2026-1C"));
        await _repo.AddAsync(curso1);

        var curso2 = new Curso(institucionId, "Redes de Datos", "Sistemas");
        await _repo.AddAsync(curso2);

        var tenantContext = new FakeTenantContext
        {
            InstitucionId = institucionId,
            UsuarioId = adminId,
            Rol = "Administrador"
        };

        var handler = new GetCursosQueryHandler(_repo, tenantContext);
        var query = new GetCursosQuery(Area: null, Busqueda: null, Pagina: 1, TamanoPagina: 10);

        // Act
        var resultado = await handler.HandleAsync(query);

        // Assert
        Assert.NotNull(resultado);
        Assert.Equal(2, resultado.TotalItems);
        Assert.Equal(2, resultado.Items.Count);
    }
}
