using System;
using PEI.Cursos.Domain.Entities;
using PEI.Cursos.Domain.Exceptions;
using Xunit;

namespace PEI.Cursos.UnitTests.Domain.Entities;

/* Pruebas unitarias para la entidad rica Curso.
Verifica la integridad de invariantes, validaciones defensivas y transiciones de ciclo de vida en memoria. */
public class CursoTests
{
    [Fact]
    public void Crear_ConDatosValidos_DebeInicializarCursoActivoConUtc()
    {
        // Arrange
        var institucionId = Guid.NewGuid();
        var nombre = "Matemática Discreta";
        var area = "Ciencias Exactas";

        // Act
        var curso = new Curso(institucionId, nombre, area);

        // Assert
        Assert.NotEqual(Guid.Empty, curso.Id);
        Assert.Equal(institucionId, curso.InstitucionId);
        Assert.Equal(nombre, curso.Nombre);
        Assert.Equal(area, curso.Area);
        Assert.True(curso.Activo);
        Assert.Equal(1, curso.Version);
        Assert.True(curso.FechaCreacionUtc <= DateTime.UtcNow);
        Assert.Null(curso.FechaModificacionUtc);
        Assert.NotNull(curso.Cohortes);
        Assert.Empty(curso.Cohortes);
    }

    [Fact]
    public void Crear_ConInstitucionVacia_DebeLanzarDomainException()
    {
        // Act & Assert
        var ex = Assert.Throws<DomainException>(() =>
            new Curso(Guid.Empty, "Matemática", "Exactas"));

        Assert.Contains("La institución es obligatoria", ex.Message);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Crear_ConNombreVacio_DebeLanzarDomainException(string? nombreInvalido)
    {
        // Act & Assert
        var ex = Assert.Throws<DomainException>(() =>
            new Curso(Guid.NewGuid(), nombreInvalido!, "Exactas"));

        Assert.Contains("El nombre del curso es obligatorio", ex.Message);
    }

    [Fact]
    public void Crear_ConNombreMayorA150Caracteres_DebeLanzarDomainException()
    {
        // Arrange
        var nombreLargo = new string('A', 151);

        // Act & Assert
        var ex = Assert.Throws<DomainException>(() =>
            new Curso(Guid.NewGuid(), nombreLargo, "Exactas"));

        Assert.Contains("no puede superar los 150 caracteres", ex.Message);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Crear_ConAreaVacia_DebeLanzarDomainException(string? areaInvalida)
    {
        // Act & Assert
        var ex = Assert.Throws<DomainException>(() =>
            new Curso(Guid.NewGuid(), "Matemática", areaInvalida!));

        Assert.Contains("El área temática del curso es obligatoria", ex.Message);
    }

    [Fact]
    public void Crear_ConAreaMayorA50Caracteres_DebeLanzarDomainException()
    {
        // Arrange
        var areaLarga = new string('B', 51);

        // Act & Assert
        var ex = Assert.Throws<DomainException>(() =>
            new Curso(Guid.NewGuid(), "Matemática", areaLarga));

        Assert.Contains("no puede superar los 50 caracteres", ex.Message);
    }

    [Fact]
    public void ActualizarDatos_EstandoActivo_DebeActualizarYModificarFechaUtc()
    {
        // Arrange
        var curso = new Curso(Guid.NewGuid(), "Nombre Original", "Area Original");

        // Act
        curso.ActualizarDatos("Nombre Actualizado", "Area Actualizada");

        // Assert
        Assert.Equal("Nombre Actualizado", curso.Nombre);
        Assert.Equal("Area Actualizada", curso.Area);
        Assert.NotNull(curso.FechaModificacionUtc);
    }

    [Fact]
    public void ActualizarDatos_EstandoInactivo_DebeLanzarDomainException()
    {
        // Arrange
        var curso = new Curso(Guid.NewGuid(), "Nombre Original", "Area Original");
        curso.Desactivar();

        // Act & Assert
        var ex = Assert.Throws<DomainException>(() =>
            curso.ActualizarDatos("Nuevo Nombre", "Nueva Area"));

        Assert.Contains("No es posible modificar los datos de un curso inactivo", ex.Message);
    }

    [Fact]
    public void Desactivar_Y_Activar_DebenGestionarCicloDeVidaCorrectamente()
    {
        // Arrange
        var curso = new Curso(Guid.NewGuid(), "Nombre", "Area");

        // Act 1: Desactivar
        curso.Desactivar();

        // Assert 1
        Assert.False(curso.Activo);

        // Assert 2: No se puede desactivar si ya está inactivo
        Assert.Throws<DomainException>(() => curso.Desactivar());

        // Act 2: Reactivar
        curso.Activar();

        // Assert 3
        Assert.True(curso.Activo);

        // Assert 4: No se puede activar si ya está activo
        Assert.Throws<DomainException>(() => curso.Activar());
    }
}
