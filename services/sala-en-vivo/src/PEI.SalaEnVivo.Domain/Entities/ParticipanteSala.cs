namespace services.SalaEnVivo.src.PEI.SalaEnVivo.Domain.Entities
{
    public class ParticipanteSala
    {
        public int Id { get; set; }
        public int SalaId { get; set; }

        public Sala? Sala { get; set; }
    }
}
