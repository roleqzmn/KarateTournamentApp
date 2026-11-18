namespace KarateTournament.Api.Entities;

public sealed class RevokedCategoryClaim
{
    public int CategoryId { get; set; }
    public string ClaimReference { get; set; } = string.Empty;
}
