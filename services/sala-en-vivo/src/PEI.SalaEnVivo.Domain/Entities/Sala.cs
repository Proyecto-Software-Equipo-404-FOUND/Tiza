namespace PEI.SalaEnVivo.Domain.Entities
{
    public class Sala
    {
        public Guid Id { get; set; }
        public Guid InstitucionId { get; set; }

        public int[] Version { get; set; } = Array.Empty<int>();
        public DateTime FechaVencimiento { get; set; }
        public int CodigoPIN { get; set; }
        public string Estado { get; set; } = string.Empty;
        public int CapacidadMaxima { get; set; }
        public int CantidadParticipantes { get; set; }

        public void setCapacidadMaxima(int capacidad)
        {
            if (capacidad < 1)
            {
                throw new ArgumentException("La capacidad máxima debe ser mayor a cero.");
            }
            CapacidadMaxima = capacidad;
        }
        public bool modificarCapacidadMaxima(int nuevaCapacidad)
        {
            if (nuevaCapacidad < 1 || nuevaCapacidad < CantidadParticipantes)
            {
                return false;
            }
            CapacidadMaxima = nuevaCapacidad;
            return true;
        }
    }
}
