using KarateClassLibrary;
using KarateClassLibrary.Contracts;
using KarateTournamentApp.ViewModels;
using Xunit;

namespace KarateTournamentApp.Tests;

public sealed class OrganizerCompetitionRowTests
{
    [Theory]
    [InlineData(false, false, OrganizerCompetitionStatus.Available, "Dostępna", null)]
    [InlineData(false, true, OrganizerCompetitionStatus.InProgress, "W trakcie", "Tatami 1")]
    [InlineData(true, false, OrganizerCompetitionStatus.Finished, "Zakończona", null)]
    [InlineData(true, true, OrganizerCompetitionStatus.Finished, "Zakończona", null)]
    public void UsesPersistedResultsAndClaimsToDetermineStatus(
        bool finished, bool claimed, OrganizerCompetitionStatus expected, string label, string? station)
    {
        var summary = new CategorySummaryDto(1, "Kata", CategoryType.Kata, Sex.Unisex, null, null,
            finished, 2, 0, claimed, claimed ? "Tatami 1" : null);

        var row = new OrganizerCompetitionRow(summary);

        Assert.Equal(expected, row.Status);
        Assert.Equal(label, row.StatusText);
        Assert.Equal(station, row.StationName);
    }
}
