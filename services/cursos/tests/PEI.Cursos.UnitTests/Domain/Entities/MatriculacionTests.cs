using System;
using PEI.Cursos.Domain.Entities;
using PEI.Cursos.Domain.Enums;
using PEI.Cursos.Domain.Exceptions;
using Xunit;

namespace PEI.Cursos.UnitTests.Domain.Entities;

/* Pruebas unitarias para la entidad rica Matriculacion.
Verifica las transiciones válidas de la máquina de estados y las invariantes de negocio. */
public class MatriculacionTests
{
    [Fact]
    public void Crear_ConDatosValidos_DebeInicializarEnEstadoActivaConFechaAltaUtc()
    {
        // Arrange
        var institucionId = Guid.NewGuid();
        var cohorteId = Guid.NewGuid();
        var alumnoId = Guid.NewGuid();

        // Act
        var matriculacion = new Matriculacion(institucionId, cohorteId, alumnoId);

        // Assert
        Assert.NotEqual(Guid.Empty, matriculacion.Id);
        Assert.Equal(institucionId, matriculacion.InstitucionId);
        Assert.Equal(cohorteId, matriculacion.CohorteId);
        Assert.Equal(alumnoId, matriculacion.AlumnoId);
        Assert.Equal(EstadoMatriculacion.Activa, matriculacion.Estado);
        Assert.True(matriculacion.FechaAlta <= DateTime.UtcNow);
        Assert.Equal(1, matriculacion.Version);
        Assert.Null(matriculacion.FechaModificacionUtc);
    }

    [Fact]
    public void Crear_ConIdentificadoresVacios_DebeLanzarDomainException()
    {
        // Assert: Institucion vacía
        Assert.Throws<DomainException>(() =>
            new Matriculacion(Guid.Empty, Guid.NewGuid(), Guid.NewGuid()));

        // Assert: Cohorte vacía
        Assert.Throws<DomainException>(() =>
            new Matriculacion(Guid.NewGuid(), Guid.Empty, Guid.NewGuid()));

        // Assert: Alumno vacío
        Assert.Throws<DomainException>(() =>
            new Matriculacion(Guid.NewGuid(), Guid.NewGuid(), Guid.Empty));
    }

    [Fact]
    public void DarDeBaja_DesdeEstadoActiva_DebeCambiarEstadoABajaYActualizarFechaUtc()
    {
        // Arrange
        var matriculacion = new Matriculacion(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());

        // Act
        matriculacion.DarDeBaja();

        // Assert
        Assert.Equal(EstadoMatriculacion.Baja, matriculacion.Estado);
        Assert.NotNull(matriculacion.FechaModificacionUtc);
    }

    [Fact]
    public void DarDeBaja_SiYaEstaEnBaja_DebeLanzarDomainException()
    {
        // Arrange
        var matriculacion = new Matriculacion(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        matriculacion.DarDeBaja();

        // Act & Assert
        var ex = Assert.Throws<DomainException>(() => matriculacion.DarDeBaja());
        Assert.Contains("ya se encuentra dada de baja", ex.Message);
    }

    [Fact]
    public void DarDeBaja_SobreMatriculacionCompletada_DebeLanzarDomainException()
    {
        // Arrange: Alumno que ya aprobó/completó la cursada
        var matriculacion = new Matriculacion(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        matriculacion.Completar();

        // Act & Assert: No se puede dar de baja una cursada completada
        var ex = Assert.Throws<DomainException>(() => matriculacion.DarDeBaja());
        Assert.Contains("ya ha sido completada", ex.Message);
    }

    [Fact]
    public void Completar_DesdeEstadoActiva_DebeCambiarEstadoACompletadaYActualizarFechaUtc()
    {
        // Arrange
        var matriculacion = new Matriculacion(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());

        // Act
        matriculacion.Completar();

        // Assert
        Assert.Equal(EstadoMatriculacion.Completada, matriculacion.Estado);
        Assert.NotNull(matriculacion.FechaModificacionUtc);
    }

    [Fact]
    public void Completar_SobreMatriculacionEnBaja_DebeLanzarDomainException()
    {
        // Arrange: Alumno dado de baja
        var matriculacion = new Matriculacion(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        matriculacion.DarDeBaja();

        // Act & Assert: No se puede completar si está en baja
        var ex = Assert.Throws<DomainException>(() => matriculacion.Completar());
        Assert.Contains("Solo se pueden marcar como completadas las matriculaciones en estado activo", ex.Message);
    }

    [Fact]
    public void Reactivar_DesdeEstadoBaja_DebeRestablecerEstadoActivaYActualizarFechaUtc()
    {
        // Arrange
        var matriculacion = new Matriculacion(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        matriculacion.DarDeBaja();

        // Act
        matriculacion.Reactivar();

        // Assert
        Assert.Equal(EstadoMatriculacion.Activa, matriculacion.Estado);
        Assert.NotNull(matriculacion.FechaModificacionUtc);
    }

    [Fact]
    public void Reactivar_SobreMatriculacionActivaOCompletada_DebeLanzarDomainException()
    {
        // Arrange 1: Activa
        var matriculacionActiva = new Matriculacion(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        Assert.Throws<DomainException>(() => matriculacionActiva.Reactivar());

        // Arrange 2: Completada
        var matriculacionCompletada = new Matriculacion(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        matriculacionCompletada.Completar();
        Assert.Throws<DomainException>(() => matriculacionCompletada.Reactivar());
    }
}
