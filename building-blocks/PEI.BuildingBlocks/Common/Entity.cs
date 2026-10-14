namespace PEI.BuildingBlocks.Common;

public abstract class Entity<TId>
{
    public TId Id { get; protected set; } = default!;

    /// <summary>
    /// Token de concurrencia optimista (Criterio A07 de cátedra).
    /// Incrementa en cada actualización. Responde 409 Conflict si hay desfase.
    /// </summary>
    public int Version { get; set; } = 1;

    public DateTime FechaCreacionUtc { get; protected set; } = DateTime.UtcNow;
    public DateTime? FechaModificacionUtc { get; protected set; }
}
