using KarateClassLibrary.Scoring;
using Xunit;

namespace KarateTournament.Api.Tests;

public class JudgingScoreCalculatorTests
{
    [Fact]
    public void ThreeNotesAllContributeToScore()
    {
        var result = JudgingScoreCalculator.CalculateFinalScore([7m, 8m, 9m]);
        Assert.Equal(24m, result.FinalScore);
        Assert.Empty(result.DiscardedScoreIndexes);
    }

    [Fact]
    public void EqualExtremaDiscardTwoDistinctNotesDeterministically()
    {
        var result = JudgingScoreCalculator.CalculateFinalScore([8m, 8m, 8m, 8m, 8m]);
        Assert.Equal(24m, result.FinalScore);
        Assert.Equal(new[] { 0, 4 }, result.DiscardedScoreIndexes);
    }
}
