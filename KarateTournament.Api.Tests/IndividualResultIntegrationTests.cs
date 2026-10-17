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
    private async Task<(int CategoryId, int ParticipantId, Guid Token)> PrepareScoredCategory()
    {
        using var participant = await _client.PostAsJsonAsync("/api/participants",
            new CreateParticipantRequest("Jan", "Kowalski", 12, null, Belts.Dan, Sex.Male, [CategoryType.Kata]));
        participant.EnsureSuccessStatusCode();
        var participantId = (await participant.Content.ReadFromJsonAsync<ParticipantDto>())!.Id;
        using var category = await _client.PostAsJsonAsync("/api/categories",
            new CreateCategoryRequest("Kata", null, null, Sex.Unisex, CategoryType.Kata, [Belts.Dan], [participantId]));
        category.EnsureSuccessStatusCode();
        var categoryId = (await category.Content.ReadFromJsonAsync<CategoryDto>())!.Id;
        var token = Guid.NewGuid();
        using var claim = await _client.PostAsJsonAsync($"/api/categories/{categoryId}/claim", new ClaimCategoryRequest(token, "Tatami 1"));
        claim.EnsureSuccessStatusCode();
        return (categoryId, participantId, token);
    }

    [Fact]
    public async Task SavesCalculatedResultsAndAllowsIdenticalConcurrentRetries()
    {
        var (id, participantId, token) = await PrepareScoredCategory();
        // Exercise the upgrade from the preceding schema without touching local tournament data.
        await using (var connection = new NpgsqlConnection(_database.GetConnectionString()))
        {
            await connection.OpenAsync();
            await using var oldSchema = new NpgsqlCommand(
                "ALTER TABLE \"Categories\" DROP COLUMN \"CompletionToken\", DROP COLUMN \"CompletionHash\"", connection);
            await oldSchema.ExecuteNonQueryAsync();
            var sql = await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "add-category-completion.sql"));
            for (var attempt = 0; attempt < 2; attempt++)
            {
                await using var upgrade = new NpgsqlCommand(sql, connection);
                await upgrade.ExecuteNonQueryAsync();
            }
        }
        var request = new CompleteIndividualCategoryRequest(token, [new(participantId, 1, [6m, 7m, 8m, 9m, 10m])]);
        var responses = await Task.WhenAll(Enumerable.Range(0, 2).Select(_ =>
            _client.PostAsJsonAsync($"/api/categories/{id}/individual-results", request)));
        try { Assert.All(responses, response => Assert.Equal(HttpStatusCode.NoContent, response.StatusCode)); }
        finally { foreach (var response in responses) response.Dispose(); }
        var category = (await _client.GetFromJsonAsync<CategoryDto>($"/api/categories/{id}"))!;
        Assert.True(category.IsFinished);
        Assert.False(category.IsClaimed);
        var result = Assert.Single(category.FinalResults);
        Assert.Equal(24m, result.Score);
        Assert.Equal(new[] { 0, 4 }, result.JudgeScores.Where(score => score.IsDiscarded).Select(score => score.Ordinal));
        _client.Dispose();
        await _factory.DisposeAsync();
        StartApi();
        using var retry = await _client.PostAsJsonAsync($"/api/categories/{id}/individual-results", request);
        Assert.Equal(HttpStatusCode.NoContent, retry.StatusCode);
        Assert.DoesNotContain(token.ToString(), await _client.GetStringAsync($"/api/categories/{id}"));
        using var changed = await _client.PostAsJsonAsync($"/api/categories/{id}/individual-results",
            request with { Results = [new(participantId, 1, [7m, 7m, 8m])] });
        Assert.Equal(HttpStatusCode.Conflict, changed.StatusCode);
    }

    [Fact]
    public async Task RejectsWrongSessionAndInvalidResultsWithoutEndingCategory()
    {
        var (id, participantId, token) = await PrepareScoredCategory();
        using var wrongSession = await _client.PostAsJsonAsync($"/api/categories/{id}/individual-results",
            new CompleteIndividualCategoryRequest(Guid.NewGuid(), [new(participantId, 1, [8m, 8m, 8m])]));
        Assert.Equal(HttpStatusCode.Conflict, wrongSession.StatusCode);
        foreach (var input in new IndividualResultRequest[]?[]
        {
            null, [], [null!], [new(participantId, 1, null)], [new(participantId, 1, [8m, 8m])],
            [new(participantId, 1, [11m, 8m, 8m])], [new(participantId, 1, [8.001m, 8m, 8m])],
            [new(participantId, 2, [8m, 8m, 8m])], [new(participantId + 1, 1, [8m, 8m, 8m])]
        })
        {
            using var response = await _client.PostAsJsonAsync($"/api/categories/{id}/individual-results",
                new CompleteIndividualCategoryRequest(token, input));
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }
        var category = (await _client.GetFromJsonAsync<CategoryDto>($"/api/categories/{id}"))!;
        Assert.False(category.IsFinished);
        Assert.True(category.IsClaimed);
        Assert.Empty(category.FinalResults);
    }

    [Fact]
    public async Task DatabaseFailureRollsBackResultsAndCompletionTogether()
    {
        var (id, participantId, token) = await PrepareScoredCategory();
        await using var connection = new NpgsqlConnection(_database.GetConnectionString());
        await connection.OpenAsync();
        // Force a write failure only inside this disposable test database.
        await using var constraint = new NpgsqlCommand(
            "ALTER TABLE \"ParticipantResults\" ADD CONSTRAINT test_failure CHECK (\"Score\" < 0)", connection);
        await constraint.ExecuteNonQueryAsync();
        try
        {
            using var response = await _client.PostAsJsonAsync($"/api/categories/{id}/individual-results",
                new CompleteIndividualCategoryRequest(token, [new(participantId, 1, [8m, 8m, 8m])]));
            Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        }
        catch (DbUpdateException) { /* TestServer may propagate an unhandled application exception. */ }
        var category = (await _client.GetFromJsonAsync<CategoryDto>($"/api/categories/{id}"))!;
        Assert.False(category.IsFinished);
        Assert.True(category.IsClaimed);
        Assert.Empty(category.FinalResults);
    }

    [Fact]
    public async Task RequiresCompleteUniqueRankingsAndAcceptsLocallyResolvedTie()
    {
        var (_, firstId, _) = await PrepareScoredCategory();
        using var participant = await _client.PostAsJsonAsync("/api/participants",
            new CreateParticipantRequest("Anna", "Nowak", 12, null, Belts.Dan, Sex.Female, [CategoryType.Kata]));
        participant.EnsureSuccessStatusCode();
        var secondId = (await participant.Content.ReadFromJsonAsync<ParticipantDto>())!.Id;
        using var created = await _client.PostAsJsonAsync("/api/categories",
            new CreateCategoryRequest("Kata", null, null, Sex.Unisex, CategoryType.Kata, [Belts.Dan], [firstId, secondId]));
        created.EnsureSuccessStatusCode();
        var id = (await created.Content.ReadFromJsonAsync<CategoryDto>())!.Id;
        var token = Guid.NewGuid();
        using var claim = await _client.PostAsJsonAsync($"/api/categories/{id}/claim", new ClaimCategoryRequest(token, "Tatami 1"));
        claim.EnsureSuccessStatusCode();
        foreach (var input in new IndividualResultRequest[][]
        {
            [new(firstId, 1, [8m, 8m, 8m])],
            [new(firstId, 1, [8m, 8m, 8m]), new(firstId, 2, [8m, 8m, 8m])],
            [new(firstId, 1, [8m, 8m, 8m]), new(secondId, 1, [8m, 8m, 8m])],
            [new(firstId, 1, [7m, 7m, 7m]), new(secondId, 2, [8m, 8m, 8m])]
        })
        {
            using var invalid = await _client.PostAsJsonAsync($"/api/categories/{id}/individual-results",
                new CompleteIndividualCategoryRequest(token, input));
            Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        }
        var request = new CompleteIndividualCategoryRequest(token,
            [new(firstId, 2, [8.0m, 8.00m, 8m]), new(secondId, 1, [8m, 8m, 8m])]);
        using var completed = await _client.PostAsJsonAsync($"/api/categories/{id}/individual-results", request);
        Assert.Equal(HttpStatusCode.NoContent, completed.StatusCode);
        using var reorderedRetry = await _client.PostAsJsonAsync($"/api/categories/{id}/individual-results",
            request with { Results = [new(secondId, 1, [8.00m, 8m, 8m]), new(firstId, 2, [8m, 8m, 8m])] });
        Assert.Equal(HttpStatusCode.NoContent, reorderedRetry.StatusCode);
    }
}
