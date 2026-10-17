namespace services.sala-en-vivo.src.PEI.SalaEnVivo.Domain.Entities;

public class Sala
{
    public int Id { get; set; }
    public int InstitucionId { get; set; }

    public byte[] Version { get; set; } = Array.Empty<byte>();
    public DateTime FechaVencimiento { get; set; }
    public int CodigoPIN { get; set; }
    public string Estado { get; set; }
}
