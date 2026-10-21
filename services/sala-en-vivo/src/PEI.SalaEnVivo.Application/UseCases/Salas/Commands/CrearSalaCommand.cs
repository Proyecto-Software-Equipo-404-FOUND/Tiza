namespace PEI.SalaEnVivo.Application.UseCases.Salas.Commands
{
    public class CrearSalaCommand
    {
        public Guid InstitucionId { get; set; }
        public Guid EvaluacionId { get; set; }
        public int CapacidadMaxima { get; set; }
    }
}
