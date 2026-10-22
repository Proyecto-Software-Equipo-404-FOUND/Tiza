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
}
