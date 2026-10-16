using KarateClassLibrary;
using KarateTournamentApp.Models;
using Xunit;

namespace KarateTournamentApp.Tests;

public class CategoryMergeTests
{
    [Fact]
    public void MergeCombinesParticipantsAndEligibilityWithoutDuplicatesOrChangingSource()
    {
        var first = new Participant { Id = 41 };
        var second = new Participant { Id = 42 };
        var target = new Category(Belts.Kyu8, CategoryType.Kata, Sex.Male, 10, 12)
        {
            Participants = [first, second]
        };
        var duplicateSecond = new Participant { Id = 42 };
        var third = new Participant { Id = 43 };
        var source = new Category([Belts.Kyu8, Belts.Kyu7], CategoryType.Kata, Sex.Female, 9, 14)
        {
            Participants = [duplicateSecond, third]
        };

        target.MergeWith(source);

        Assert.Equal(new[] { first, second, third }, target.Participants);
        Assert.Equal(new[] { Belts.Kyu8, Belts.Kyu7 }, target.AllowedBelts);
        Assert.Equal(9, target.MinAge);
        Assert.Equal(14, target.MaxAge);
        Assert.Equal(Sex.Unisex, target.Sex);
        Assert.Equal(new[] { duplicateSecond, third }, source.Participants);
        Assert.Equal(9, source.MinAge);
        Assert.Equal(14, source.MaxAge);
        Assert.Equal(Sex.Female, source.Sex);
        Assert.Equal(new[] { Belts.Kyu8, Belts.Kyu7 }, source.AllowedBelts);
    }

    [Fact]
    public void TeamMergeDeduplicatesByIdAndPreservesExistingTeamAndSource()
    {
        var existing = new Team("Existing", Sex.Male) { Id = 41 };
        var duplicate = new Team("Imported copy", Sex.Male) { Id = 41 };
        var added = new Team("New", Sex.Male) { Id = 42 };
        var target = new TeamCategory([Belts.Kyu8], CategoryType.Team, Sex.Male, 12, 14)
        {
            Teams = [existing]
        };
        var source = new TeamCategory([Belts.Kyu8, Belts.Kyu7], CategoryType.Team, Sex.Male, 10, 13)
        {
            Teams = [duplicate, added]
        };

        target.MergeWith(source);

        Assert.Equal(new[] { existing, added }, target.Teams);
        Assert.Equal(new[] { Belts.Kyu8, Belts.Kyu7 }, target.AllowedBelts);
        Assert.Equal(10, target.MinAge);
        Assert.Equal(14, target.MaxAge);
        Assert.Equal(Sex.Male, target.Sex);
        Assert.Equal(new[] { duplicate, added }, source.Teams);
        Assert.Equal(10, source.MinAge);
        Assert.Equal(13, source.MaxAge);
        Assert.Equal(Sex.Male, source.Sex);
        Assert.Equal(new[] { Belts.Kyu8, Belts.Kyu7 }, source.AllowedBelts);
    }
}
