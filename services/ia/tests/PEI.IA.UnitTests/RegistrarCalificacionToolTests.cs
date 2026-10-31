using PEI.IA.Domain.Entities;
using PEI.IA.McpServer.Tools;

namespace PEI.IA.UnitTests;

public class RegistrarCalificacionToolTests
{
    // EntregaId y puntajeMaximo usados en todos los tests que necesitan valores válidos
    private static readonly Guid EntregaIdValido = Guid.NewGuid();
    private const decimal PuntajeMaximoValido = 10m;

    public RegistrarCalificacionToolTests()
    {
        RegistrarCalificacionSugeridaTool.Limpiar();
    }

    // ── Happy path ──────────────────────────────────────────────────────────

    [Fact]
    public void RegistrarCalificacion_ValoresValidos_RegistraEnMemoriaYRetornaExito()
    {
        // Arrange
        var nota = 8.5m;
        var justificacion = "El alumno comprende la abstracción y provee ejemplos correctos.";
        var desglose = "[{\"criterio\":\"Definición\",\"puntajeMaximo\":5,\"puntajeOtorgado\":4.5}]";

        // Act
        var result = RegistrarCalificacionSugeridaTool.RegistrarCalificacion(
            EntregaIdValido, nota, PuntajeMaximoValido, justificacion, desglose);

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
    public void RegistrarCalificacion_NotaIgualAlMaximo_RegistraCorrectamente()
    {
        // Nota exactamente en el límite debe ser aceptada
        var result = RegistrarCalificacionSugeridaTool.RegistrarCalificacion(
            EntregaIdValido, PuntajeMaximoValido, PuntajeMaximoValido, "Respuesta perfecta.");

        Assert.True(result.Exito);
        Assert.Equal(PuntajeMaximoValido, result.NotaSugerida);
    }

    [Fact]
    public void RegistrarCalificacion_NotaCero_RegistraCorrectamente()
    {
        // Nota 0 es válida (respuesta completamente incorrecta)
        var result = RegistrarCalificacionSugeridaTool.RegistrarCalificacion(
            EntregaIdValido, 0m, PuntajeMaximoValido, "No responde el concepto solicitado.");

        Assert.True(result.Exito);
        Assert.Equal(0m, result.NotaSugerida);
    }

    // ── Validaciones: notaSugerida ───────────────────────────────────────────

    [Fact]
    public void RegistrarCalificacion_NotaNegativa_RetornaErrorSinGuardar()
    {
        var result = RegistrarCalificacionSugeridaTool.RegistrarCalificacion(
            EntregaIdValido, -1m, PuntajeMaximoValido, "Justificación");

        Assert.False(result.Exito);
        Assert.NotNull(result.Error);
        Assert.Contains("negativa", result.Error);
        Assert.Empty(RegistrarCalificacionSugeridaTool.ObtenerRegistros());
    }

    [Fact]
    public void RegistrarCalificacion_NotaSuperaPuntajeMaximo_RetornaError()
    {
        // 11 > 10 → debe fallar
        var result = RegistrarCalificacionSugeridaTool.RegistrarCalificacion(
            EntregaIdValido, 11m, 10m, "Justificación válida.");

        Assert.False(result.Exito);
        Assert.NotNull(result.Error);
        Assert.Contains("excede", result.Error);
        Assert.Empty(RegistrarCalificacionSugeridaTool.ObtenerRegistros());
    }

    // ── Validaciones: puntajeMaximo ──────────────────────────────────────────

    [Fact]
    public void RegistrarCalificacion_PuntajeMaximoCero_RetornaError()
    {
        var result = RegistrarCalificacionSugeridaTool.RegistrarCalificacion(
            EntregaIdValido, 5m, 0m, "Justificación válida.");

        Assert.False(result.Exito);
        Assert.NotNull(result.Error);
        Assert.Contains("mayor que cero", result.Error);
        Assert.Empty(RegistrarCalificacionSugeridaTool.ObtenerRegistros());
    }

    [Fact]
    public void RegistrarCalificacion_PuntajeMaximoNegativo_RetornaError()
    {
        var result = RegistrarCalificacionSugeridaTool.RegistrarCalificacion(
            EntregaIdValido, 5m, -1m, "Justificación válida.");

        Assert.False(result.Exito);
        Assert.Contains("mayor que cero", result.Error);
    }

    // ── Validaciones: justificacion ──────────────────────────────────────────

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void RegistrarCalificacion_JustificacionVacia_RetornaErrorSinGuardar(string? justificacionInvalida)
    {
        var result = RegistrarCalificacionSugeridaTool.RegistrarCalificacion(
            EntregaIdValido, 7m, PuntajeMaximoValido, justificacionInvalida!);

        Assert.False(result.Exito);
        Assert.NotNull(result.Error);
        Assert.Contains("obligatoria", result.Error);
        Assert.Empty(RegistrarCalificacionSugeridaTool.ObtenerRegistros());
    }

    // ── Persistencia: entregaId ──────────────────────────────────────────────

    [Fact]
    public void RegistrarCalificacion_GuardaEntregaId_Correcto()
    {
        var entregaId = Guid.NewGuid();

        var result = RegistrarCalificacionSugeridaTool.RegistrarCalificacion(
            entregaId, 8m, PuntajeMaximoValido, "Buena respuesta.");

        Assert.True(result.Exito);
        var reg = RegistrarCalificacionSugeridaTool.ObtenerRegistros().Values.First();
        Assert.Equal(entregaId, reg.EntregaId);
    }

    // ── Entidad de dominio ───────────────────────────────────────────────────

    [Fact]
    public void CalificacionSugerida_Entidad_InicializaIdYFechaUtc()
    {
        var entidad = new CalificacionSugerida
        {
            EntregaId    = Guid.NewGuid(),
            NotaSugerida = 10,
            Justificacion = "Perfecto"
        };

        Assert.NotEqual(Guid.Empty, entidad.Id);
        Assert.NotEqual(Guid.Empty, entidad.EntregaId);
        Assert.True((DateTime.UtcNow - entidad.FechaRegistroUtc).TotalSeconds < 5);
    }
}
