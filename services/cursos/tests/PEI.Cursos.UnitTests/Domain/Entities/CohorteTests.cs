using System;
using PEI.Cursos.Domain.Entities;
using PEI.Cursos.Domain.Exceptions;
using Xunit;

namespace PEI.Cursos.UnitTests.Domain.Entities;

/* Pruebas unitarias para la entidad rica Cohorte.
Verifica la integridad de claves foráneas, restricciones de longitud y reasignaciones de docentes. */
public class CohorteTests
{
    [Fact]
    public void Crear_ConDatosValidos_DebeInicializarCohorteActivaConUtc()
    {
        // Arrange
        var institucionId = Guid.NewGuid();
        var cursoId = Guid.NewGuid();
        var docenteId = Guid.NewGuid();
        var nombre = "Comisión A - Mañana";
        var periodo = "2026-1C";

        // Act
        var cohorte = new Cohorte(institucionId, cursoId, docenteId, nombre, periodo);

        // Assert
        Assert.NotEqual(Guid.Empty, cohorte.Id);
        Assert.Equal(institucionId, cohorte.InstitucionId);
        Assert.Equal(cursoId, cohorte.CursoId);
        Assert.Equal(docenteId, cohorte.DocenteId);
        Assert.Equal(nombre, cohorte.Nombre);
        Assert.Equal(periodo, cohorte.Periodo);
        Assert.True(cohorte.Activo);
        Assert.Equal(1, cohorte.Version);
        Assert.True(cohorte.FechaCreacionUtc <= DateTime.UtcNow);
        Assert.Null(cohorte.FechaModificacionUtc);
        Assert.NotNull(cohorte.Matriculaciones);
        Assert.Empty(cohorte.Matriculaciones);
    }

    [Fact]
    public void Crear_ConIdentificadoresVacios_DebeLanzarDomainException()
    {
        // Assert: Institucion vacía
        Assert.Throws<DomainException>(() =>
            new Cohorte(Guid.Empty, Guid.NewGuid(), Guid.NewGuid(), "Comision", "2026-1C"));

        // Assert: Curso vacío
        Assert.Throws<DomainException>(() =>
            new Cohorte(Guid.NewGuid(), Guid.Empty, Guid.NewGuid(), "Comision", "2026-1C"));

        // Assert: Docente vacío
        Assert.Throws<DomainException>(() =>
            new Cohorte(Guid.NewGuid(), Guid.NewGuid(), Guid.Empty, "Comision", "2026-1C"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Crear_ConNombreVacio_DebeLanzarDomainException(string? nombreInvalido)
    {
        // Act & Assert
        var ex = Assert.Throws<DomainException>(() =>
            new Cohorte(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), nombreInvalido!, "2026-1C"));

        Assert.Contains("El nombre de la cohorte es obligatorio", ex.Message);
    }

    [Fact]
    public void Crear_ConNombreMayorA100Caracteres_DebeLanzarDomainException()
    {
        // Arrange
        var nombreLargo = new string('A', 101);

        // Act & Assert
        var ex = Assert.Throws<DomainException>(() =>
            new Cohorte(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), nombreLargo, "2026-1C"));

        Assert.Contains("no puede superar los 100 caracteres", ex.Message);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Crear_ConPeriodoVacio_DebeLanzarDomainException(string? periodoInvalido)
    {
        // Act & Assert
        var ex = Assert.Throws<DomainException>(() =>
            new Cohorte(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Comision A", periodoInvalido!));

        Assert.Contains("El período lectivo es obligatorio", ex.Message);
    }

    [Fact]
    public void Crear_ConPeriodoMayorA30Caracteres_DebeLanzarDomainException()
    {
        // Arrange
        var periodoLargo = new string('P', 31);

        // Act & Assert
        var ex = Assert.Throws<DomainException>(() =>
            new Cohorte(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Comision A", periodoLargo));

        Assert.Contains("no puede superar los 30 caracteres", ex.Message);
    }

    [Fact]
    public void ActualizarDatos_EstandoActiva_DebeActualizarYModificarFechaUtc()
    {
        // Arrange
        var cohorte = new Cohorte(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Comisión Original", "2026-1C");

        // Act
        cohorte.ActualizarDatos("Comisión Modificada", "2026-2C");

        // Assert
        Assert.Equal("Comisión Modificada", cohorte.Nombre);
        Assert.Equal("2026-2C", cohorte.Periodo);
        Assert.NotNull(cohorte.FechaModificacionUtc);
    }

    [Fact]
    public void ReasignarDocente_EstandoActiva_DebeActualizarDocenteYFechaModificacion()
    {
        // Arrange
        var cohorte = new Cohorte(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Comision A", "2026-1C");
        var nuevoDocenteId = Guid.NewGuid();

        // Act
        cohorte.ReasignarDocente(nuevoDocenteId);

        // Assert
        Assert.Equal(nuevoDocenteId, cohorte.DocenteId);
        Assert.NotNull(cohorte.FechaModificacionUtc);
    }

    [Fact]
    public void ReasignarDocente_ConGuidVacio_DebeLanzarDomainException()
    {
        // Arrange
        var cohorte = new Cohorte(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Comision A", "2026-1C");

        // Act & Assert
        var ex = Assert.Throws<DomainException>(() => cohorte.ReasignarDocente(Guid.Empty));
        Assert.Contains("no puede ser un identificador vacío", ex.Message);
    }

    [Fact]
    public void Operaciones_EstandoInactiva_DebenLanzarDomainException()
    {
        // Arrange
        var cohorte = new Cohorte(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Comision A", "2026-1C");
        cohorte.Desactivar();

        // Assert: Modificar datos en inactiva
        Assert.Throws<DomainException>(() => cohorte.ActualizarDatos("Nuevo", "2026-2C"));

        // Assert: Reasignar docente en inactiva
        Assert.Throws<DomainException>(() => cohorte.ReasignarDocente(Guid.NewGuid()));
    }
}
