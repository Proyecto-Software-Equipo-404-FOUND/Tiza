namespace PEI.SalaEnVivo.Domain.Entities
{
    public class ParticipanteSala
    {
        public Guid Id { get; set; }
        public Guid SalaId { get; set; }
        public Guid AlumnoId { get; set; }

        /// <summary>S3-SA-01 (#231): nombre que escribió el invitado. Null para los alumnos con cuenta.</summary>
        public string? NombreInvitado { get; set; }
        public Sala? Sala { get; set; }
    }
}
