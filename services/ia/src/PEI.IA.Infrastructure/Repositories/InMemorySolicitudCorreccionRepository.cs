using System.Collections.Concurrent;
using PEI.IA.Application.Interfaces;
using PEI.IA.Domain.Entities;

namespace PEI.IA.Infrastructure.Repositories;

/// <summary>
/// Implementación en memoria y thread-safe del repositorio de SolicitudCorreccion (SolicitudIA).
/// Permite almacenar, auditar y consultar las ejecuciones del servicio de IA (proveedor, tokens, latencia).
/// </summary>
public class InMemorySolicitudCorreccionRepository : ISolicitudCorreccionRepository
{
    private static readonly ConcurrentDictionary<Guid, SolicitudCorreccion> Solicitudes = new();

    /// <inheritdoc />
    public Task GuardarAsync(SolicitudCorreccion solicitud, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(solicitud);
        Solicitudes[solicitud.Id] = solicitud;
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<SolicitudCorreccion?> ObtenerPorIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        Solicitudes.TryGetValue(id, out var solicitud);
        return Task.FromResult(solicitud);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<SolicitudCorreccion>> ListarAsync(Guid? institucionId = null, CancellationToken cancellationToken = default)
    {
        var query = Solicitudes.Values.AsEnumerable();
        if (institucionId.HasValue)
        {
            query = query.Where(s => s.InstitucionId == institucionId.Value);
        }

        IReadOnlyList<SolicitudCorreccion> lista = query.OrderByDescending(s => s.FechaCreacionUtc).ToList();
        return Task.FromResult(lista);
    }

    /// <summary>
    /// Limpia el almacén en memoria (utilizado para aislar pruebas unitarias).
    /// </summary>
    public static void Limpiar()
    {
        Solicitudes.Clear();
    }
}
