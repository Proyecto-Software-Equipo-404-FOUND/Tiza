using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using PEI.Cursos.Application.Interfaces;

namespace PEI.Cursos.Application.Common;

/* Implementacion en memoria del mediador liviano.
Resuelve en tiempo de ejecucion la instancia del handler registrado en el
contenedor de dependencias para la solicitud recibida y ejecuta su logica. */
public class Mediator : IMediator
{
    private readonly IServiceProvider _serviceProvider;

    public Mediator(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public async Task<TResponse> SendAsync<TRequest, TResponse>(TRequest request, CancellationToken cancellationToken = default)
    {
        var handler = _serviceProvider.GetRequiredService<IRequestHandler<TRequest, TResponse>>();
        return await handler.HandleAsync(request, cancellationToken);
    }
}
