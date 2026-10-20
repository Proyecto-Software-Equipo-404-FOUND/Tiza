using PEI.IA.Domain.Entities;
using PEI.IA.McpServer.Tools;

namespace PEI.IA.UnitTests;

public class RegistrarCalificacionToolTests
{
    public RegistrarCalificacionToolTests()
    {
        RegistrarCalificacionSugeridaTool.Limpiar();
    }

    [Fact]
    public void RegistrarCalificacion_ValoresValidos_RegistraEnMemoriaYRetornaExito()
    {
        // Arrange
        var nota = 8.5m;
        var justificacion = "El alumno comprende la abstracción y provee ejemplos correctos.";
        var desglose = "[{\"criterio\":\"Definición\",\"puntajeMaximo\":5,\"puntajeOtorgado\":4.5}]";

        // Act
        var result = RegistrarCalificacionSugeridaTool.RegistrarCalificacion(nota, justificacion, desglose);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Exito);
        Assert.Equal(8.5m, result.NotaSugerida);
        Assert.Null(result.Error);

        var registros = RegistrarCalificacionSugeridaTool.ObtenerRegistros();
        Assert.Single(registros);

        var calificacion = registros.Values.First();
        Assert.Equal(8.5m, calificacion.NotaSugerida);
        Assert.Equal(justificacion, calificacion.Justificacion);
        Assert.Equal(desglose, calificacion.DesglosePorCriterioJson);
        Assert.NotEqual(Guid.Empty, calificacion.Id);
    }

    [Fact]
    public void RegistrarCalificacion_NotaNegativa_RetornaErrorSinGuardar()
    {
        // Act
        var result = RegistrarCalificacionSugeridaTool.RegistrarCalificacion(-1m, "Justificación");

        // Assert
        Assert.False(result.Exito);
        Assert.NotNull(result.Error);
        Assert.Contains("negativa", result.Error);
        Assert.Empty(RegistrarCalificacionSugeridaTool.ObtenerRegistros());
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void RegistrarCalificacion_JustificacionVacia_RetornaErrorSinGuardar(string? justificacionInvalida)
    {
        // Act
        var result = RegistrarCalificacionSugeridaTool.RegistrarCalificacion(7m, justificacionInvalida!);

        // Assert
        Assert.False(result.Exito);
        Assert.NotNull(result.Error);
        Assert.Contains("obligatoria", result.Error);
        Assert.Empty(RegistrarCalificacionSugeridaTool.ObtenerRegistros());
    }

    [Fact]
    public void CalificacionSugerida_Entidad_InicializaIdYFechaUtc()
    {
        var entidad = new CalificacionSugerida
        {
            NotaSugerida = 10,
            Justificacion = "Perfecto"
        };

        Assert.NotEqual(Guid.Empty, entidad.Id);
        Assert.True((DateTime.UtcNow - entidad.FechaRegistroUtc).TotalSeconds < 5);
    }
}
