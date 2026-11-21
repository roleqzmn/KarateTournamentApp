using System.Net;
using System.Net.Http.Json;
using KarateClassLibrary;
using KarateClassLibrary.Contracts;
using Xunit;

namespace KarateTournament.Api.Tests;

public partial class TournamentApiIntegrationTests
{
    [Fact]
    public async Task RegistrationAndBatchReuseCategoriesAndPreserveRosterOrder()
    {
        const string grouping = "?groupBy=AgeAndBelt";
        var first = await RegisterAsync(Person("Jan", disciplines: [CategoryType.Kata, CategoryType.Kumite]), grouping);
        using var import = await _client.PostAsJsonAsync("/api/participants/batch" + grouping, new[]
        {
            Person("Adam"),
            Person("Anna", sex: Sex.Female),
            Person("Paweł", belt: Belts.Kyu7),
            Person("Piotr", age: 18),
            Person("Marek", age: 34, belt: Belts.Dan)
        });
        Assert.Equal(HttpStatusCode.Created, import.StatusCode);
        var people = (await import.Content.ReadFromJsonAsync<ParticipantDto[]>())!;
        var snapshot = (await _client.GetFromJsonAsync<TournamentSnapshotDto>("/api/tournament"))!;
        Assert.Equal(5, snapshot.Categories.Count);

        var sharedKata = Assert.Single(snapshot.Categories, category => category.ParticipantIds.Contains(people[0].Id));
        Assert.Equal(CategoryType.Kata, sharedKata.CategoryType);
        Assert.Equal(new[] { first.Id, people[0].Id }, sharedKata.ParticipantIds);
        Assert.Equal(12, sharedKata.MinAge);
        Assert.Equal(12, sharedKata.MaxAge);
        Assert.Equal(Belts.Kyu8, Assert.Single(sharedKata.AllowedBelts));
        Assert.Equal(first.Id, Assert.Single(Assert.Single(snapshot.Categories,
            category => category.CategoryType == CategoryType.Kumite).ParticipantIds));
        var seniors = Assert.Single(snapshot.Categories, category => category.MinAge == 18);
        Assert.Equal(new[] { people[3].Id, people[4].Id }, seniors.ParticipantIds);
        Assert.Equal(Enum.GetValues<Belts>(), seniors.AllowedBelts);
    }

    [Fact]
    public async Task RegistrationLeavesClaimedAndCompletedRostersAlone()
    {
        const string grouping = "?groupBy=Age";
        var first = await RegisterAsync(Person("Jan"), grouping);
        var initial = Assert.Single((await _client.GetFromJsonAsync<CategorySummaryDto[]>("/api/categories"))!);
        var token = Guid.NewGuid();
        using var claim = await _client.PostAsJsonAsync($"/api/categories/{initial.Id}/claim",
            new ClaimCategoryRequest(token, "Tatami"));
        Assert.Equal(HttpStatusCode.NoContent, claim.StatusCode);

        var second = await RegisterAsync(Person("Adam", belt: Belts.Kyu7), grouping);
        using var finish = await _client.PostAsJsonAsync($"/api/categories/{initial.Id}/individual-results",
            new CompleteIndividualCategoryRequest(token, [new(first.Id, 1, [7m, 8m, 9m])]));
        Assert.Equal(HttpStatusCode.NoContent, finish.StatusCode);
        var third = await RegisterAsync(Person("Marek"), grouping);

        var snapshot = (await _client.GetFromJsonAsync<TournamentSnapshotDto>("/api/tournament"))!;
        Assert.Equal(2, snapshot.Categories.Count);
        var completed = Assert.Single(snapshot.Categories, category => category.IsFinished);
        Assert.Equal(first.Id, Assert.Single(completed.ParticipantIds));
        var open = Assert.Single(snapshot.Categories, category => !category.IsFinished);
        Assert.Equal(new[] { second.Id, third.Id }, open.ParticipantIds);
    }

    [Fact]
    public async Task ParallelRegistrationsInBeltModeCreateOneCategoryAndKeepAllRosterEntries()
    {
        const string grouping = "?groupBy=Belt";
        var people = await Task.WhenAll(
            RegisterAsync(Person("Jan", age: 11), grouping),
            RegisterAsync(Person("Adam", age: 12), grouping),
            RegisterAsync(Person("Marek", age: 13), grouping));

        var snapshot = (await _client.GetFromJsonAsync<TournamentSnapshotDto>("/api/tournament"))!;
        var category = Assert.Single(snapshot.Categories);
        Assert.Equal(1, category.MinAge);
        Assert.Equal(17, category.MaxAge);
        Assert.Equal(people.Select(person => person.Id).Order(), category.ParticipantIds.Order());
    }

    [Fact]
    public async Task InvalidAutomaticImportDoesNotSaveAnyParticipantsOrCategories()
    {
        using var invalidGrouping = await _client.PostAsJsonAsync("/api/participants?groupBy=99", Person("Jan"));
        Assert.Equal(HttpStatusCode.BadRequest, invalidGrouping.StatusCode);
        using var import = await _client.PostAsJsonAsync("/api/participants/batch?groupBy=AgeAndBelt",
            new[] { Person("Jan"), Person(" ") });
        Assert.Equal(HttpStatusCode.BadRequest, import.StatusCode);
        var snapshot = (await _client.GetFromJsonAsync<TournamentSnapshotDto>("/api/tournament"))!;
        Assert.Empty(snapshot.Participants);
        Assert.Empty(snapshot.Categories);
    }

    private static CreateParticipantRequest Person(string name, int age = 12, Belts belt = Belts.Kyu8,
        Sex sex = Sex.Male, CategoryType[]? disciplines = null) =>
        new(name, "Nowak", age, null, belt, sex, disciplines ?? [CategoryType.Kata]);

    private async Task<ParticipantDto> RegisterAsync(CreateParticipantRequest request, string grouping)
    {
        using var response = await _client.PostAsJsonAsync("/api/participants" + grouping, request);
        Assert.True(response.StatusCode == HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<ParticipantDto>())!;
    }
}
