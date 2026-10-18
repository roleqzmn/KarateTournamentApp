using KarateClassLibrary;
using KarateTournament.Api.Data;
using KarateTournament.Api.Entities;
using Xunit;

namespace KarateTournament.Api.Tests;

public class CategoryReadModelTests
{
    [Fact]
    public void PreservesDomainOrderRegardlessOfCollectionInsertionOrder()
    {
        var category = new Category
        {
            Id = 7, Name = "Kata", CategoryType = CategoryType.Kata,
            ParticipantLinks =
            [
                new() { ParticipantId = 20, SortOrder = 1 },
                new() { ParticipantId = 10, SortOrder = 0 }
            ],
            AllowedBeltLinks =
            [
                new() { Belt = Belts.Dan, SortOrder = 1 },
                new() { Belt = Belts.Kyu1, SortOrder = 0 }
            ],
            BracketMatches = [new() { Id = 2, BracketPosition = 1 }, new() { Id = 1, BracketPosition = 0 }],
            Teams =
            [
                new() { Id = 2, SortOrder = 1 },
                new() { Id = 1, SortOrder = 0, MemberLinks =
                    [new() { ParticipantId = 20, SortOrder = 1 }, new() { ParticipantId = 10, SortOrder = 0 }] }
            ],
            FinalResults =
            [
                new() { Id = 3, Rank = null },
                new() { Id = 2, Rank = 2 },
                new() { Id = 1, Rank = 1, Score = 8.5m, JudgeScores =
                    [new() { Ordinal = 1, Score = 9m }, new() { Ordinal = 0, Score = 8m, IsDiscarded = true }] }
            ]
        };

        var dto = CategoryReadModel.ToDto(category);

        Assert.Equal(new[] { 10, 20 }, dto.ParticipantIds);
        Assert.Equal(new[] { Belts.Kyu1, Belts.Dan }, dto.AllowedBelts);
        Assert.Equal(new[] { 1, 2 }, dto.BracketMatches.Select(match => match.Id));
        Assert.Equal(new[] { 1, 2 }, dto.Teams.Select(team => team.Id));
        Assert.Equal(new[] { 10, 20 }, dto.Teams[0].MemberParticipantIds);
        Assert.Equal(new[] { 1, 2, 3 }, dto.FinalResults.Select(result => result.Id));
        Assert.Equal(new[] { 8m, 9m }, dto.FinalResults[0].JudgeScores.Select(score => score.Score));
        Assert.True(dto.FinalResults[0].JudgeScores[0].IsDiscarded);
        Assert.Equal(8.5m, dto.FinalResults[0].Score);
    }

    [Fact]
    public void SupportsNewCategoryWithoutCompetitionData()
    {
        var dto = CategoryReadModel.ToDto(new Category { Id = 1, Name = "Nowa" });
        Assert.Equal(1, dto.Id);
        Assert.Equal("Nowa", dto.Name);
        Assert.False(dto.IsFinished);
        Assert.Empty(dto.ParticipantIds);
        Assert.Empty(dto.BracketMatches);
        Assert.Empty(dto.Teams);
        Assert.Empty(dto.FinalResults);
    }
}
