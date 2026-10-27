namespace PEI.Identidad.Domain.Entities;

public sealed class InvitacionAdministrador
{
    public Guid Id { get; set; }
    public Guid UsuarioId { get; set; }
    public string TokenHash { get; set; } = "";
    public DateTime Vencimiento { get; set; }
    public DateTime? Utilizada { get; set; }
}

public sealed class EntregaInvitacion
{
    public Guid Id { get; set; }
    public Guid InvitacionId { get; set; }
    public string Destinatario { get; set; } = "";
    public string ContenidoProtegido { get; set; } = "";
    public DateTime Creada { get; set; }
    public DateTime? Entregada { get; set; }
    public int Intentos { get; set; }
    public DateTime? ProximoIntento { get; set; }
}
