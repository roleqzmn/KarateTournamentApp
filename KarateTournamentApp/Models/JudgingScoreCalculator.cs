using System.Collections.Generic;
using System.Linq;

namespace KarateTournamentApp.Models
{
    public static class JudgingScoreCalculator
    {
        public static (decimal FinalScore, List<int> DiscardedScoreIndexes) CalculateFinalScore(
            IReadOnlyList<decimal> scores)
        {
            var discardedScoreIndexes = new List<int>();
            if (scores.Count > 3)
            {
                var orderedScores = scores
                    .Select((score, index) => new { score, index })
                    .OrderBy(item => item.score)
                    .ThenBy(item => item.index)
                    .ToList();

                discardedScoreIndexes.Add(orderedScores[0].index);
                discardedScoreIndexes.Add(orderedScores[orderedScores.Count - 1].index);
            }

            var discardedScoreIndexSet = new HashSet<int>(discardedScoreIndexes);
            var finalScore = scores
                .Where((_, index) => !discardedScoreIndexSet.Contains(index))
                .Sum();

            return (finalScore, discardedScoreIndexes);
        }
    }
}
