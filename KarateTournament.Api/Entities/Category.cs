using static KarateClassLibrary.Enums;

namespace KarateTournament.Api.Entities;

public class Category
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int? MinAge { get; set; }
    public int? MaxAge { get; set; }
    public bool IsFinished { get; set; }
    public Sex Sex { get; set; }
    public CategoryType CategoryType { get; set; }

    public ICollection<CategoryParticipant> ParticipantLinks { get; set; } = [];
    public ICollection<CategoryAllowedBelt> AllowedBeltLinks { get; set; } = [];
    public ICollection<Match> BracketMatches { get; set; } = [];
    public ICollection<Team> Teams { get; set; } = [];
    public ICollection<ParticipantResult> FinalResults { get; set; } = [];
}
