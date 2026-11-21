using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using KarateClassLibrary;
using KarateClassLibrary.Contracts;
using Npgsql;
using Xunit;

namespace KarateTournament.Api.Tests;

public partial class TournamentApiIntegrationTests
{
    [Fact]
    public async Task OrganizerRequestsHandoffAndNextStationResumesLatestCheckpoint()
    {
        var first = await CreateOrganizerParticipant("Jan");
        var second = await CreateOrganizerParticipant("Anna");
        var category = await CreateOrganizerCategory([first.Id, second.Id]);
        var path = $"/api/categories/{category.Id}";
        var token = Guid.NewGuid();
        var nextToken = Guid.NewGuid();
        using var claim = await _client.PostAsJsonAsync($"{path}/claim", new ClaimCategoryRequest(token, "Tatami 1"));
        Assert.Equal(HttpStatusCode.NoContent, claim.StatusCode);
        var target = (await _client.GetFromJsonAsync<ClaimRecoveryDto>($"{path}/handoff"))!;
        using var requested = await _client.PostAsJsonAsync($"{path}/handoff", new RecoverCategoryClaimRequest(target.ClaimReference));
        Assert.Equal(HttpStatusCode.NoContent, requested.StatusCode);
        var status = Assert.Single((await _client.GetFromJsonAsync<CategorySummaryDto[]>("/api/categories"))!);
        Assert.True(status.HandoffRequested);
        Assert.True(status.IsClaimed);
        using var prematureClaim = await _client.PostAsJsonAsync($"{path}/claim", new ClaimCategoryRequest(nextToken, "Tatami 2"));
        Assert.Equal(HttpStatusCode.Conflict, prematureClaim.StatusCode);
        Assert.True((await ReadHandoffAsync(path, token)).Requested);
        Assert.False((await ReadHandoffAsync(path, nextToken)).Requested);

        var saved = new JudgingCheckpoint(false, new IndividualJudgeDraft(1, [8.25m],
            [new IndividualDraftScore(first.Id, [7m, 8m, 9m])]), null, null);
        using var invalid = await _client.PostAsJsonAsync($"{path}/handoff/save",
            new SaveJudgingHandoffRequest(token, saved with { Individual = new(1, [11m], []) }));
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        Assert.True((await ReadHandoffAsync(path, token)).Requested);
        using var release = await _client.PostAsJsonAsync($"{path}/handoff/save", new SaveJudgingHandoffRequest(token, saved));
        Assert.Equal(HttpStatusCode.NoContent, release.StatusCode);
        Assert.True((await ReadHandoffAsync(path, token)).Released);
        status = Assert.Single((await _client.GetFromJsonAsync<CategorySummaryDto[]>("/api/categories"))!);
        Assert.False(status.IsClaimed);
        Assert.False(status.HandoffRequested);
        Assert.True(status.HasJudgingCheckpoint);
        Assert.False(status.IsFinished);
        using var delete = await _client.DeleteAsync(path);
        Assert.Equal(HttpStatusCode.Conflict, delete.StatusCode);

        using var next = await _client.PostAsJsonAsync($"{path}/claim", new ClaimCategoryRequest(nextToken, "Tatami 2"));
        Assert.Equal(HttpStatusCode.NoContent, next.StatusCode);
        using var retry = await _client.PostAsJsonAsync($"{path}/handoff/save", new SaveJudgingHandoffRequest(token, saved));
        Assert.Equal(HttpStatusCode.NoContent, retry.StatusCode);
        using var oldRead = await _client.PostAsJsonAsync($"{path}/handoff/checkpoint", new ReleaseCategoryRequest(token));
        Assert.Equal(HttpStatusCode.Conflict, oldRead.StatusCode);
        var checkpoint = await ReadCheckpointAsync(path, nextToken);
        Assert.Equal(first.Id, Assert.Single(checkpoint.Individual!.Results).ParticipantId);
        Assert.Equal(new[] { 7m, 8m, 9m }, checkpoint.Individual.Results[0].Notes);
        Assert.Equal(8.25m, Assert.Single(checkpoint.Individual.CurrentScores));
        Assert.Equal(1, checkpoint.Individual.ParticipantIndex);
        using var completion = await _client.PostAsJsonAsync($"{path}/individual-results",
            new CompleteIndividualCategoryRequest(nextToken, [new(first.Id, 1, [7m, 8m, 9m]), new(second.Id, 2, [7m, 7m, 7m])]));
        Assert.Equal(HttpStatusCode.NoContent, completion.StatusCode);
    }

    [Fact]
    public async Task JudgeKeyCannotRequestOrganizerHandoff()
    {
        var participant = await CreateOrganizerParticipant("Jan");
        var category = await CreateOrganizerCategory([participant.Id]);
        _client.Dispose();
        await _factory.DisposeAsync();
        var organizerKey = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        var judgeKey = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        StartApi(organizerKey: organizerKey, judgeKey: judgeKey);
        var path = $"/api/categories/{category.Id}";
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", judgeKey);
        using var claim = await _client.PostAsJsonAsync($"{path}/claim", new ClaimCategoryRequest(Guid.NewGuid(), "Tatami"));
        Assert.Equal(HttpStatusCode.NoContent, claim.StatusCode);
        using var forbidden = await _client.GetAsync($"{path}/handoff");
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
        using var forbiddenWrite = await _client.PostAsJsonAsync($"{path}/handoff", new RecoverCategoryClaimRequest(new string('0', 64)));
        Assert.Equal(HttpStatusCode.Forbidden, forbiddenWrite.StatusCode);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", organizerKey);
        using var allowed = await _client.GetAsync($"{path}/handoff");
        Assert.Equal(HttpStatusCode.OK, allowed.StatusCode);
    }

    [Fact]
    public async Task SameStationCanReclaimKumiteAndResumePausedMatch()
    {
        var first = await CreateOrganizerParticipant("Jan");
        var second = await CreateOrganizerParticipant("Adam");
        using var added = await _client.PostAsJsonAsync("/api/categories",
            new CreateCategoryRequest("Kumite", null, null, Sex.Unisex, CategoryType.Kumite, [Belts.Kyu8], [first.Id, second.Id]));
        Assert.Equal(HttpStatusCode.Created, added.StatusCode);
        var path = added.Headers.Location!.ToString();
        var previous = Guid.NewGuid();
        var current = Guid.NewGuid();
        using var claim = await _client.PostAsJsonAsync($"{path}/claim", new ClaimCategoryRequest(previous, "Tatami 1"));
        Assert.Equal(HttpStatusCode.NoContent, claim.StatusCode);
        var fight = new MatchDto(1, 0, first.Id, second.Id, null, 2, 1, false, false, 33.5, 1, 0, 0, 1,
            false, true, true, false, false, 0);
        using var saved = await _client.PostAsJsonAsync($"{path}/handoff/save",
            new SaveJudgingHandoffRequest(previous, new JudgingCheckpoint(false, null, [fight], null)));
        Assert.Equal(HttpStatusCode.NoContent, saved.StatusCode);
        using var reclaimed = await _client.PostAsJsonAsync($"{path}/claim", new ClaimCategoryRequest(current, "Tatami 1"));
        Assert.Equal(HttpStatusCode.NoContent, reclaimed.StatusCode);
        var checkpoint = await ReadCheckpointAsync(path, current);
        Assert.Equal(fight, Assert.Single(checkpoint.Bracket!));
    }

    [Fact]
    public async Task StartupAddsHandoffTableWithoutChangingExistingCategories()
    {
        var participant = await CreateOrganizerParticipant("Jan");
        var category = await CreateOrganizerCategory([participant.Id]);
        _client.Dispose();
        await _factory.DisposeAsync();
        await using (var connection = new NpgsqlConnection(_database.GetConnectionString()))
        {
            await connection.OpenAsync();
            await using var previousSchema = new NpgsqlCommand("""DROP TABLE "JudgingProgress" """, connection);
            await previousSchema.ExecuteNonQueryAsync();
        }

        StartApi();

        var restored = (await _client.GetFromJsonAsync<CategoryDto>($"/api/categories/{category.Id}"))!;
        Assert.Equal(category.Name, restored.Name);
        Assert.Equal(participant.Id, Assert.Single(restored.ParticipantIds));
        Assert.False(restored.HasJudgingCheckpoint);
    }

    private async Task<JudgingHandoffState> ReadHandoffAsync(string path, Guid token)
    {
        using var response = await _client.PostAsJsonAsync($"{path}/handoff/status", new ReleaseCategoryRequest(token));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<JudgingHandoffState>())!;
    }

    private async Task<JudgingCheckpoint> ReadCheckpointAsync(string path, Guid token)
    {
        using var response = await _client.PostAsJsonAsync($"{path}/handoff/checkpoint", new ReleaseCategoryRequest(token));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<JudgingCheckpointDto>())!.Checkpoint!;
    }
}
