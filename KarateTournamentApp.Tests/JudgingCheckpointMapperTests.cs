using KarateClassLibrary;
using KarateTournamentApp.Models;
using KarateTournamentApp.Services.Api;
using Xunit;

namespace KarateTournamentApp.Tests;

public sealed class JudgingCheckpointMapperTests
{
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
