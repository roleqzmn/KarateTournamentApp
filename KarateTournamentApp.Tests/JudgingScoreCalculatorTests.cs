using KarateTournamentApp.Models;
using Xunit;

namespace KarateTournamentApp.Tests;

public class JudgingScoreCalculatorTests
{
    [Fact]
    public void ThreeJudgesKeepEveryScore()
    {
        var result = JudgingScoreCalculator.CalculateFinalScore([7.1m, 8.2m, 9.3m]);
        Assert.Equal(24.6m, result.FinalScore);
        Assert.Empty(result.DiscardedScoreIndexes);
    }

    [Fact]
    public void FiveJudgesDiscardExactlyOneLowestAndHighestScore()
    {
        var scores = new decimal[] { 8.4m, 9.1m, 7.2m, 8.3m, 8.5m };
        var result = JudgingScoreCalculator.CalculateFinalScore(scores);
        Assert.Equal(25.2m, result.FinalScore);
        Assert.Equal(new[] { 2, 1 }, result.DiscardedScoreIndexes);
        Assert.Equal(new decimal[] { 8.4m, 9.1m, 7.2m, 8.3m, 8.5m }, scores);
    }

    [Fact]
    public void RepeatedExtremesDiscardOnlyTwoIndividualNotes()
    {
        var result = JudgingScoreCalculator.CalculateFinalScore([7m, 9m, 8m, 7m, 9m]);
        Assert.Equal(24m, result.FinalScore);
        Assert.Equal(new[] { 0, 4 }, result.DiscardedScoreIndexes);
    }

    [Fact]
    public void EqualNotesDiscardDistinctIndexesDeterministically()
    {
        var result = JudgingScoreCalculator.CalculateFinalScore([8m, 8m, 8m, 8m, 8m]);
        Assert.Equal(24m, result.FinalScore);
        Assert.Equal(new[] { 0, 4 }, result.DiscardedScoreIndexes);
    }

    [Fact]
    public void FourJudgesLeaveTwoScoresAfterDiscardingExtremes()
    {
        var result = JudgingScoreCalculator.CalculateFinalScore([6m, 7.5m, 8.5m, 10m]);
        Assert.Equal(16m, result.FinalScore);
        Assert.Equal(new[] { 0, 3 }, result.DiscardedScoreIndexes);
    }
}
