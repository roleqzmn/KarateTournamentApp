using KarateClassLibrary;
using KarateClassLibrary.Contracts;
using KarateTournamentApp.Models;
using KarateTournamentApp.Services.Api;
using Xunit;

namespace KarateTournamentApp.Tests;

public sealed class JudgingCheckpointMapperTests
{
    [Fact]
    public void TeamCheckpointKeepsThePerformingTeamAndResolvedRanking()
    {
        var first = new Team("Pierwsza", Sex.Unisex) { Id = 41 };
        var second = new Team("Druga", Sex.Unisex) { Id = 42 };
        var original = new JudgingSession(1, "Team Kata", Guid.NewGuid(), "Tatami 1")
        {
            Category = new TeamCategory { CategoryType = CategoryType.Team, Teams = [first, second] },
            TeamDraft = new TeamJudgeDraft(1, [8.5m], [new(first.Id, [6m, 8m, 9m, 10m])])
        };
        var next = new JudgingSession(1, "Team Kata", Guid.NewGuid(), "Tatami 2")
        {
            Category = new TeamCategory { CategoryType = CategoryType.Team, Teams = [first, second] }
        };

        var checkpoint = JudgingCheckpointMapper.Capture(original);
        JudgingCheckpointMapper.Restore(next, checkpoint);
        Assert.Null(checkpoint.Individual);
        Assert.Equal(1, next.TeamDraft!.TeamIndex);
        Assert.Equal(new[] { 8.5m }, next.TeamDraft.CurrentScores);
        Assert.Equal(first.Id, Assert.Single(next.TeamDraft.Results).TeamId);

        var completed = checkpoint with
        {
            IsCompleteLocally = true,
            Team = new TeamJudgeDraft(1, [], [checkpoint.Team!.Results[0], new(second.Id, [9m, 9m, 9m])]),
            RankedTeamIds = [second.Id, first.Id]
        };
        JudgingCheckpointMapper.Restore(next, completed);
        var resumed = Assert.IsType<TeamCategory>(next.Category);
        Assert.True(resumed.IsFinished);
        Assert.Equal(new[] { second.Id, first.Id }, resumed.TeamResults.Select(result => result.Team.Id));
        Assert.Equal(new[] { 27m, 17m }, resumed.TeamResults.Select(result => result.Score));
        Assert.Empty(resumed.FinalResults);
    }

    [Fact]
    public void RestoresKumitePointsPenaltiesAndClockWithoutStartingTheTimer()
    {
        var first = new Participant("Jan", "Test", 12, Belts.Kyu8, Sex.Male, [CategoryType.Kumite]);
        var second = new Participant("Adam", "Test", 12, Belts.Kyu8, Sex.Male, [CategoryType.Kumite]);
        var fight = new ShobuSanbonMatch
        {
            Aka = first.Id, Shiro = second.Id, AkaScore = 2, ShiroScore = 1, TimeRemaining = 33.5,
            AtenaiAka = 1, ChukokuShiro = 2, IsRunning = true, HasSenshuAka = true,
            IsInOvertime = true, OvertimeCount = 1
        };
        var original = new JudgingSession(1, "Kumite", Guid.NewGuid(), "Tatami 1")
        {
            Category = new ShobuSanbonCategory { CategoryType = CategoryType.Kumite, Participants = [first, second], BracketMatches = [fight] }
        };
        var next = new JudgingSession(1, "Kumite", Guid.NewGuid(), "Tatami 2")
        {
            Category = new ShobuSanbonCategory { CategoryType = CategoryType.Kumite, Participants = [first, second] }
        };

        var checkpoint = JudgingCheckpointMapper.Capture(original);
        JudgingCheckpointMapper.Restore(next, checkpoint);

        var resumed = Assert.IsType<ShobuSanbonMatch>(Assert.Single(next.Category.BracketMatches));
        Assert.Equal(2, resumed.AkaScore);
        Assert.Equal(1, resumed.ShiroScore);
        Assert.Equal(33.5, resumed.TimeRemaining);
        Assert.Equal(1, resumed.AtenaiAka);
        Assert.Equal(2, resumed.ChukokuShiro);
        Assert.Equal(1, resumed.OvertimeCount);
        Assert.True(resumed.HasSenshuAka);
        Assert.True(resumed.IsInOvertime);
        Assert.False(resumed.IsRunning);
    }
}
