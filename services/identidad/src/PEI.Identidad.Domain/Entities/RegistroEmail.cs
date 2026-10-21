namespace PEI.Identidad.Domain.Entities;

public class RegistroEmail
{
    public string EmailNormalizado { get; private set; } = null!;

    public string TipoCuenta { get; private set; } = null!;

    public Guid CuentaId { get; private set; }

    public RegistroEmail(
        string emailNormalizado,
        string tipoCuenta,
        Guid cuentaId)
    {
        if (string.IsNullOrWhiteSpace(emailNormalizado))
            throw new ArgumentException(
                "El correo normalizado es obligatorio.",
                nameof(emailNormalizado));

        if (tipoCuenta is not ("Institucional" or "Plataforma"))
            throw new ArgumentException(
                "El tipo de cuenta no es válido.",
                nameof(tipoCuenta));

        if (cuentaId == Guid.Empty)
            throw new ArgumentException(
                "El identificador de cuenta es obligatorio.",
                nameof(cuentaId));

        EmailNormalizado = emailNormalizado;
        TipoCuenta = tipoCuenta;
        CuentaId = cuentaId;
    }

    private RegistroEmail()
    {
    }
}
