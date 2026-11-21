namespace KarateTournament.Api.Entities;

public sealed class TeamResult
{
    public int TeamId { get; set; }
    public int CategoryId { get; set; }
    public int Rank { get; set; }
    public decimal Score { get; set; }
    public ICollection<TeamJudgeScore> JudgeScores { get; set; } = [];
}

public sealed class TeamJudgeScore
{
    public int TeamId { get; set; }
    public int Ordinal { get; set; }
    public decimal Score { get; set; }
    public bool IsDiscarded { get; set; }
}
