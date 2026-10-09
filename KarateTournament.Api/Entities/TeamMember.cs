namespace KarateTournament.Api.Entities;

public class TeamMember
{
    public int TeamId { get; set; }
    public Team Team { get; set; } = null!;
    public int ParticipantId { get; set; }
    public Participant Participant { get; set; } = null!;
    public int SortOrder { get; set; }
}
