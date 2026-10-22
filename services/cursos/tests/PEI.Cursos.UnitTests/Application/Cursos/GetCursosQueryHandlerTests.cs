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
}
