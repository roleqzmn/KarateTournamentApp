using System.IO;
using KarateClassLibrary;
using KarateTournamentApp.Models;
using KarateTournamentApp.Services;
using Xunit;

namespace KarateTournamentApp.Tests;

public class TournamentIdentityNormalizerTests
{
    private static Participant Person(int id, string name = "Jan") =>
        new(name, "Kowalski", 12, Belts.Kyu8, Sex.Male, [CategoryType.Kata]) { Id = id };

    [Fact]
    public void RepeatedIdentitySharesObjectAcrossCategoriesResultsAndTeams()
    {
        var original = Person(42);
        var category = new Category { Participants = [original, Person(42)] };
        category.FinalResults.Add(new ParticipantResult { Participant = Person(42) });
        var team = new Team("Team", Sex.Male) { Members = [Person(42), Person(42)] };
        var other = new TeamCategory { Participants = [Person(42)], Teams = [team] };
        TournamentIdentityNormalizer.Normalize([category, other]);
        Assert.Same(original, Assert.Single(category.Participants));
        Assert.Same(original, Assert.Single(other.Participants));
        Assert.Same(original, category.FinalResults[0].Participant);
        Assert.Same(original, Assert.Single(team.Members));
        Assert.Equal(42, original.Id);
    }

    [Fact]
    public void CollidingIdsAcrossCategoriesAreRepairedTogetherWithReferences()
    {
        var first = new Category { Participants = [Person(42)] };
        var second = new Category { Participants = [Person(42, "Adam")] };
        second.BracketMatches.Add(new Match { Aka = 42, WinnerId = 42 });
        second.JudgingScores.Add((new List<decimal> { 8m, 8m, 8m }, 42));
        TournamentIdentityNormalizer.Normalize([first, second]);
        Assert.NotEqual(first.Participants[0].Id, second.Participants[0].Id);
        var repairedId = second.Participants[0].Id;
        Assert.True(repairedId > 0);
        Assert.Equal(repairedId, second.BracketMatches[0].Aka);
        Assert.Equal(repairedId, second.BracketMatches[0].WinnerId);
        Assert.Equal(repairedId, second.JudgingScores[0].ParticipantId);
    }

    [Fact]
    public void AmbiguousMatchReferenceIsRejectedInsteadOfGuessingWinner()
    {
        var category = new Category { Participants = [Person(42), Person(42, "Adam")] };
        category.BracketMatches.Add(new Match { WinnerId = 42 });
        Assert.Throws<InvalidDataException>(() => TournamentIdentityNormalizer.Normalize([category]));
    }

    [Fact]
    public void MissingParticipantReferenceIsRejected()
    {
        var category = new Category { Participants = [Person(42)] };
        category.BracketMatches.Add(new Match { Shiro = 99 });
        Assert.Throws<InvalidDataException>(() => TournamentIdentityNormalizer.Normalize([category]));
    }

    [Fact]
    public void RepeatedNormalizationPreservesIdsAndReferences()
    {
        var category = new Category { Participants = [Person(0)] };
        category.BracketMatches.Add(new Match { Aka = 0 });
        TournamentIdentityNormalizer.Normalize([category]);
        var id = category.Participants[0].Id;
        TournamentIdentityNormalizer.Normalize([category]);
        Assert.Equal(id, category.Participants[0].Id);
        Assert.Equal(id, category.BracketMatches[0].Aka);
    }

    [Fact]
    public void DuplicateCategoryAndTeamIdsBecomeUnique()
    {
        var first = new TeamCategory { Id = 42, Teams = [new Team("A", Sex.Male) { Id = 42 }] };
        var second = new TeamCategory { Id = 42, Teams = [new Team("B", Sex.Male) { Id = 42 }] };
        TournamentIdentityNormalizer.Normalize([first, second]);
        Assert.NotEqual(first.Id, second.Id);
        Assert.NotEqual(first.Teams[0].Id, second.Teams[0].Id);
        Assert.All(new[] { first.Id, second.Id, first.Teams[0].Id, second.Teams[0].Id },
            id => Assert.True(id > 0));
    }
}
