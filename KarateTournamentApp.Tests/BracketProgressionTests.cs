using KarateTournamentApp.Models;
using Xunit;

namespace KarateTournamentApp.Tests;

public class BracketProgressionTests
{
    [Theory]
    [InlineData(false, 5)]
    [InlineData(true, 5)]
    [InlineData(false, 6)]
    [InlineData(true, 6)]
    [InlineData(false, 9)]
    [InlineData(true, 9)]
    [InlineData(false, 8)]
    [InlineData(true, 8)]
    public void EveryParticipantCanProgressToFinalWithoutEmptyBranchesBlockingCompetition(
        bool shobu, int participantCount)
    {
        Category category = shobu ? new ShobuSanbonCategory() : new Category();
        for (var id = 1; id <= participantCount; id++)
            category.Participants.Add(new Participant { Id = id });
        category.InitializeBracket();
        var eliminated = new HashSet<int>();
        var playedMatches = 0;

        while (!category.IsFinished && playedMatches < participantCount)
        {
            // Select a playable match, as the competition manager does.
            var matchIndex = category.BracketMatches.FindLastIndex(match =>
                !match.IsFinished && match.Aka.HasValue && match.Shiro.HasValue);
            Assert.True(matchIndex >= 0, "Competition stalled before the final: no playable match remains.");
            var match = category.BracketMatches[matchIndex];
            Assert.DoesNotContain(match.Aka!.Value, eliminated);
            Assert.DoesNotContain(match.Shiro!.Value, eliminated);
            Assert.NotEqual(match.Aka, match.Shiro);

            // A deterministic winner avoids depending on the random initial draw.
            match.WinnerId = Math.Min(match.Aka.Value, match.Shiro.Value);
            var loser = Math.Max(match.Aka.Value, match.Shiro.Value);
            Assert.True(eliminated.Add(loser), "A participant was eliminated more than once.");
            match.IsFinished = true;
            category.PromoteWinner(matchIndex);
            playedMatches++;
        }

        Assert.True(category.IsFinished);
        Assert.Equal(participantCount - 1, playedMatches);
        Assert.Equal(Enumerable.Range(2, participantCount - 1), eliminated.OrderBy(id => id));
        Assert.Equal(1, category.BracketMatches[0].WinnerId);
    }
}
