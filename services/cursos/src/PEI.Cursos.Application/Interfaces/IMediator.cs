using System.Threading;
using System.Threading.Tasks;

namespace PEI.Cursos.Application.Interfaces;

/* Contrato del despachador mediador liviano.
Permite enviar solicitudes (commands o queries) a su correspondiente handler
desacoplando a los controladores de la logica concreta de aplicacion. */
public interface IMediator
{
    Task<TResponse> SendAsync<TRequest, TResponse>(TRequest request, CancellationToken cancellationToken = default);
}
