using PEI.IA.Application.DatasetReader;
using PEI.IA.Application.Prompts;

namespace PEI.IA.UnitTests;

public class DatasetReaderTests
{
    private const string SampleDatasetJson = """
    {
      "version": 1,
      "descripcion": "Dataset de prueba unitaria",
      "fechaCreacion": "2026-10-19",
      "autor": "404 Found",
      "preguntas": [
        {
          "id": "p001",
          "enunciado": "¿Qué es la encapsulación?",
          "tipo": "Abierta",
          "puntajeMaximo": 10,
          "rubrica": {
            "criterios": [
              { "descripcion": "Define ocultamiento de estado", "puntaje": 6 },
              { "descripcion": "Menciona modificadores de acceso", "puntaje": 4 }
            ]
          },
          "respuestas": [
            {
              "id": "r001",
              "texto": "Es ocultar el estado interno mediante private/public.",
              "notaEsperada": 10,
              "justificacionEsperada": "Cumple ambos criterios."
            },
            {
              "id": "r002",
              "texto": "No sé.",
              "notaEsperada": 0,
              "justificacionEsperada": "No cumple."
            }
          ]
        }
      ]
    }
    """;

    [Fact]
    public async Task CargarDesdeJson_ParseaPreguntasYRespuestasCorrectamente()
    {
        var reader = new DatasetReader();
        reader.CargarDesdeJson(SampleDatasetJson);

        var preguntas = await reader.ObtenerPreguntasAsync();

        Assert.Single(preguntas);
        var p = preguntas[0];
        Assert.Equal("p001", p.Id);
        Assert.Equal(10m, p.PuntajeMaximo);
        Assert.Equal(2, p.Rubrica.Criterios.Count);
        Assert.Equal(2, p.Respuestas.Count);

        var sumCriterios = p.Rubrica.Criterios.Sum(c => c.Puntaje);
        Assert.Equal(p.PuntajeMaximo, sumCriterios);
    }

    [Fact]
    public async Task ObtenerRespuestasPorPreguntaAsync_RetornaRespuestasEsperadas()
    {
        var reader = new DatasetReader();
        reader.CargarDesdeJson(SampleDatasetJson);

        var respuestas = await reader.ObtenerRespuestasPorPreguntaAsync("p001");

        Assert.Equal(2, respuestas.Count);
        Assert.Equal("r001", respuestas[0].Id);
        Assert.Equal(10m, respuestas[0].NotaEsperada);
    }

    [Fact]
    public void ObtenerRubricaJson_EInyeccionEnPromptBuilder_ProducePromptValidoConRubrica()
    {
        var reader = new DatasetReader();
        reader.CargarDesdeJson(SampleDatasetJson);

        var rubricaJson = reader.ObtenerRubricaJson("p001");

        Assert.Contains("Define ocultamiento de estado", rubricaJson);
        Assert.Contains("6", rubricaJson);

        // Inyectar en PromptBuilder
        var promptBuilder = new PromptBuilder();
        var prompt = promptBuilder.BuildUserPrompt(
            "¿Qué es la encapsulación?",
            rubricaJson,
            10m,
            "Respuesta del alumno");

        Assert.Contains(rubricaJson, prompt);
        Assert.Contains("¿Qué es la encapsulación?", prompt);
        Assert.DoesNotContain("{RUBRICA_JSON}", prompt);
    }

    [Fact]
    public void OperacionesSinCargarDataset_LanzanInvalidOperationException()
    {
        var reader = new DatasetReader();

        Assert.Throws<InvalidOperationException>(() => reader.ObtenerRubricaJson("p001"));
    }
}
