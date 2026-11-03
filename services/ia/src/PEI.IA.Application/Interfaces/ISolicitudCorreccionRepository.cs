using PEI.IA.Domain.Entities;

namespace PEI.IA.Application.Interfaces;

/// <summary>
/// Contrato de persistencia y auditoría de solicitudes de corrección procesadas por el servicio de IA.
/// Registra proveedor utilizado, modelo, tokens y latencia.
/// </summary>
public interface ISolicitudCorreccionRepository
{
    /// <summary>
    /// Guarda o actualiza una solicitud de corrección en el almacenamiento.
    /// </summary>
    Task GuardarAsync(SolicitudCorreccion solicitud, CancellationToken cancellationToken = default);

    /// <summary>
    /// Obtiene una solicitud por su identificador único.
    /// </summary>
    Task<SolicitudCorreccion?> ObtenerPorIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lista solicitudes registradas, opcionalmente filtrando por institución.
    /// </summary>
    Task<IReadOnlyList<SolicitudCorreccion>> ListarAsync(Guid? institucionId = null, CancellationToken cancellationToken = default);
}
