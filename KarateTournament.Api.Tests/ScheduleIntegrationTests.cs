using System.Net;
using System.Net.Http.Json;
using KarateClassLibrary.Contracts;
using Xunit;

namespace KarateTournament.Api.Tests;

public partial class TournamentApiIntegrationTests
{
    [Fact]
    public async Task SavesPerMatOrderAndLocalTimesAndRejectsAStalePlan()
    {
        var participant = await CreateOrganizerParticipant("Jan");
        var first = await CreateOrganizerCategory([participant.Id]);
        var second = await CreateOrganizerCategory([participant.Id]);
        var third = await CreateOrganizerCategory([participant.Id]);
        var empty = await ReadSchedule();
        Assert.Empty(empty.Mats);
        var start = new DateTime(2026, 11, 23, 10, 30, 0);
        var request = new SaveTournamentScheduleRequest(empty.Version, 2,
            [new(second.Id, 1, start, start.AddMinutes(20)), new(third.Id, 2, null, null), new(first.Id, 1, null, null)]);
        using var invalidTimes = await _client.PutAsJsonAsync("/api/schedule",
            request with { Slots = [new(first.Id, 1, start, start.AddMinutes(-1))] });
        Assert.Equal(HttpStatusCode.BadRequest, invalidTimes.StatusCode);
        using var duplicate = await _client.PutAsJsonAsync("/api/schedule",
            request with { Slots = [new(first.Id, 1, null, null), new(first.Id, 2, null, null)] });
        Assert.Equal(HttpStatusCode.BadRequest, duplicate.StatusCode);
        using var saved = await _client.PutAsJsonAsync("/api/schedule", request);
        Assert.Equal(HttpStatusCode.NoContent, saved.StatusCode);
        using var stale = await _client.PutAsJsonAsync("/api/schedule", request with { Slots = [] });
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        using var plannedDelete = await _client.DeleteAsync($"/api/categories/{first.Id}");
        Assert.Equal(HttpStatusCode.Conflict, plannedDelete.StatusCode);

        _client.Dispose();
        await _factory.DisposeAsync();
        StartApi();
        var plan = await ReadSchedule();
        Assert.Equal(empty.Version + 1, plan.Version);
        Assert.Equal(new[] { second.Id, first.Id }, plan.Mats[0].Slots.Select(slot => slot.CategoryId));
        Assert.Equal(start, plan.Mats[0].Slots[0].StartsAt);
        Assert.Equal(DateTimeKind.Unspecified, plan.Mats[0].Slots[0].StartsAt!.Value.Kind);
        Assert.Equal(start.AddMinutes(20), plan.Mats[0].Slots[0].EndsAt);
        Assert.Equal(third.Id, Assert.Single(plan.Mats[1].Slots).CategoryId);
    }

    [Fact]
    public async Task OnlyMatOwnerCanStartNextCompetitionAndFinishBeforeRelease()
    {
        var participant = await CreateOrganizerParticipant("Jan");
        var first = await CreateOrganizerCategory([participant.Id]);
        var second = await CreateOrganizerCategory([participant.Id]);
        var plan = new SaveTournamentScheduleRequest((await ReadSchedule()).Version, 1,
            [new(first.Id, 1, null, null), new(second.Id, 1, null, null)]);
        using var saved = await _client.PutAsJsonAsync("/api/schedule", plan);
        Assert.Equal(HttpStatusCode.NoContent, saved.StatusCode);
        var mat = Assert.Single((await ReadSchedule()).Mats);
        const string matPath = "/api/schedule/mats/1";
        var attempts = new[]
        {
            new ClaimMatRequest(Guid.NewGuid(), "Stanowisko A", mat.Revision),
            new ClaimMatRequest(Guid.NewGuid(), "Stanowisko B", mat.Revision)
        };
        var responses = await Task.WhenAll(attempts.Select(request => _client.PostAsJsonAsync($"{matPath}/claim", request)));
        var owner = Assert.Single(attempts.Where((_, index) => responses[index].StatusCode == HttpStatusCode.NoContent));
        Assert.Single(responses, response => response.StatusCode == HttpStatusCode.Conflict);
        foreach (var response in responses)
            response.Dispose();

        var categoryClaim = new ClaimCategoryRequest(Guid.NewGuid(), owner.StationName, owner.SessionToken);
        using var withoutMat = await _client.PostAsJsonAsync($"/api/categories/{first.Id}/claim",
            categoryClaim with { MatClaimToken = null });
        Assert.Equal(HttpStatusCode.Conflict, withoutMat.StatusCode);
        using var outOfOrder = await _client.PostAsJsonAsync($"/api/categories/{second.Id}/claim", categoryClaim);
        Assert.Equal(HttpStatusCode.Conflict, outOfOrder.StatusCode);
        using var started = await _client.PostAsJsonAsync($"/api/categories/{first.Id}/claim", categoryClaim);
        Assert.Equal(HttpStatusCode.NoContent, started.StatusCode);
        using var releaseWhileJudging = await _client.PostAsJsonAsync($"{matPath}/release", new ReleaseMatRequest(owner.SessionToken));
        Assert.Equal(HttpStatusCode.Conflict, releaseWhileJudging.StatusCode);
        var activePlan = await ReadSchedule();
        using var moveActive = await _client.PutAsJsonAsync("/api/schedule",
            plan with { Version = activePlan.Version, Slots = [plan.Slots![1], plan.Slots[0]] });
        Assert.Equal(HttpStatusCode.Conflict, moveActive.StatusCode);

        using var finish = await _client.PostAsJsonAsync($"/api/categories/{first.Id}/individual-results",
            new CompleteIndividualCategoryRequest(categoryClaim.SessionToken, [new(participant.Id, 1, [7m, 8m, 9m])]));
        Assert.Equal(HttpStatusCode.NoContent, finish.StatusCode);
        using var released = await _client.PostAsJsonAsync($"{matPath}/release", new ReleaseMatRequest(owner.SessionToken));
        Assert.Equal(HttpStatusCode.NoContent, released.StatusCode);
        using var retryOld = await _client.PostAsJsonAsync($"{matPath}/claim", owner);
        Assert.Equal(HttpStatusCode.Conflict, retryOld.StatusCode);

        var current = Assert.Single((await ReadSchedule()).Mats);
        Assert.True(current.Slots[0].IsFinished);
        var successor = attempts.Single(request => request != owner) with { ExpectedRevision = current.Revision };
        using var matTaken = await _client.PostAsJsonAsync($"{matPath}/claim", successor);
        Assert.Equal(HttpStatusCode.NoContent, matTaken.StatusCode);
        using var next = await _client.PostAsJsonAsync($"/api/categories/{second.Id}/claim",
            new ClaimCategoryRequest(Guid.NewGuid(), successor.StationName, successor.SessionToken));
        Assert.Equal(HttpStatusCode.NoContent, next.StatusCode);
    }

    private async Task<TournamentScheduleDto> ReadSchedule() =>
        (await _client.GetFromJsonAsync<TournamentScheduleDto>("/api/schedule"))!;
}
