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
    public async Task OrganizerStoresTeamAsOrderedLinksToExistingParticipants()
    {
        var first = await CreateOrganizerParticipant("Jan");
        var second = await CreateOrganizerParticipant("Adam");
        var category = await CreateTeamCategoryAsync();
        var path = $"/api/categories/{category.Id}/teams";
        var request = new SaveTeamRequest("  Testowa drużyna  ", Sex.Unisex, [first.Id, second.Id]);

        using var create = await _client.PostAsJsonAsync(path, request);
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var created = (await create.Content.ReadFromJsonAsync<TeamDto>())!;
        Assert.Equal("Testowa drużyna", created.Name);
        Assert.Equal(request.MemberParticipantIds, created.MemberParticipantIds);
        var location = create.Headers.Location!;

        using var reorder = await _client.PutAsJsonAsync(location,
            request with { Name = "Nowa nazwa", MemberParticipantIds = [second.Id, first.Id] });
        Assert.Equal(HttpStatusCode.OK, reorder.StatusCode);
        var read = (await _client.GetFromJsonAsync<TeamDto>(location))!;
        Assert.Equal(created.Id, read.Id);
        Assert.Equal("Nowa nazwa", read.Name);
        Assert.Equal(new[] { second.Id, first.Id }, read.MemberParticipantIds);

        using var invalidUpdate = await _client.PutAsJsonAsync(location, request with { MemberParticipantIds = [int.MaxValue] });
        Assert.Equal(HttpStatusCode.BadRequest, invalidUpdate.StatusCode);
        var snapshot = (await _client.GetFromJsonAsync<TournamentSnapshotDto>("/api/tournament"))!;
        Assert.Equal(new[] { first.Id, second.Id }, snapshot.Participants.Select(participant => participant.Id));
        var stored = Assert.Single(snapshot.Categories);
        Assert.Empty(stored.ParticipantIds);
        Assert.Equal(new[] { second.Id, first.Id }, Assert.Single(stored.Teams).MemberParticipantIds);

        using var delete = await _client.DeleteAsync(location);
        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);
        using var missing = await _client.GetAsync(location);
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        snapshot = (await _client.GetFromJsonAsync<TournamentSnapshotDto>("/api/tournament"))!;
        Assert.Equal(2, snapshot.Participants.Count);
        Assert.Empty(Assert.Single(snapshot.Categories).Teams);
    }

    [Fact]
    public async Task RejectsInvalidTeamRostersAndLocksWritesWhenJudgingStarts()
    {
        var participant = await CreateOrganizerParticipant("Jan");
        var teamCategory = await CreateTeamCategoryAsync();
        var individual = await CreateOrganizerCategory([participant.Id]);
        var path = $"/api/categories/{teamCategory.Id}/teams";
        var request = new SaveTeamRequest("Drużyna", Sex.Unisex, [participant.Id]);

        using var duplicates = await _client.PostAsJsonAsync(path, request with { MemberParticipantIds = [participant.Id, participant.Id] });
        Assert.Equal(HttpStatusCode.BadRequest, duplicates.StatusCode);
        using var wrongDiscipline = await _client.PostAsJsonAsync($"/api/categories/{individual.Id}/teams", request);
        Assert.Equal(HttpStatusCode.Conflict, wrongDiscipline.StatusCode);

        using var added = await _client.PostAsJsonAsync(path, request);
        Assert.Equal(HttpStatusCode.Created, added.StatusCode);
        using var claim = await _client.PostAsJsonAsync($"/api/categories/{teamCategory.Id}/claim",
            new ClaimCategoryRequest(Guid.NewGuid(), "Tatami 1"));
        Assert.Equal(HttpStatusCode.NoContent, claim.StatusCode);
        using var addAfterClaim = await _client.PostAsJsonAsync(path, request);
        Assert.Equal(HttpStatusCode.Conflict, addAfterClaim.StatusCode);
        using var editAfterClaim = await _client.PutAsJsonAsync(added.Headers.Location, request);
        Assert.Equal(HttpStatusCode.Conflict, editAfterClaim.StatusCode);
        using var deleteAfterClaim = await _client.DeleteAsync(added.Headers.Location);
        Assert.Equal(HttpStatusCode.Conflict, deleteAfterClaim.StatusCode);
    }

    [Fact]
    public async Task JudgeCanReadTeamsButCannotChangeRosters()
    {
        var participant = await CreateOrganizerParticipant("Jan");
        var category = await CreateTeamCategoryAsync();
        var path = $"/api/categories/{category.Id}/teams";
        var request = new SaveTeamRequest("Drużyna", Sex.Unisex, [participant.Id]);
        using var added = await _client.PostAsJsonAsync(path, request);
        Assert.Equal(HttpStatusCode.Created, added.StatusCode);
        _client.Dispose();
        await _factory.DisposeAsync();
        var judgeKey = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        StartApi(organizerKey: Convert.ToHexString(RandomNumberGenerator.GetBytes(32)), judgeKey: judgeKey);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", judgeKey);

        using var read = await _client.GetAsync(added.Headers.Location);
        Assert.Equal(HttpStatusCode.OK, read.StatusCode);
        using var create = await _client.PostAsJsonAsync(path, request);
        Assert.Equal(HttpStatusCode.Forbidden, create.StatusCode);
        using var update = await _client.PutAsJsonAsync(added.Headers.Location, request);
        Assert.Equal(HttpStatusCode.Forbidden, update.StatusCode);
        using var delete = await _client.DeleteAsync(added.Headers.Location);
        Assert.Equal(HttpStatusCode.Forbidden, delete.StatusCode);
    }

    private async Task<CategoryDto> CreateTeamCategoryAsync()
    {
        using var response = await _client.PostAsJsonAsync("/api/categories",
            new CreateCategoryRequest("Team Kata", null, null, Sex.Unisex, CategoryType.Team, [Belts.Kyu8], []));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<CategoryDto>())!;
    }
}
