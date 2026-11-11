using System.Security.Cryptography;
using System.Text;

namespace PEI.Evaluaciones.Api.Seguridad;

// Validación del header X-Api-Key-Interna (RN-INT-01), en tiempo constante.
// Misma lógica que usa SalasInternoController (#143), para reutilizar en los endpoints internos.
public static class ClaveInterna
{
    public const string Header = "X-Api-Key-Interna";

    public static bool EsValida(IConfiguration configuracion, string? recibida)
    {
        var esperada = configuracion["ServiciosInternos:ApiKey"];
        if (string.IsNullOrEmpty(esperada) || string.IsNullOrEmpty(recibida)) return false;

        var a = Encoding.UTF8.GetBytes(esperada);
        var b = Encoding.UTF8.GetBytes(recibida);
        return a.Length == b.Length && CryptographicOperations.FixedTimeEquals(a, b);
    }
}
