namespace PEI.Identidad.Infrastructure.Security;

public sealed class InstitutionalOptions
{
    public string ActivationUrl { get; set; } = "";

    public Uri GetActivationUri(bool development)
    {
        if (!Uri.TryCreate(ActivationUrl, UriKind.Absolute, out var url) ||
            !string.IsNullOrEmpty(url.Query) || !string.IsNullOrEmpty(url.Fragment) ||
            !string.IsNullOrEmpty(url.UserInfo) ||
            (url.Scheme != "https" && !(development && url.Scheme == "http" && url.IsLoopback)))
            throw new InvalidOperationException("Configurar Institutional:ActivationUrl con HTTPS (HTTP solo en loopback de desarrollo).");
        return url;
    }
}
