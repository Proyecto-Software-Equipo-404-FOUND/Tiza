using PEI.IA.Application.Prompts;

namespace PEI.IA.UnitTests;

public class PromptBuilderTests
{
    [Fact]
    public void BuildSystemPrompt_RetornaInstruccionesPedagogicasEsperadas()
    {
        var builder = new PromptBuilder();

        var systemPrompt = builder.BuildSystemPrompt();

        Assert.NotNull(systemPrompt);
        Assert.Contains("asistente pedagógico", systemPrompt);
        Assert.Contains("rúbrica", systemPrompt);
        Assert.Contains("Anti-Alucinación", systemPrompt);
        Assert.Contains("RegistrarCalificacionSugerida", systemPrompt);
    }

    [Fact]
    public void BuildUserPrompt_ReemplazaTodosLosPlaceholdersCorrectamente()
    {
        var builder = new PromptBuilder();
        var enunciado = "¿Qué es el polimorfismo?";
        var rubricaJson = "{\"criterios\":[{\"descripcion\":\"Definición precisa\",\"puntaje\":5}]}";
        var puntajeMaximo = 10.0m;
        var respuestaAlumno = "El polimorfismo permite que distintos objetos respondan al mismo mensaje.";

        var userPrompt = builder.BuildUserPrompt(enunciado, rubricaJson, puntajeMaximo, respuestaAlumno);

        Assert.NotNull(userPrompt);
        Assert.Contains(enunciado, userPrompt);
        Assert.Contains(rubricaJson, userPrompt);
        Assert.Contains("10", userPrompt);
        Assert.Contains(respuestaAlumno, userPrompt);

        // Verificar que no queden placeholders sin sustituir
        Assert.DoesNotContain("{ENUNCIADO}", userPrompt);
        Assert.DoesNotContain("{RUBRICA_JSON}", userPrompt);
        Assert.DoesNotContain("{PUNTAJE_MAXIMO}", userPrompt);
        Assert.DoesNotContain("{RESPUESTA_ALUMNO}", userPrompt);
    }

    [Theory]
    [InlineData("", "rubrica", "respuesta")]
    [InlineData("enunciado", "", "respuesta")]
    [InlineData("enunciado", "rubrica", "")]
    public void BuildUserPrompt_ArgumentosInvalidos_LanzaArgumentException(string enunciado, string rubrica, string respuesta)
    {
        var builder = new PromptBuilder();

        Assert.ThrowsAny<ArgumentException>(() =>
            builder.BuildUserPrompt(enunciado, rubrica, 10m, respuesta));
    }
}
