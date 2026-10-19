using services.SalaEnVivo.src.PEI.SalaEnVivo.Application.UseCases.Salas.Commands;
using services.SalaEnVivo.src.PEI.SalaEnVivo.Application.UseCases.Salas.Handlers;
using Microsoft.AspNetCore.Mvc;

namespace services.SalaEnVivo.src.PEI.SalaEnVivo.Api.Controllers
{
    [ApiController]
    [Route("api/v1/salas")]
    public class SalasController : ControllerBase
    {
        private readonly CrearSalaCommandHandler _crearSalaCommandHandler;

        public SalasController(CrearSalaCommandHandler crearSalaCommandHandler)
	    {
            _crearSalaCommandHandler = crearSalaCommandHandler;
        }

        [HttpPost]
        public async Task<IActionResult> CrearSala(CrearSalaCommand command, CancellationToken cancellationToken)
        {
            var resultado = await _crearSalaCommandHandler.HandleAsync(command, cancellationToken);

            return StatusCode(StatusCodes.Status201Created, new
            {
                mensaje = "Sala creada exitosamente",
            });
        }
    }
}
