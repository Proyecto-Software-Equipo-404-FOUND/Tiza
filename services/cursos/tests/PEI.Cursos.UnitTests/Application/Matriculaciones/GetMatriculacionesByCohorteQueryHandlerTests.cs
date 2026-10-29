using System;
using System.Threading.Tasks;
using PEI.Cursos.Application.UseCases.Matriculaciones.Queries;
using PEI.Cursos.Domain.Entities;
using PEI.Cursos.Domain.Exceptions;
using PEI.Cursos.UnitTests.Common;
using Xunit;

namespace PEI.Cursos.UnitTests.Application.Matriculaciones;

/* Pruebas unitarias para GetMatriculacionesByCohorteQueryHandler (Caso de Uso S2-CU-01).
Verifica la consulta de alumnos por cohorte, reglas de aislamiento, filtros de estado y manejo de excepciones. */
public class GetMatriculacionesByCohorteQueryHandlerTests
{
    private readonly FakeCohorteRepository _cohorteRepo = new();
    private readonly FakeMatriculacionRepository _matriculacionRepo = new();

    [Fact]
    public async Task HandleAsync_ConCohorteInexistente_DebeRetornarNull()
    {
        // Arrange
        var handler = new GetMatriculacionesByCohorteQueryHandler(_cohorteRepo, _matriculacionRepo);
        var query = new GetMatriculacionesByCohorteQuery(Guid.NewGuid());

        // Act
        var resultado = await handler.HandleAsync(query);

        // Assert
        Assert.Null(resultado);
    }

    [Fact]
    public async Task HandleAsync_ConCohorteInactiva_DebeRetornarNull()
    {
        // Arrange
        var cohorte = new Cohorte(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Comisión Inactiva",
            "2026-2C"
        );
        cohorte.Desactivar();
        await _cohorteRepo.AddAsync(cohorte);

        var handler = new GetMatriculacionesByCohorteQueryHandler(_cohorteRepo, _matriculacionRepo);
        var query = new GetMatriculacionesByCohorteQuery(cohorte.Id);

        // Act
        var resultado = await handler.HandleAsync(query);

        // Assert
        Assert.Null(resultado);
    }

    [Fact]
    public async Task HandleAsync_ConCohorteValidaSinMatriculaciones_DebeRetornarListaVacia()
    {
        // Arrange
        var cohorte = new Cohorte(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            "1° A - Turno Noche",
            "2026-2C"
        );
        await _cohorteRepo.AddAsync(cohorte);

        var handler = new GetMatriculacionesByCohorteQueryHandler(_cohorteRepo, _matriculacionRepo);
        var query = new GetMatriculacionesByCohorteQuery(cohorte.Id);

        // Act
        var resultado = await handler.HandleAsync(query);

        // Assert
        Assert.NotNull(resultado);
        Assert.Empty(resultado);
    }

    [Fact]
    public async Task HandleAsync_ConCohorteYMatriculaciones_DebeRetornarListaDeMatriculacionDto()
    {
        // Arrange
        var institucionId = Guid.NewGuid();
        var cohorte = new Cohorte(
            institucionId,
            Guid.NewGuid(),
            Guid.NewGuid(),
            "3° B - Turno Mañana",
            "2026-2C"
        );
        await _cohorteRepo.AddAsync(cohorte);

        var mat1 = new Matriculacion(institucionId, cohorte.Id, Guid.NewGuid());
        var mat2 = new Matriculacion(institucionId, cohorte.Id, Guid.NewGuid());
        await _matriculacionRepo.AddAsync(mat1);
        await _matriculacionRepo.AddAsync(mat2);

        var handler = new GetMatriculacionesByCohorteQueryHandler(_cohorteRepo, _matriculacionRepo);
        var query = new GetMatriculacionesByCohorteQuery(cohorte.Id);

        // Act
        var resultado = await handler.HandleAsync(query);

        // Assert
        Assert.NotNull(resultado);
        Assert.Equal(2, resultado.Count);
        Assert.Contains(resultado, m => m.Id == mat1.Id && m.AlumnoId == mat1.AlumnoId);
        Assert.Contains(resultado, m => m.Id == mat2.Id && m.AlumnoId == mat2.AlumnoId);
    }

    [Fact]
    public async Task HandleAsync_ConFiltroSoloActivas_DebeRetornarUnicamenteMatriculacionesActivas()
    {
        // Arrange
        var institucionId = Guid.NewGuid();
        var cohorte = new Cohorte(
            institucionId,
            Guid.NewGuid(),
            Guid.NewGuid(),
            "2° C",
            "2026-2C"
        );
        await _cohorteRepo.AddAsync(cohorte);

        var matActiva = new Matriculacion(institucionId, cohorte.Id, Guid.NewGuid());
        var matBaja = new Matriculacion(institucionId, cohorte.Id, Guid.NewGuid());
        matBaja.DarDeBaja();

        await _matriculacionRepo.AddAsync(matActiva);
        await _matriculacionRepo.AddAsync(matBaja);

        var handler = new GetMatriculacionesByCohorteQueryHandler(_cohorteRepo, _matriculacionRepo);

        // Act: Con soloActivas = true (por defecto)
        var resultadoActivas = await handler.HandleAsync(new GetMatriculacionesByCohorteQuery(cohorte.Id, SoloActivas: true));

        // Act: Con soloActivas = false (historial completo)
        var resultadoTodas = await handler.HandleAsync(new GetMatriculacionesByCohorteQuery(cohorte.Id, SoloActivas: false));

        // Assert
        Assert.NotNull(resultadoActivas);
        Assert.Single(resultadoActivas);
        Assert.Equal(matActiva.Id, resultadoActivas[0].Id);

        Assert.NotNull(resultadoTodas);
        Assert.Equal(2, resultadoTodas.Count);
    }

    [Fact]
    public async Task HandleAsync_ConCohorteIdVacio_DebeLanzarDomainException()
    {
        // Arrange
        var handler = new GetMatriculacionesByCohorteQueryHandler(_cohorteRepo, _matriculacionRepo);
        var query = new GetMatriculacionesByCohorteQuery(Guid.Empty);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<DomainException>(() => handler.HandleAsync(query));
        Assert.Contains("obligatorio", ex.Message, StringComparison.OrdinalIgnoreCase);
    }
}
