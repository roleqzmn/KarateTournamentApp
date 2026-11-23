using System.Net;
using System.Net.Http.Json;
using KarateClassLibrary;
using KarateClassLibrary.Contracts;
using Xunit;

namespace KarateTournament.Api.Tests;

public partial class TournamentApiIntegrationTests
{
    [Fact]
    public async Task ReadsPublishedIndividualAndTeamResultsByParticipantId()
    {
        var participant = await CreateOrganizerParticipant("Jan");
        var teammate = await CreateOrganizerParticipant("Adam");
        var namesake = await CreateOrganizerParticipant("Jan");

        var kata = await CreateOrganizerCategory([participant.Id]);
        var kataToken = await ClaimForHistoryAsync(kata.Id);
        Assert.Empty(await ReadHistoryAsync(participant.Id));
        using var kataResult = await _client.PostAsJsonAsync($"/api/categories/{kata.Id}/individual-results",
            new CompleteIndividualCategoryRequest(kataToken, [new(participant.Id, 1, [6m, 8m, 9m, 10m])]));
        Assert.Equal(HttpStatusCode.NoContent, kataResult.StatusCode);

        var teams = await CreateTeamCategoryAsync();
        using var roster = await _client.PostAsJsonAsync($"/api/categories/{teams.Id}/teams",
            new SaveTeamRequest("Smoki", Sex.Unisex, [participant.Id, teammate.Id]));
        Assert.Equal(HttpStatusCode.Created, roster.StatusCode);
        var team = (await roster.Content.ReadFromJsonAsync<TeamDto>())!;
        var teamToken = await ClaimForHistoryAsync(teams.Id);
        var kataEntry = new ParticipantResultHistoryEntryDto(kata.Id, kata.Name, CategoryType.Kata, 1, 17m, null, null);
        Assert.Equal(kataEntry, Assert.Single(await ReadHistoryAsync(participant.Id)));
        using var teamResult = await _client.PostAsJsonAsync($"/api/categories/{teams.Id}/team-results",
            new CompleteTeamCategoryRequest(teamToken, [new(team.Id, 1, [7m, 8m, 9m])]));
        Assert.Equal(HttpStatusCode.NoContent, teamResult.StatusCode);

        var expected = new List<ParticipantResultHistoryEntryDto> { kataEntry };
        var teamEntry = new ParticipantResultHistoryEntryDto(teams.Id, teams.Name, CategoryType.Team, 1, 24m, team.Id, team.Name);
        expected.Add(teamEntry);
        foreach (var (discipline, route) in new[] { (CategoryType.Kumite, "kumite"), (CategoryType.Grappling, "grappling") })
        {
            using var response = await _client.PostAsJsonAsync("/api/categories",
                new CreateCategoryRequest(discipline.ToString(), null, null, Sex.Unisex, discipline,
                    [Belts.Kyu8], [participant.Id, namesake.Id]));
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            var category = (await response.Content.ReadFromJsonAsync<CategoryDto>())!;
            var token = await ClaimForHistoryAsync(category.Id);
            using var result = await _client.PostAsJsonAsync($"/api/categories/{category.Id}/{route}-results",
                new CompleteKumiteCategoryRequest(token,
                    [new(0, participant.Id, namesake.Id, namesake.Id, 2, 3)], [namesake.Id, participant.Id]));
            Assert.Equal(HttpStatusCode.NoContent, result.StatusCode);
            expected.Add(new(category.Id, category.Name, discipline, 2, null, null, null));
        }

        Assert.Equal(expected.OrderBy(row => row.CategoryName), await ReadHistoryAsync(participant.Id));
        Assert.Equal(teamEntry, Assert.Single(await ReadHistoryAsync(teammate.Id)));
        var namesakeHistory = await ReadHistoryAsync(namesake.Id);
        Assert.Equal(2, namesakeHistory.Length);
        Assert.All(namesakeHistory, row =>
        {
            Assert.True(row.CategoryType.UsesMatchBracket());
            Assert.Equal(1, row.Rank);
            Assert.Null(row.TeamId);
            Assert.Null(row.JudgedScore);
        });
        using var missing = await _client.GetAsync("/api/participants/2147483647/results");
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
    }

    private async Task<Guid> ClaimForHistoryAsync(int categoryId)
    {
        var token = Guid.NewGuid();
        using var claim = await _client.PostAsJsonAsync($"/api/categories/{categoryId}/claim", new ClaimCategoryRequest(token, "Tatami 1"));
        Assert.Equal(HttpStatusCode.NoContent, claim.StatusCode);
        return token;
    }

    private async Task<ParticipantResultHistoryEntryDto[]> ReadHistoryAsync(int id) =>
        (await _client.GetFromJsonAsync<ParticipantResultHistoryEntryDto[]>($"/api/participants/{id}/results"))!;
}
