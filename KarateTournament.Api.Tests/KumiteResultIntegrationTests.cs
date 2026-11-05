using System.Net;
using System.Net.Http.Json;
using KarateClassLibrary;
using KarateTournament.Api.Contracts;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Xunit;

namespace KarateTournament.Api.Tests;

public partial class TournamentApiIntegrationTests
{
    private async Task<(int Id, Guid Token, int[] Participants)> PrepareKumite(int count = 2)
    {
        var ids = new List<int>();
        for (var index = 0; index < count; index++)
        {
            using var response = await _client.PostAsJsonAsync("/api/participants",
                new CreateParticipantRequest($"Jan {index}", "Kowalski", 12, null, Belts.Dan, Sex.Male, [CategoryType.Kumite]));
            response.EnsureSuccessStatusCode();
            ids.Add((await response.Content.ReadFromJsonAsync<ParticipantDto>())!.Id);
        }
        using var category = await _client.PostAsJsonAsync("/api/categories",
            new CreateCategoryRequest("Kumite", null, null, Sex.Unisex, CategoryType.Kumite, [Belts.Dan], ids.ToArray()));
        category.EnsureSuccessStatusCode();
        var id = (await category.Content.ReadFromJsonAsync<CategoryDto>())!.Id;
        var token = Guid.NewGuid();
        using var claim = await _client.PostAsJsonAsync($"/api/categories/{id}/claim", new ClaimCategoryRequest(token, "Tatami 1"));
        claim.EnsureSuccessStatusCode();
        return (id, token, ids.ToArray());
    }

    [Fact]
    public async Task SavesFightStateAndRankingAndDeduplicatesConcurrentRetries()
    {
        var (id, token, ids) = await PrepareKumite();
        var match = new KumiteMatchResultRequest(0, ids[0], ids[1], ids[0], 2, 1,
            IsDisqualification: true, TimeRemaining: 12.5, AtenaiAka: 1, ChukokuShiro: 2, HasSenshuAka: true,
            IsInOvertime: true, OvertimeCount: 1);
        var request = new CompleteKumiteCategoryRequest(token, [match], ids);
        var responses = await Task.WhenAll(Enumerable.Range(0, 2).Select(_ =>
            _client.PostAsJsonAsync($"/api/categories/{id}/kumite-results", request)));
        try { Assert.All(responses, response => Assert.Equal(HttpStatusCode.NoContent, response.StatusCode)); }
        finally { foreach (var response in responses) response.Dispose(); }
        var category = (await _client.GetFromJsonAsync<CategoryDto>($"/api/categories/{id}"))!;
        Assert.True(category.IsFinished);
        Assert.False(category.IsClaimed);
        var stored = Assert.Single(category.BracketMatches);
        Assert.True(stored.Id > 0);
        Assert.True(stored.IsFinished);
        Assert.True(stored.IsDisqualification);
        Assert.Equal(ids[0], stored.AkaParticipantId);
        Assert.Equal(ids[1], stored.ShiroParticipantId);
        Assert.Equal(ids[0], stored.WinnerParticipantId);
        Assert.False(stored.IsRunning);
        Assert.Equal(2, stored.AkaScore);
        Assert.Equal(1, stored.ShiroScore);
        Assert.Equal(12.5, stored.TimeRemaining);
        Assert.Equal(1, stored.AtenaiAka);
        Assert.Equal(2, stored.ChukokuShiro);
        Assert.True(stored.HasSenshuAka);
        Assert.True(stored.IsInOvertime);
        Assert.Equal(1, stored.OvertimeCount);
        Assert.Equal(ids, category.FinalResults.Select(result => result.ParticipantId));
        Assert.Equal(new[] { 2m, 1m }, category.FinalResults.Select(result => result.Score));
        _client.Dispose();
        await _factory.DisposeAsync();
        StartApi();
        using var restartedRetry = await _client.PostAsJsonAsync($"/api/categories/{id}/kumite-results", request);
        Assert.Equal(HttpStatusCode.NoContent, restartedRetry.StatusCode);
        using var changed = await _client.PostAsJsonAsync($"/api/categories/{id}/kumite-results",
            request with { Matches = [match with { AkaScore = 3 }] });
        Assert.Equal(HttpStatusCode.Conflict, changed.StatusCode);
    }

    [Fact]
    public async Task RejectsWrongOwnerAndInconsistentBracketWithoutReleasingClaim()
    {
        var (id, token, ids) = await PrepareKumite(4);
        KumiteMatchResultRequest[] matches =
        [new(0, ids[0], ids[2], ids[0], 2, 0), new(1, ids[0], ids[1], ids[0], 2, 0), new(2, ids[2], ids[3], ids[2], 2, 0)];
        var valid = new CompleteKumiteCategoryRequest(token, matches, ids);
        using var wrongOwner = await _client.PostAsJsonAsync($"/api/categories/{id}/kumite-results",
            valid with { SessionToken = Guid.NewGuid() });
        Assert.Equal(HttpStatusCode.Conflict, wrongOwner.StatusCode);
        foreach (var request in new[]
        {
            valid with { Matches = null }, valid with { Matches = [null!] },
            valid with { Matches = matches.Take(2).ToArray() },
            valid with { Matches = [matches[0], matches[1], matches[2] with { BracketPosition = 1 }] },
            valid with { Matches = [matches[0] with { AkaParticipantId = ids[1] }, matches[1], matches[2]] },
            valid with { Matches = [matches[0] with { WinnerParticipantId = ids[1] }, matches[1], matches[2]] },
            valid with { Matches = [matches[0] with { AkaScore = -1 }, matches[1], matches[2]] },
            valid with { RankedParticipantIds = [ids[1], ids[0], ids[2], ids[3]] },
            valid with { RankedParticipantIds = [ids[0], ids[0], ids[2], ids[3]] },
            valid with
            {
                Matches = matches.Select(match => match with { AkaParticipantId = match.AkaParticipantId + 1000,
                    ShiroParticipantId = match.ShiroParticipantId + 1000, WinnerParticipantId = match.WinnerParticipantId + 1000 }).ToArray(),
                RankedParticipantIds = ids.Select(participantId => participantId + 1000).ToArray()
            }
        })
        {
            using var rejected = await _client.PostAsJsonAsync($"/api/categories/{id}/kumite-results", request);
            Assert.Equal(HttpStatusCode.BadRequest, rejected.StatusCode);
        }
        var category = (await _client.GetFromJsonAsync<CategoryDto>($"/api/categories/{id}"))!;
        Assert.False(category.IsFinished);
        Assert.True(category.IsClaimed);
        Assert.Empty(category.BracketMatches);
        using var missing = await _client.PostAsJsonAsync("/api/categories/2147483647/kumite-results", valid);
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        using var completed = await _client.PostAsJsonAsync($"/api/categories/{id}/kumite-results", valid);
        Assert.Equal(HttpStatusCode.NoContent, completed.StatusCode);
        using var reordered = await _client.PostAsJsonAsync($"/api/categories/{id}/kumite-results",
            valid with { Matches = matches.Reverse().ToArray() });
        Assert.Equal(HttpStatusCode.NoContent, reordered.StatusCode);
    }

    [Fact]
    public async Task PersistsByeAndEmptySubtreeWithoutInventingFightPoints()
    {
        var (id, token, ids) = await PrepareKumite(1);
        using var response = await _client.PostAsJsonAsync($"/api/categories/{id}/kumite-results",
            new CompleteKumiteCategoryRequest(token,
                [new(0, ids[0], null, ids[0]), new(1, ids[0], null, ids[0]), new(2, null, null, null)], ids));
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        var category = (await _client.GetFromJsonAsync<CategoryDto>($"/api/categories/{id}"))!;
        Assert.True(category.IsFinished);
        Assert.Equal(3, category.BracketMatches.Count);
        Assert.Null(category.BracketMatches[2].WinnerParticipantId);
        Assert.Equal(ids[0], Assert.Single(category.FinalResults).ParticipantId);
    }

    [Fact]
    public async Task DatabaseFailureRollsBackBracketRankingAndCompletion()
    {
        var (id, token, ids) = await PrepareKumite();
        await using var connection = new NpgsqlConnection(_database.GetConnectionString());
        await connection.OpenAsync();
        await using var constraint = new NpgsqlCommand(
            "ALTER TABLE \"Matches\" ADD CONSTRAINT test_failure CHECK (\"AkaScore\" < 0)", connection);
        await constraint.ExecuteNonQueryAsync();
        try
        {
            using var response = await _client.PostAsJsonAsync($"/api/categories/{id}/kumite-results",
                new CompleteKumiteCategoryRequest(token, [new(0, ids[0], ids[1], ids[0], 2, 0)], ids));
            Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        }
        catch (DbUpdateException) { /* TestServer may propagate an unhandled application exception. */ }
        var category = (await _client.GetFromJsonAsync<CategoryDto>($"/api/categories/{id}"))!;
        Assert.False(category.IsFinished);
        Assert.True(category.IsClaimed);
        Assert.Empty(category.BracketMatches);
        Assert.Empty(category.FinalResults);
    }
}
