using System.Text;
using PEI.Identidad.Api.Controllers;

namespace PEI.Identidad.UnitTests;

public sealed class CsvAlumnosParserTests
{
    [Fact]
    public void AceptaUtf8ConBomComillasComasYSaltosDeLinea()
    {
        var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(
            "nombre,apellido,email\r\n\"Ana, María\",\"Pérez\nLópez\",ana@example.test\r\n")).ToArray();

        var filas = CsvAlumnosParser.Parse(bytes);

        var fila = Assert.Single(filas);
        Assert.Equal(2, fila.Numero);
        Assert.Equal("Ana, María", fila.Nombre);
        Assert.Equal("Pérez\nLópez", fila.Apellido);
        Assert.Equal("ana@example.test", fila.Email);
    }

    [Theory]
    [InlineData("")]
    [InlineData("nombre,apellido,email\n\"sin cierre,apellido,a@example.test")]
    [InlineData("nombre,apellido,email\nAna,Apellido\"Roto,a@example.test")]
    [InlineData("nombre,apellido,email\nAna,Apellido,a@example.test,extra")]
    [InlineData("nombre,apellido,Email\nAna,Apellido,a@example.test")]
    public void RechazaArchivoVacioOMalformado(string csv) =>
        Assert.Throws<CsvImportFormatException>(() => CsvAlumnosParser.Parse(Encoding.UTF8.GetBytes(csv)));

    [Fact]
    public void RechazaUtf8Invalido()
    {
        var bytes = new byte[] { 0x6E, 0x6F, 0x6D, 0x62, 0x72, 0x65, 0x2C, 0x61, 0x70, 0x65, 0x6C, 0x6C, 0x69, 0x64, 0x6F, 0x2C, 0x65, 0x6D, 0x61, 0x69, 0x6C, 0x0A, 0xFF };
        Assert.Throws<CsvImportFormatException>(() => CsvAlumnosParser.Parse(bytes));
    }

    [Fact]
    public void AceptaExactamenteDoscientasFilasYRechazaDoscientasUna()
    {
        var csv = "nombre,apellido,email\n" + string.Concat(Enumerable.Range(0, 200).Select(i => $"N{i},A{i},u{i}@example.test\n"));
        Assert.Equal(200, CsvAlumnosParser.Parse(Encoding.UTF8.GetBytes(csv)).Count);
        csv += "N200,A200,u200@example.test\n";
        Assert.Throws<CsvImportFormatException>(() => CsvAlumnosParser.Parse(Encoding.UTF8.GetBytes(csv)));
    }

    [Fact]
    public void RechazaArchivoMayorAUnMib()
    {
        var bytes = new byte[CsvAlumnosParser.MaximoBytes + 1];
        Assert.Throws<CsvImportFormatException>(() => CsvAlumnosParser.Parse(bytes));
    }
}
