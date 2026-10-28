namespace PEI.SalaEnVivo.Domain.Entities
{
    public class RespuestaSala
    {
        public Guid Id { get; set; }
        public Guid InstitucionId { get; set; }
        public Guid SalaId { get; set; }
        public Guid AlumnoId { get; set; }
        public Guid PreguntaId { get; set; }
        public string Respuesta { get; set; } = string.Empty;
        public Guid IdempotencyKey { get; set; }
        public int Secuencia { get; set; }
        public DateTime RecibidaUtc { get; set; }

        public Sala? Sala { get; set; }

        public RespuestaSala() { }

        public static RespuestaSala Crear(
            Guid institucionId,
            Guid salaId,
            Guid alumnoId,
            Guid preguntaId,
            string respuesta,
            Guid idempotencyKey,
            int secuencia,
            DateTime? recibidaUtc = null)
        {
            if (institucionId == Guid.Empty)
                throw new ArgumentException("El InstitucionId no puede ser vacío.", nameof(institucionId));

            if (salaId == Guid.Empty)
                throw new ArgumentException("El SalaId no puede ser vacío.", nameof(salaId));

            if (alumnoId == Guid.Empty)
                throw new ArgumentException("El AlumnoId no puede ser vacío.", nameof(alumnoId));

            if (preguntaId == Guid.Empty)
                throw new ArgumentException("El PreguntaId no puede ser vacío.", nameof(preguntaId));

            if (idempotencyKey == Guid.Empty)
                throw new ArgumentException("El IdempotencyKey no puede ser vacío.", nameof(idempotencyKey));

            if (string.IsNullOrWhiteSpace(respuesta))
                throw new ArgumentException("La respuesta no puede estar vacía.", nameof(respuesta));

            var trimmedRespuesta = respuesta.Trim();
            if (trimmedRespuesta.Length > 10000)
                throw new ArgumentException("La respuesta no puede exceder los 10.000 caracteres.", nameof(respuesta));

            if (secuencia < 1)
                throw new ArgumentOutOfRangeException(nameof(secuencia), "La secuencia debe ser mayor o igual a 1.");

            return new RespuestaSala
            {
                Id = Guid.NewGuid(),
                InstitucionId = institucionId,
                SalaId = salaId,
                AlumnoId = alumnoId,
                PreguntaId = preguntaId,
                Respuesta = trimmedRespuesta,
                IdempotencyKey = idempotencyKey,
                Secuencia = secuencia,
                RecibidaUtc = recibidaUtc ?? DateTime.UtcNow
            };
        }
    }
}
