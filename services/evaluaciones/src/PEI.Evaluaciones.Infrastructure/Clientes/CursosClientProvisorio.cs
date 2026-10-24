using PEI.Evaluaciones.Application.Evaluaciones;

namespace PEI.Evaluaciones.Infrastructure.Clientes;

// El que se encarga de CU-05, reemplazar por el cliente real cuando Cursos tenga el endpoint interno.
public class CursosClientProvisorio : ICursosClient
{
    public Task<bool> ExisteCohorteAsync(Guid cohorteId, Guid institucionId, CancellationToken ct)
    {
        // Implementación provisional - reemplazar con la llamada real al cliente de Cursos
        return Task.FromResult(true);
    }
}