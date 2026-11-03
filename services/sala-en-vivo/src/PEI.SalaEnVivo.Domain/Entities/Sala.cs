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
        public Guid EvaluacionId { get; set; }
        public Guid? PreguntaActualId { get; set; }
        public int IndicePreguntaActual { get; set; }

        public void Iniciar()
        {
            Estado = "EnCurso";
        }

        public void EstablecerPreguntaActual(Guid preguntaId, int indice)
        {
            if (preguntaId == Guid.Empty)
            {
                throw new ArgumentException("La pregunta actual no puede ser vacía.", nameof(preguntaId));
            }

            if (indice < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(indice), "El índice de la pregunta debe ser mayor o igual a 1.");
            }

            PreguntaActualId = preguntaId;
            IndicePreguntaActual = indice;
        }

        public void SetCapacidadMaxima(int capacidad)
        {
            if (capacidad < 1)
            {
                throw new ArgumentException("La capacidad máxima debe ser mayor a cero.");
            }
            CapacidadMaxima = capacidad;
        }
        public bool ModificarCapacidadMaxima(int nuevaCapacidad)
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
