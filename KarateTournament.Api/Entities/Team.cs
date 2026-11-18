using KarateClassLibrary;

namespace KarateTournament.Api.Entities;

public class Team
{
    public int Id { get; set; }
    public int CategoryId { get; set; }
    public Category Category { get; set; } = null!;
    public string Name { get; set; } = string.Empty;
    public Sex Sex { get; set; }
    public int SortOrder { get; set; }
    public ICollection<TeamMember> MemberLinks { get; set; } = [];
}
