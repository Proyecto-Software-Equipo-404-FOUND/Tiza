namespace PEI.SalaEnVivo.Application.UseCases.Salas.Commands
{
    public class CrearSalaCommand
    {
        public Guid InstitucionId { get; set; }
        public Guid EvaluacionId { get; set; }
        public int CapacidadMaxima { get; set; }

        /// <summary>Minutos que la sala queda abierta desde que se crea (por defecto 120).</summary>
        public int DuracionMinutos { get; set; } = DuracionPorDefecto;

        public const int DuracionPorDefecto = 120;
        public const int DuracionMinima = 5;
        public const int DuracionMaxima = 240;
    }
}
