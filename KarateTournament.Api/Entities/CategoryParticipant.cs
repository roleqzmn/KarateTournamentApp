namespace KarateTournament.Api.Entities;

public class CategoryParticipant
{
    public int CategoryId { get; set; }
    public Category Category { get; set; } = null!;
    public int ParticipantId { get; set; }
    public Participant Participant { get; set; } = null!;
    public int SortOrder { get; set; }
}
