using System;
using System.Threading.Tasks;
using PEI.Cursos.Application.UseCases.Matriculaciones.Queries;
using PEI.Cursos.Domain.Entities;
using PEI.Cursos.UnitTests.Common;
using Xunit;

namespace PEI.Cursos.UnitTests.Application.Matriculaciones;

/* Pruebas unitarias para GetMatriculacionByIdQueryHandler.
Verifica la recuperacion individual de inscripciones respaldando la cabecera Location. */
public class GetMatriculacionByIdQueryHandlerTests
{
    private readonly FakeMatriculacionRepository _matriculacionRepo = new();

    [Fact]
    public async Task HandleAsync_ConIdExistente_DebeRetornarMatriculacionDto()
    {
        // Arrange
        var institucionId = Guid.NewGuid();
        var cohorteId = Guid.NewGuid();
        var alumnoId = Guid.NewGuid();
        var matriculacion = new Matriculacion(institucionId, cohorteId, alumnoId);
        await _matriculacionRepo.AddAsync(matriculacion);

        var handler = new GetMatriculacionByIdQueryHandler(_matriculacionRepo);
        var query = new GetMatriculacionByIdQuery(matriculacion.Id);

        // Act
        var resultado = await handler.HandleAsync(query);

        // Assert
        Assert.NotNull(resultado);
        Assert.Equal(matriculacion.Id, resultado.Id);
        Assert.Equal(institucionId, resultado.InstitucionId);
        Assert.Equal(cohorteId, resultado.CohorteId);
        Assert.Equal(alumnoId, resultado.AlumnoId);
        Assert.Equal("Activa", resultado.Estado);
    }

    [Fact]
    public async Task HandleAsync_ConIdInexistente_DebeRetornarNull()
    {
        // Arrange
        var handler = new GetMatriculacionByIdQueryHandler(_matriculacionRepo);
        var query = new GetMatriculacionByIdQuery(Guid.NewGuid());

        // Act
        var resultado = await handler.HandleAsync(query);

        // Assert
        Assert.Null(resultado);
    }
}
