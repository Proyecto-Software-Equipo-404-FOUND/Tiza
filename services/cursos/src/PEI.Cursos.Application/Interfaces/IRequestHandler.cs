using System.Threading;
using System.Threading.Tasks;

namespace PEI.Cursos.Application.Interfaces;

/* Interfaz generica para la atencion de casos de uso (Commands y Queries).
Garantiza que cada handler implemente un unico metodo de ejecucion respetando SRP. */
public interface IRequestHandler<in TRequest, TResponse>
{
    Task<TResponse> HandleAsync(TRequest request, CancellationToken cancellationToken = default);
}
