namespace PEI.SalaEnVivo.Domain.Entities
{
    public class ParticipanteSala
    {
        public Guid Id { get; set; }
        public Guid SalaId { get; set; }

        public Sala? Sala { get; set; }
    }
}
