using static KarateClassLibrary.Enums;

namespace KarateTournament.Api.Entities;

public class ParticipantCategoryType
{
    public int ParticipantId { get; set; }
    public Participant Participant { get; set; } = null!;
    public CategoryType CategoryType { get; set; }
}
