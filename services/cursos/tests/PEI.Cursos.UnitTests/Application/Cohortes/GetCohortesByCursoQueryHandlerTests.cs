using System;
using System.Threading.Tasks;
using PEI.Cursos.Application.UseCases.Cohortes.Queries;
using PEI.Cursos.Domain.Entities;
using PEI.Cursos.UnitTests.Common;
using Xunit;

namespace PEI.Cursos.UnitTests.Application.Cohortes;

/* Pruebas unitarias para GetCohortesByCursoQueryHandler.
Verifica la consulta de comisiones pertenecientes a un curso y la emision de null para 404. */
public class GetCohortesByCursoQueryHandlerTests
{
    private readonly FakeCursoRepository _cursoRepo = new();
    private readonly FakeCohorteRepository _cohorteRepo = new();

    [Fact]
    public async Task HandleAsync_ConCursoExistente_DebeRetornarListaDeCohortes()
    {
        // Arrange
        var institucionId = Guid.NewGuid();
        var curso = new Curso(institucionId, "Bases de Datos", "Sistemas");
        await _cursoRepo.AddAsync(curso);

        var docenteId = Guid.NewGuid();
        await _cohorteRepo.AddAsync(new Cohorte(institucionId, curso.Id, docenteId, "Comision 1", "2026-1C"));
        await _cohorteRepo.AddAsync(new Cohorte(institucionId, curso.Id, docenteId, "Comision 2", "2026-1C"));

        var handler = new GetCohortesByCursoQueryHandler(_cursoRepo, _cohorteRepo);
        var query = new GetCohortesByCursoQuery(curso.Id);

        // Act
        var resultado = await handler.HandleAsync(query);

        // Assert
        Assert.NotNull(resultado);
        Assert.Equal(2, resultado.Count);
        Assert.Contains(resultado, c => c.Nombre == "Comision 1");
        Assert.Contains(resultado, c => c.Nombre == "Comision 2");
    }

    [Fact]
    public async Task HandleAsync_ConCursoInexistente_DebeRetornarNull()
    {
        // Arrange
        var handler = new GetCohortesByCursoQueryHandler(_cursoRepo, _cohorteRepo);
        var query = new GetCohortesByCursoQuery(Guid.NewGuid());

        // Act
        var resultado = await handler.HandleAsync(query);

        // Assert
        Assert.Null(resultado);
    }
}
