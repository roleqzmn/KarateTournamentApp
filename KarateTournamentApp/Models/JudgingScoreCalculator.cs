using System.Collections.Generic;

namespace KarateTournamentApp.Models
{
    public static class JudgingScoreCalculator
    {
        public static (decimal FinalScore, List<int> DiscardedScoreIndexes) CalculateFinalScore(
            IReadOnlyList<decimal> scores)
            => KarateClassLibrary.Scoring.JudgingScoreCalculator.CalculateFinalScore(scores);
    }
}
