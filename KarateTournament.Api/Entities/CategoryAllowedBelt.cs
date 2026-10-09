using static KarateClassLibrary.Enums;

namespace KarateTournament.Api.Entities;

public class CategoryAllowedBelt
{
    public int CategoryId { get; set; }
    public Category Category { get; set; } = null!;
    public Belts Belt { get; set; }
    public int SortOrder { get; set; }
}
