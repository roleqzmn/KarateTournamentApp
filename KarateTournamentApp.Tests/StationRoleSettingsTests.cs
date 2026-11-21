using KarateTournamentApp.Models;
using Xunit;

namespace KarateTournamentApp.Tests;

public sealed class StationRoleSettingsTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("invalid")]
    [InlineData("1")]
    [InlineData("judge")]
    public void DefaultsToJudgeWithoutAnExplicitPrivilegedRole(string? setting)
    {
        Assert.Equal(StationRole.Judge, StationRoleSettings.Parse(setting));
    }

    [Fact]
    public void RecognizesExplicitOrganizerAndLocalModes()
    {
        Assert.Equal(StationRole.Organizer, StationRoleSettings.Parse(" ORGANIZER "));
        Assert.Equal(StationRole.Local, StationRoleSettings.Parse("local"));
    }
}
