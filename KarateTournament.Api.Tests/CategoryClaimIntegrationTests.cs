using System.Net;
using System.Net.Http.Json;
using KarateClassLibrary;
using KarateTournament.Api.Contracts;
using Npgsql;
using Xunit;

namespace KarateTournament.Api.Tests;

public partial class TournamentApiIntegrationTests
{
    private async Task<int> CreateEmptyCategory()
    {
        using var response = await _client.PostAsJsonAsync("/api/categories",
            new CreateCategoryRequest("Kata", null, null, Sex.Unisex, CategoryType.Kata, [Belts.Dan], []));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<CategoryDto>())!.Id;
    }

    [Fact]
    public async Task OnlyOneConcurrentClaimWinsAndOldSessionCannotReleaseNewClaim()
    {
        var id = await CreateEmptyCategory();
        var requests = new[]
        {
            new ClaimCategoryRequest(Guid.NewGuid(), "Tatami 1"),
            new ClaimCategoryRequest(Guid.NewGuid(), "Tatami 2")
        };
        var responses = await Task.WhenAll(requests.Select(request =>
            _client.PostAsJsonAsync($"/api/categories/{id}/claim", request)));
        try
        {
            Assert.Single(responses, response => response.StatusCode == HttpStatusCode.NoContent);
            Assert.Single(responses, response => response.StatusCode == HttpStatusCode.Conflict);
            var winnerIndex = Array.FindIndex(responses, response => response.StatusCode == HttpStatusCode.NoContent);
            var winner = requests[winnerIndex];
            var loser = requests[1 - winnerIndex];
            var summary = Assert.Single((await _client.GetFromJsonAsync<CategorySummaryDto[]>("/api/categories"))!);
            Assert.True(summary.IsClaimed);
            Assert.Equal(winner.StationName, summary.ClaimedBy);
            using var retry = await _client.PostAsJsonAsync($"/api/categories/{id}/claim", winner);
            Assert.Equal(HttpStatusCode.NoContent, retry.StatusCode);
            using var wrongRelease = await _client.PostAsJsonAsync($"/api/categories/{id}/release",
                new ReleaseCategoryRequest(loser.SessionToken));
            Assert.Equal(HttpStatusCode.Conflict, wrongRelease.StatusCode);
            using var release = await _client.PostAsJsonAsync($"/api/categories/{id}/release",
                new ReleaseCategoryRequest(winner.SessionToken));
            Assert.Equal(HttpStatusCode.NoContent, release.StatusCode);
            using var repeatedRelease = await _client.PostAsJsonAsync($"/api/categories/{id}/release",
                new ReleaseCategoryRequest(winner.SessionToken));
            Assert.Equal(HttpStatusCode.NoContent, repeatedRelease.StatusCode);
            Assert.False((await _client.GetFromJsonAsync<CategoryDto>($"/api/categories/{id}"))!.IsClaimed);
            using var nextClaim = await _client.PostAsJsonAsync($"/api/categories/{id}/claim", loser);
            Assert.Equal(HttpStatusCode.NoContent, nextClaim.StatusCode);
            using var staleRelease = await _client.PostAsJsonAsync($"/api/categories/{id}/release",
                new ReleaseCategoryRequest(winner.SessionToken));
            Assert.Equal(HttpStatusCode.Conflict, staleRelease.StatusCode);
        }
        finally { foreach (var response in responses) response.Dispose(); }
    }

    [Fact]
    public async Task ClaimSurvivesApiRestartAndReadEndpointsDoNotExposeToken()
    {
        var id = await CreateEmptyCategory();
        var request = new ClaimCategoryRequest(Guid.NewGuid(), " Tatami 1 ");
        using var claim = await _client.PostAsJsonAsync($"/api/categories/{id}/claim", request);
        Assert.Equal(HttpStatusCode.NoContent, claim.StatusCode);
        _client.Dispose();
        await _factory.DisposeAsync();
        StartApi();
        using var otherClaim = await _client.PostAsJsonAsync($"/api/categories/{id}/claim",
            new ClaimCategoryRequest(Guid.NewGuid(), "Tatami 2"));
        Assert.Equal(HttpStatusCode.Conflict, otherClaim.StatusCode);
        var category = (await _client.GetFromJsonAsync<CategoryDto>($"/api/categories/{id}"))!;
        Assert.True(category.IsClaimed);
        Assert.Equal("Tatami 1", category.ClaimedBy);
        foreach (var path in new[] { "/api/categories", $"/api/categories/{id}", "/api/tournament" })
            Assert.DoesNotContain(request.SessionToken.ToString(), await _client.GetStringAsync(path));
    }

    [Fact]
    public async Task RejectsEmptyTokenAndInvalidStationAndReturns404ForMissingCategory()
    {
        var id = await CreateEmptyCategory();
        foreach (var request in new[]
        {
            new ClaimCategoryRequest(Guid.Empty, "Tatami 1"),
            new ClaimCategoryRequest(Guid.NewGuid(), null),
            new ClaimCategoryRequest(Guid.NewGuid(), " "),
            new ClaimCategoryRequest(Guid.NewGuid(), new string('x', 101))
        })
        {
            using var response = await _client.PostAsJsonAsync($"/api/categories/{id}/claim", request);
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }
        using var missing = await _client.PostAsJsonAsync("/api/categories/2147483647/claim",
            new ClaimCategoryRequest(Guid.NewGuid(), "Tatami 1"));
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        using var missingRelease = await _client.PostAsJsonAsync("/api/categories/2147483647/release",
            new ReleaseCategoryRequest(Guid.NewGuid()));
        Assert.Equal(HttpStatusCode.NotFound, missingRelease.StatusCode);
        using var emptyRelease = await _client.PostAsJsonAsync($"/api/categories/{id}/release",
            new ReleaseCategoryRequest(Guid.Empty));
        Assert.Equal(HttpStatusCode.BadRequest, emptyRelease.StatusCode);
        Assert.False((await _client.GetFromJsonAsync<CategoryDto>($"/api/categories/{id}"))!.IsClaimed);
    }

    [Fact]
    public async Task ExplicitSchemaUpgradePreservesExistingCategoryAndCanBeRepeated()
    {
        var id = await CreateEmptyCategory();
        await using var connection = new NpgsqlConnection(_database.GetConnectionString());
        await connection.OpenAsync();
        // Recreate the previous schema only inside this test's disposable database.
        await using (var oldSchema = new NpgsqlCommand(
            "ALTER TABLE \"Categories\" DROP COLUMN \"ClaimToken\", DROP COLUMN \"ClaimedBy\"", connection))
            await oldSchema.ExecuteNonQueryAsync();
        var sql = await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "add-category-claims.sql"));
        for (var attempt = 0; attempt < 2; attempt++)
        {
            await using var upgrade = new NpgsqlCommand(sql, connection);
            await upgrade.ExecuteNonQueryAsync();
        }
        var category = (await _client.GetFromJsonAsync<CategoryDto>($"/api/categories/{id}"))!;
        Assert.Equal("Kata", category.Name);
        Assert.False(category.IsClaimed);
        await using (var finish = new NpgsqlCommand(
            "UPDATE \"Categories\" SET \"IsFinished\" = TRUE WHERE \"Id\" = @id", connection))
        {
            finish.Parameters.AddWithValue("id", id);
            await finish.ExecuteNonQueryAsync();
        }
        using var claim = await _client.PostAsJsonAsync($"/api/categories/{id}/claim",
            new ClaimCategoryRequest(Guid.NewGuid(), "Tatami 1"));
        Assert.Equal(HttpStatusCode.Conflict, claim.StatusCode);
    }
}
