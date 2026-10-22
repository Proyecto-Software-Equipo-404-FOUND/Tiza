using System;
using System.Threading.Tasks;
using PEI.Cursos.Application.UseCases.Cohortes.Queries;
using PEI.Cursos.Domain.Entities;
using PEI.Cursos.UnitTests.Common;
using Xunit;

namespace PEI.Cursos.UnitTests.Application.Cohortes;

/* Pruebas unitarias para ValidarCohorteQueryHandler (CU-05).
Verifica los 4 escenarios de validacion de existencia, estado y pertenencia institucional. */
public class ValidarCohorteQueryHandlerTests
{
    private readonly FakeCohorteRepository _repo = new();

    [Fact]
    public async Task HandleAsync_ConCohorteExistenteYActiva_DebeRetornarDtoConActivaTrue()
    {
        // Arrange
        var institucionId = Guid.NewGuid();
        var cursoId = Guid.NewGuid();
        var docenteId = Guid.NewGuid();
        var cohorte = new Cohorte(institucionId, cursoId, docenteId, "Comision 1A", "2026-2C");
        await _repo.AddAsync(cohorte);

        var handler = new ValidarCohorteQueryHandler(_repo);
        var query = new ValidarCohorteQuery(cohorte.Id);

        // Act
        var resultado = await handler.HandleAsync(query);

        // Assert
        Assert.NotNull(resultado);
        Assert.Equal(cohorte.Id, resultado.CohorteId);
        Assert.Equal(institucionId, resultado.InstitucionId);
        Assert.Equal(cursoId, resultado.CursoId);
        Assert.Equal(docenteId, resultado.DocenteId);
        Assert.Equal("Comision 1A", resultado.Nombre);
        Assert.True(resultado.Activa);
    }

    [Fact]
    public async Task HandleAsync_ConCohorteExistentePeroInactiva_DebeRetornarDtoConActivaFalse()
    {
        // Arrange
        var institucionId = Guid.NewGuid();
        var cursoId = Guid.NewGuid();
        var docenteId = Guid.NewGuid();
        var cohorte = new Cohorte(institucionId, cursoId, docenteId, "Comision 1B", "2026-2C");
        cohorte.Desactivar();
        await _repo.AddAsync(cohorte);

        var handler = new ValidarCohorteQueryHandler(_repo);
        var query = new ValidarCohorteQuery(cohorte.Id);

        // Act
        var resultado = await handler.HandleAsync(query);

        // Assert
        Assert.NotNull(resultado);
        Assert.Equal(cohorte.Id, resultado.CohorteId);
        Assert.Equal("Comision 1B", resultado.Nombre);
        Assert.False(resultado.Activa);
    }

    [Fact]
    public async Task HandleAsync_ConCohorteInexistente_DebeRetornarNull()
    {
        // Arrange
        var handler = new ValidarCohorteQueryHandler(_repo);
        var query = new ValidarCohorteQuery(Guid.NewGuid());

        // Act
        var resultado = await handler.HandleAsync(query);

        // Assert
        Assert.Null(resultado);
    }

    [Fact]
    public async Task HandleAsync_ConCohorteIdVacio_DebeRetornarNull()
    {
        // Arrange
        var handler = new ValidarCohorteQueryHandler(_repo);
        var query = new ValidarCohorteQuery(Guid.Empty);

        // Act
        var resultado = await handler.HandleAsync(query);

        // Assert
        Assert.Null(resultado);
    }
}
