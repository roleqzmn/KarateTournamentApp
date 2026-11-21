using System.Net;
using System.Net.Http.Json;
using KarateClassLibrary;
using KarateClassLibrary.Contracts;
using Npgsql;
using Xunit;

namespace KarateTournament.Api.Tests;

public partial class TournamentApiIntegrationTests
{
    [Fact]
    public async Task PublishesTeamRankingUsingKataScoresAndKeepsMemberIdentity()
    {
        var (path, first, second, memberId) = await PrepareTeamsAsync();
        var token = Guid.NewGuid();
        using var claim = await _client.PostAsJsonAsync($"{path}/claim", new ClaimCategoryRequest(token, "Tatami 1"));
        Assert.Equal(HttpStatusCode.NoContent, claim.StatusCode);
        var valid = new CompleteTeamCategoryRequest(token,
            [new(first.Id, 1, [6m, 8m, 9m, 10m]), new(second.Id, 2, [7m, 8m, 7m, 8m])]);
        using var incomplete = await _client.PostAsJsonAsync($"{path}/team-results", valid with { Results = [valid.Results![0]] });
        Assert.Equal(HttpStatusCode.BadRequest, incomplete.StatusCode);
        using var wrongSession = await _client.PostAsJsonAsync($"{path}/team-results", valid with { SessionToken = Guid.NewGuid() });
        Assert.Equal(HttpStatusCode.Conflict, wrongSession.StatusCode);
        using var individual = await _client.PostAsJsonAsync($"{path}/individual-results",
            new CompleteIndividualCategoryRequest(token, [new(memberId, 1, [8m, 8m, 8m])]));
        Assert.Equal(HttpStatusCode.BadRequest, individual.StatusCode);

        var outcomes = await Task.WhenAll(_client.PostAsJsonAsync($"{path}/team-results", valid),
            _client.PostAsJsonAsync($"{path}/team-results", valid));
        foreach (var outcome in outcomes)
        {
            using (outcome)
                Assert.Equal(HttpStatusCode.NoContent, outcome.StatusCode);
        }
        using var different = await _client.PostAsJsonAsync($"{path}/team-results", valid with
        {
            Results = [valid.Results![0] with { JudgeScores = [8m, 8m, 8m] }, valid.Results[1]]
        });
        Assert.Equal(HttpStatusCode.Conflict, different.StatusCode);

        var category = (await _client.GetFromJsonAsync<CategoryDto>(path))!;
        Assert.True(category.IsFinished);
        Assert.False(category.IsClaimed);
        Assert.Empty(category.FinalResults);
        var results = category.TeamResults!;
        Assert.Equal(new[] { first.Id, second.Id }, results.Select(result => result.TeamId));
        Assert.Equal(17m, results[0].Score);
        Assert.Equal(new[] { 0, 3 }, results[0].JudgeScores.Where(note => note.IsDiscarded).Select(note => note.Ordinal));
        Assert.Equal(memberId, Assert.Single(category.Teams.Single(team => team.Id == results[0].TeamId).MemberParticipantIds));
    }

    [Fact]
    public async Task TeamHandoffRestoresCompletedAndCurrentNotesByTeamId()
    {
        var (path, first, _, _) = await PrepareTeamsAsync();
        var previous = Guid.NewGuid();
        using var claim = await _client.PostAsJsonAsync($"{path}/claim", new ClaimCategoryRequest(previous, "Tatami 1"));
        Assert.Equal(HttpStatusCode.NoContent, claim.StatusCode);
        var draft = new TeamJudgeDraft(1, [8.25m], [new(first.Id, [7m, 8m, 9m])]);
        var checkpoint = new JudgingCheckpoint(false, null, null, null, Team: draft);
        using var invalid = await _client.PostAsJsonAsync($"{path}/handoff/save",
            new SaveJudgingHandoffRequest(previous, checkpoint with { Team = draft with { Results = [new(int.MaxValue, [7m, 8m, 9m])] } }));
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        using var handoff = await _client.PostAsJsonAsync($"{path}/handoff/save", new SaveJudgingHandoffRequest(previous, checkpoint));
        Assert.Equal(HttpStatusCode.NoContent, handoff.StatusCode);
        var next = Guid.NewGuid();
        using var reclaim = await _client.PostAsJsonAsync($"{path}/claim", new ClaimCategoryRequest(next, "Tatami 2"));
        Assert.Equal(HttpStatusCode.NoContent, reclaim.StatusCode);

        var resumed = (await ReadCheckpointAsync(path, next)).Team!;
        Assert.Equal(1, resumed.TeamIndex);
        Assert.Equal(8.25m, Assert.Single(resumed.CurrentScores));
        Assert.Equal(first.Id, Assert.Single(resumed.Results).TeamId);
        Assert.Equal(new[] { 7m, 8m, 9m }, resumed.Results[0].Notes);
    }

    [Fact]
    public async Task StartupAddsTeamScoreTablesAndPreservesExistingTeams()
    {
        var (path, first, _, memberId) = await PrepareTeamsAsync();
        _client.Dispose();
        await _factory.DisposeAsync();
        await using (var connection = new NpgsqlConnection(_database.GetConnectionString()))
        {
            await connection.OpenAsync();
            await using var oldSchema = new NpgsqlCommand("""DROP TABLE "TeamJudgeScores", "TeamResults" """, connection);
            await oldSchema.ExecuteNonQueryAsync();
        }

        StartApi();
        var category = (await _client.GetFromJsonAsync<CategoryDto>(path))!;
        Assert.Empty(category.TeamResults!);
        Assert.Equal(memberId, Assert.Single(category.Teams.Single(team => team.Id == first.Id).MemberParticipantIds));
    }

    private async Task<(string Path, TeamDto First, TeamDto Second, int MemberId)> PrepareTeamsAsync()
    {
        var first = await CreateOrganizerParticipant("Jan");
        var second = await CreateOrganizerParticipant("Adam");
        var category = await CreateTeamCategoryAsync();
        var path = $"/api/categories/{category.Id}";
        async Task<TeamDto> Add(string name, int id)
        {
            using var response = await _client.PostAsJsonAsync($"{path}/teams", new SaveTeamRequest(name, Sex.Unisex, [id]));
            response.EnsureSuccessStatusCode();
            return (await response.Content.ReadFromJsonAsync<TeamDto>())!;
        }
        return (path, await Add("AKA", first.Id), await Add("SHIRO", second.Id), first.Id);
    }
}
