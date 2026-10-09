using static KarateClassLibrary.Enums;

namespace KarateTournament.Api.Entities;

public class Participant
{
    public int Id { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public int Age { get; set; }
    public string? Club { get; set; }
    public Belts Belt { get; set; }
    public Sex Sex { get; set; }

    public ICollection<ParticipantCategoryType> CategoryTypes { get; set; } = [];
    public ICollection<CategoryParticipant> CategoryMemberships { get; set; } = [];
    public ICollection<TeamMember> TeamMemberships { get; set; } = [];
}
