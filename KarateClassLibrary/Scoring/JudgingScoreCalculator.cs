namespace KarateClassLibrary.Scoring;

public static class JudgingScoreCalculator
{
    public static (decimal FinalScore, List<int> DiscardedScoreIndexes) CalculateFinalScore(
        IReadOnlyList<decimal> scores)
    {
        var discarded = new List<int>();
        if (scores.Count > 3)
        {
            var ordered = scores.Select((score, index) => (score, index))
                .OrderBy(item => item.score).ThenBy(item => item.index).ToArray();
            discarded.Add(ordered[0].index);
            discarded.Add(ordered[^1].index);
        }
        return (scores.Where((_, index) => !discarded.Contains(index)).Sum(), discarded);
    }
}
