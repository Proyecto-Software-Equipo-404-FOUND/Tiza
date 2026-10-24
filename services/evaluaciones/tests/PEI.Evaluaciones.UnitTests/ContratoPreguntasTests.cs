using System.Text.Json.Nodes;
using PEI.Evaluaciones.Application.Validacion;

namespace PEI.Evaluaciones.UnitTests;

public class ContratoPreguntasTests
{
    private const string Valida = """
        {"version":1,"preguntas":[{"id":"00000000-0000-4000-8000-000000000101",
        "enunciado":"Explicar una relación","tipo":"Abierta","puntajeMaximo":10,
        "rubrica":{"criterios":[{"descripcion":"Claridad","puntaje":10}]}}]}
        """;

    [Fact]
    public void AceptaPreguntaAbiertaConRubrica() => Assert.True(new ValidadorPreguntas().Validar(Valida).EsValido);

    [Theory]
    [InlineData("no-json")]
    [InlineData("{}")]
    [InlineData("{\"version\":\"1\",\"preguntas\":[]}")]
    [InlineData("{\"version\":1,\"preguntas\":{}}")]
    public void RechazaDocumentoInvalido(string json) => Assert.False(new ValidadorPreguntas().Validar(json).EsValido);

    [Theory]
    [InlineData("id", "no-uuid")]
    [InlineData("tipo", "Desconocido")]
    [InlineData("puntajeMaximo", "texto")]
    public void RechazaCamposInvalidos(string campo, string valor)
    {
        var json = JsonNode.Parse(Valida)!;
        json["preguntas"]![0]![campo] = valor;
        Assert.False(new ValidadorPreguntas().Validar(json.ToJsonString()).EsValido);
    }

    [Theory]
    [InlineData("id")]
    [InlineData("enunciado")]
    [InlineData("tipo")]
    [InlineData("puntajeMaximo")]
    public void RechazaCamposObligatoriosAusentes(string campo)
    {
        var json = JsonNode.Parse(Valida)!;
        json["preguntas"]![0]!.AsObject().Remove(campo);
        Assert.False(new ValidadorPreguntas().Validar(json.ToJsonString()).EsValido);
    }
}
