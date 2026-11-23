namespace PEI.SalaEnVivo.Domain.Entities
{
    public class Sala
    {
        public Guid Id { get; set; }
        public Guid InstitucionId { get; set; }

        public int[] Version { get; set; } = Array.Empty<int>();
        /// <summary>
        /// S3-SA-02 (#239): antes de iniciar es el plazo máximo para iniciar la sala
        /// (creación + <see cref="EsperaMaximaSinIniciar"/>); al iniciar pasa a ser
        /// el fin del examen (IniciadaUtc + DuracionMinutos).
        /// </summary>
        public DateTime FechaVencimiento { get; set; }

        /// <summary>S3-SA-02 (#239): minutos que dura el examen, contados desde que el docente lo inicia.</summary>
        public int DuracionMinutos { get; set; } = DuracionPorDefectoMinutos;

        /// <summary>S3-SA-02 (#239): momento en que el docente inició la sala. Null mientras no se inicia.</summary>
        public DateTime? IniciadaUtc { get; set; }

        public const int DuracionPorDefectoMinutos = 120;

        /// <summary>Una sala creada y nunca iniciada deja de aceptar el PIN pasado este plazo.</summary>
        public static readonly TimeSpan EsperaMaximaSinIniciar = TimeSpan.FromHours(24);

        public bool FueIniciada => IniciadaUtc.HasValue;
        public int CodigoPIN { get; set; }
        public string Estado { get; set; } = string.Empty;
        public int CapacidadMaxima { get; set; }
        public int CantidadParticipantes { get; set; }
        public Guid EvaluacionId { get; set; }
        public Guid? PreguntaActualId { get; set; }
        public int IndicePreguntaActual { get; set; }

        /// <summary>
        /// Momento exacto en que la sala pasó a estado Finalizada (en UTC).
        /// Requerido por el contrato de lote de entregas hacia Evaluaciones (lote-entregas-v1)
        /// y por el evento SignalR salaFinalizada.
        /// </summary>
        public DateTime? FinalizadaUtc { get; set; }

        /// <summary>
        /// Motivo del cierre autoritativo de la sala ("Docente" o "TiempoAgotado").
        /// Alineado con contracts/signalr/sala-hub-v1.md (sección 6.6).
        /// </summary>
        public string? MotivoCierre { get; set; }

        /// <summary>
        /// Indica que la sala se finalizó pero el lote de entregas no pudo enviarse
        /// a Evaluaciones. El reintento definitivo queda para el Outbox de S3-SA-01.
        /// </summary>
        public bool EntregaPendiente { get; set; }

        /// <summary>
        /// Pasa la sala a EnCurso. La primera vez fija IniciadaUtc y calcula el vencimiento
        /// con la duración elegida (S3-SA-02, #239). Es idempotente: si ya estaba iniciada,
        /// no vuelve a mover el reloj.
        /// </summary>
        public void Iniciar(DateTime? ahoraUtc = null)
        {
            Estado = "EnCurso";

            if (IniciadaUtc.HasValue)
            {
                return;
            }

            var ahora = ahoraUtc ?? DateTime.UtcNow;
            IniciadaUtc = ahora;
            FechaVencimiento = ahora.AddMinutes(DuracionMinutos > 0 ? DuracionMinutos : DuracionPorDefectoMinutos);
        }

        /// <summary>
        /// Finaliza la sala de forma autoritativa e idempotente.
        /// Si la sala ya se encontraba en estado 'Finalizada', no modifica la fecha original
        /// ni el motivo de cierre previo y retorna false.
        /// </summary>
        /// <param name="motivo">Causa del cierre: "Docente" o "TiempoAgotado" (por defecto "Docente").</param>
        /// <returns>True si la sala realizó la transición a Finalizada; False si ya estaba finalizada.</returns>
        public bool Finalizar(string motivo = "Docente")
        {
            if (string.Equals(Estado, "Finalizada", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            Estado = "Finalizada";
            FinalizadaUtc = DateTime.UtcNow;
            MotivoCierre = string.Equals(motivo, "TiempoAgotado", StringComparison.OrdinalIgnoreCase)
                ? "TiempoAgotado"
                : "Docente";
            return true;
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
