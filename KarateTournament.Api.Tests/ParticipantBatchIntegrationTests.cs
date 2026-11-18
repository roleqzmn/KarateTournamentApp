using System.Net;
using System.Net.Http.Json;
using KarateClassLibrary;
using KarateClassLibrary.Contracts;
using Xunit;

namespace KarateTournament.Api.Tests;

public partial class TournamentApiIntegrationTests
{
    [Fact]
    public async Task BatchRejectsEveryRowIfOneIsInvalidAndReturnsDatabaseIdsOnSuccess()
    {
        var valid = new CreateParticipantRequest("Anna", "Nowak", 12, null, Belts.Kyu8, Sex.Female, [CategoryType.Kata]);
        using var invalid = await _client.PostAsJsonAsync("/api/participants/batch", new[] { valid, valid with { Age = -1 } });
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        Assert.Contains("rows[2].Age", await invalid.Content.ReadAsStringAsync());
        var empty = await _client.GetFromJsonAsync<TournamentSnapshotDto>("/api/tournament");
        Assert.Empty(empty!.Participants);

        using var response = await _client.PostAsJsonAsync("/api/participants/batch", new[] { valid, valid with { FirstName = "Maria" } });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var saved = (await response.Content.ReadFromJsonAsync<ParticipantDto[]>())!;
        Assert.Equal(2, saved.Select(person => person.Id).Distinct().Count());
        var stored = (await _client.GetFromJsonAsync<TournamentSnapshotDto>("/api/tournament"))!;
        Assert.Equal(saved.Select(person => person.Id).Order(), stored.Participants.Select(person => person.Id));
        Assert.Empty(stored.Categories);
    }
}
