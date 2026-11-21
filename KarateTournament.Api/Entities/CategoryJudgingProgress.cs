namespace KarateTournament.Api.Entities;

public sealed class CategoryJudgingProgress
{
    public int CategoryId { get; set; }
    public Guid? RequestedFor { get; set; }
    public Guid? LastReleasedToken { get; set; }
    public string? CheckpointJson { get; set; }
}
