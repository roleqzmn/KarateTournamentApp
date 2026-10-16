using KarateTournamentApp.Models;
using Xunit;

namespace KarateTournamentApp.Tests;

public class BracketTests
{
    [Theory]
    [InlineData(42, null, 42, true)]
    [InlineData(null, 43, 43, true)]
    [InlineData(42, 43, null, false)]
    [InlineData(null, null, null, false)]
    public void MatchAwardsByeOnlyWhenExactlyOneParticipantExists(
        int? aka, int? shiro, int? winner, bool finished)
    {
        var match = new Match(aka, shiro);
        Assert.Equal(winner, match.WinnerId);
        Assert.Equal(finished, match.IsFinished);
    }

    [Theory]
    [InlineData(1, true)]
    [InlineData(2, false)]
    public void SemifinalWinnerAdvancesToCorrectSideOfFinal(int index, bool akaSide)
    {
        var category = new Category { BracketMatches = [new(), new(), new()] };
        category.BracketMatches[index].WinnerId = 42;
        category.PromoteWinner(index);
        Assert.Equal(42, akaSide ? category.BracketMatches[0].Aka : category.BracketMatches[0].Shiro);
        Assert.Null(akaSide ? category.BracketMatches[0].Shiro : category.BracketMatches[0].Aka);
        Assert.False(category.IsFinished);
    }

    [Fact]
    public void FinalWinnerFinishesCategory()
    {
        var category = new Category { BracketMatches = [new() { WinnerId = 42 }] };
        category.PromoteWinner(0);
        Assert.True(category.IsFinished);
        Assert.Equal(42, category.BracketMatches[0].WinnerId);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void TwoParticipantsInitializeOneUnfinishedMatch(bool shobu)
    {
        Category category = shobu ? new ShobuSanbonCategory() : new Category();
        category.Participants.Add(new Participant { Id = 42 });
        category.Participants.Add(new Participant { Id = 43 });
        category.InitializeBracket();
        var match = Assert.Single(category.BracketMatches);
        Assert.Equal(42, match.Aka);
        Assert.Equal(43, match.Shiro);
        Assert.False(match.IsFinished);
        Assert.Null(match.WinnerId);
        if (shobu) Assert.IsType<ShobuSanbonMatch>(match);
    }
}
