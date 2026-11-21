using System.Text.Json.Serialization;

namespace KarateTournamentApp.Models;

public interface IKataEntrant
{
    int Id { get; }
    string JudgingName { get; }
    string? JudgingDetails { get; }
}

public abstract class KataPerformanceResult
{
    public decimal Score { get; set; }
    public List<decimal> JudgeScores { get; set; } = [];
    public List<int> DiscardedJudgeScoreIndexes { get; set; } = [];

    [JsonIgnore] public IKataEntrant Entrant => GetEntrant();
    [JsonIgnore] public string Name => Entrant.JudgingName;
    [JsonIgnore] public string? Details => Entrant.JudgingDetails;

    protected abstract IKataEntrant GetEntrant();
}

public sealed class TeamKataResult : KataPerformanceResult
{
    public required Team Team { get; set; }

    protected override IKataEntrant GetEntrant() => Team;
}
