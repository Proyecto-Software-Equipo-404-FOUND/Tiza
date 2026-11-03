namespace PEI.SalaEnVivo.Domain.Entities
{
    public class AuditoriaFoco
    {
        public const int UmbralAlertaRojaMs = 3000;
        public const string SeveridadAmarillo = "Amarillo";
        public const string SeveridadRoja = "Roja";

        public Guid Id { get; set; }
        public Guid InstitucionId { get; set; }
        public Guid SalaId { get; set; }
        public Guid AlumnoId { get; set; }
        public Guid? PreguntaId { get; set; }
        public string TipoEvento { get; set; } = string.Empty;
        public int DuracionMs { get; set; }
        public string Severidad { get; set; } = string.Empty;
        public DateTime RegistradoUtc { get; set; }

        public Sala? Sala { get; set; }

        public AuditoriaFoco() { }

        public static AuditoriaFoco Crear(
            Guid institucionId,
            Guid salaId,
            Guid alumnoId,
            int duracionMs,
            string? tipoEvento = null,
            Guid? preguntaId = null,
            DateTime? registradoUtc = null)
        {
            if (institucionId == Guid.Empty)
                throw new ArgumentException("El InstitucionId no puede ser vacío.", nameof(institucionId));

            if (salaId == Guid.Empty)
                throw new ArgumentException("El SalaId no puede ser vacío.", nameof(salaId));

            if (alumnoId == Guid.Empty)
                throw new ArgumentException("El AlumnoId no puede ser vacío.", nameof(alumnoId));

            if (duracionMs < 0)
                throw new ArgumentOutOfRangeException(nameof(duracionMs), "La duración fuera de foco no puede ser negativa.");

            var eventoNormalizado = string.IsNullOrWhiteSpace(tipoEvento)
                ? "visibilitychange"
                : tipoEvento.Trim();

            if (eventoNormalizado.Length > 64)
                eventoNormalizado = eventoNormalizado[..64];

            var severidad = duracionMs >= UmbralAlertaRojaMs
                ? SeveridadRoja
                : SeveridadAmarillo;

            return new AuditoriaFoco
            {
                Id = Guid.NewGuid(),
                InstitucionId = institucionId,
                SalaId = salaId,
                AlumnoId = alumnoId,
                PreguntaId = preguntaId == Guid.Empty ? null : preguntaId,
                TipoEvento = eventoNormalizado,
                DuracionMs = duracionMs,
                Severidad = severidad,
                RegistradoUtc = registradoUtc ?? DateTime.UtcNow
            };
        }
    }
}
