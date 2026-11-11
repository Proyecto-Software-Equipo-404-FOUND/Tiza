using PEI.Identidad.Infrastructure.Security;

namespace PEI.Identidad.UnitTests;

public sealed class InstitutionalOptionsTests
{
    [Fact]
    public void AceptaHttpsSinQueryNiFragmento()
    {
        var options = new InstitutionalOptions { ActivationUrl = "https://frontend.tiza.test/activar-alumno" };

        Assert.Equal("https://frontend.tiza.test/activar-alumno", options.GetActivationUri(development: false).AbsoluteUri);
    }

    [Fact]
    public void SoloPermiteHttpLocalEnDevelopment()
    {
        var options = new InstitutionalOptions { ActivationUrl = "http://localhost:5173/activar-alumno" };

        Assert.Equal("http://localhost:5173/activar-alumno", options.GetActivationUri(development: true).AbsoluteUri);
        Assert.Throws<InvalidOperationException>(() => options.GetActivationUri(development: false));
    }

    [Theory]
    [InlineData("https://frontend.tiza.test/activar?next=x")]
    [InlineData("https://frontend.tiza.test/activar#fragment")]
    [InlineData("https://user@frontend.tiza.test/activar")]
    [InlineData("not-a-url")]
    public void RechazaUrlNoSegura(string value)
    {
        var options = new InstitutionalOptions { ActivationUrl = value };

        Assert.Throws<InvalidOperationException>(() => options.GetActivationUri(development: false));
    }
}
