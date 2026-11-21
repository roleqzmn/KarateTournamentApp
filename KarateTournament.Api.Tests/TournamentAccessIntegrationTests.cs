using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using KarateClassLibrary;
using KarateClassLibrary.Contracts;
using Xunit;

namespace KarateTournament.Api.Tests;

public partial class TournamentApiIntegrationTests
{
    [Fact]
    public async Task TwoIndependentStationsCanSeeTheCompletedResultsAndOnlyOneCanClaim()
    {
        var participant = await CreateOrganizerParticipant("Jan");
        var category = await CreateOrganizerCategory([participant.Id]);
        var judges = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        var organizers = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        _client.Dispose();
        await _factory.DisposeAsync();
        StartApi(organizerKey: organizers, judgeKey: judges);
        using var organizer = _factory.CreateClient(new() { BaseAddress = new Uri("https://localhost") });
        organizer.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", organizers);
        using var secondStation = _factory.CreateClient(new() { BaseAddress = new Uri("https://localhost") });
        var clients = new[] { _client, secondStation };
        foreach (var client in clients)
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", judges);
        var tokens = new[] { Guid.NewGuid(), Guid.NewGuid() };
        var path = $"/api/categories/{category.Id}";

        async Task<CategorySummaryDto> ReadOrganizerSummaryAsync()
        {
            var summaries = await organizer.GetFromJsonAsync<CategorySummaryDto[]>("/api/categories");
            return Assert.Single(summaries!);
        }

        var open = await ReadOrganizerSummaryAsync();
        Assert.False(open.IsClaimed);
        Assert.False(open.IsFinished);

        var claims = await Task.WhenAll(clients.Select((client, index) =>
            client.PostAsJsonAsync($"{path}/claim", new ClaimCategoryRequest(tokens[index], $"Tatami {index}"))));
        try
        {
            Assert.Single(claims, response => response.StatusCode == HttpStatusCode.NoContent);
            Assert.Single(claims, response => response.StatusCode == HttpStatusCode.Conflict);
            var winner = Array.FindIndex(claims, response => response.StatusCode == HttpStatusCode.NoContent);
            var inProgress = await ReadOrganizerSummaryAsync();
            Assert.True(inProgress.IsClaimed);
            Assert.False(inProgress.IsFinished);
            Assert.Equal($"Tatami {winner}", inProgress.ClaimedBy);
            var request = new CompleteIndividualCategoryRequest(tokens[winner], [new(participant.Id, 1, [7m, 8m, 9m])]);
            using var submitted = await clients[winner].PostAsJsonAsync($"{path}/individual-results", request);
            Assert.Equal(HttpStatusCode.NoContent, submitted.StatusCode);
            using var retry = await clients[winner].PostAsJsonAsync($"{path}/individual-results", request);
            Assert.Equal(HttpStatusCode.NoContent, retry.StatusCode);
            var stored = await clients[1 - winner].GetFromJsonAsync<CategoryDto>(path);
            Assert.True(stored!.IsFinished);
            Assert.Equal(24m, Assert.Single(stored.FinalResults).Score);
            var completed = await ReadOrganizerSummaryAsync();
            Assert.True(completed.IsFinished);
            Assert.False(completed.IsClaimed);
            Assert.Null(completed.ClaimedBy);
            var ranking = await organizer.GetFromJsonAsync<CategoryDto>(path);
            Assert.Equal(1, Assert.Single(ranking!.FinalResults).Rank);
        }
        finally { foreach (var response in claims) response.Dispose(); }
    }

    [Fact]
    public async Task ConfiguredAccessSeparatesOrganizerWritesFromJudgingAndRequiresHttps()
    {
        var category = await CreateOrganizerCategory([]);
        var organizers = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        var judges = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        var recovery = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        _client.Dispose();
        await _factory.DisposeAsync();
        StartApi(recovery, organizers, judges);

        using var uncredentialed = await _client.GetAsync("/api/tournament");
        Assert.Equal(HttpStatusCode.Unauthorized, uncredentialed.StatusCode);
        using var health = await _client.GetAsync("/health/database");
        Assert.Equal(HttpStatusCode.OK, health.StatusCode);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", judges);
        using var plaintext = await _client.GetAsync("http://localhost/api/tournament");
        Assert.Equal(HttpStatusCode.BadRequest, plaintext.StatusCode);
        using var read = await _client.GetAsync("/api/tournament");
        Assert.Equal(HttpStatusCode.OK, read.StatusCode);

        var profile = new CreateParticipantRequest("Jan", "Nowak", 12, null, Belts.Kyu8, Sex.Male, [CategoryType.Kata]);
        using var forbiddenRegister = await _client.PostAsJsonAsync("/api/participants", profile);
        Assert.Equal(HttpStatusCode.Forbidden, forbiddenRegister.StatusCode);
        using var forbiddenBatch = await _client.PostAsJsonAsync("/api/participants/batch", new[] { profile });
        Assert.Equal(HttpStatusCode.Forbidden, forbiddenBatch.StatusCode);
        using var forbiddenDelete = await _client.DeleteAsync($"/api/categories/{category.Id}");
        Assert.Equal(HttpStatusCode.Forbidden, forbiddenDelete.StatusCode);
        var token = Guid.NewGuid();
        using var claim = await _client.PostAsJsonAsync($"/api/categories/{category.Id}/claim", new ClaimCategoryRequest(token, "Tatami"));
        Assert.Equal(HttpStatusCode.NoContent, claim.StatusCode);
        using var forbiddenRecovery = await _client.GetAsync($"/api/categories/{category.Id}/claim-recovery");
        Assert.Equal(HttpStatusCode.Unauthorized, forbiddenRecovery.StatusCode);

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", organizers);
        using var registered = await _client.PostAsJsonAsync("/api/participants", profile);
        Assert.Equal(HttpStatusCode.Created, registered.StatusCode);
        using var recoveryRequest = new HttpRequestMessage(HttpMethod.Get, $"/api/categories/{category.Id}/claim-recovery");
        recoveryRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", recovery);
        using var recoveryTarget = await _client.SendAsync(recoveryRequest);
        Assert.Equal(HttpStatusCode.OK, recoveryTarget.StatusCode);
    }
}
