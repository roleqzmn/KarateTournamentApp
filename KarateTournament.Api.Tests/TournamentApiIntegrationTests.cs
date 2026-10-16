using System.Net;
using System.Net.Http.Json;
using KarateClassLibrary;
using KarateTournament.Api.Contracts;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Testcontainers.PostgreSql;
using Xunit;

namespace KarateTournament.Api.Tests;

[Trait("Category", "Integration")]
public partial class TournamentApiIntegrationTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _database = new PostgreSqlBuilder("postgres:17-alpine")
        .WithCreateParameterModifier(parameters =>
        {
            foreach (var binding in parameters.HostConfig!.PortBindings["5432/tcp"])
                binding.HostIP = "127.0.0.1";
        })
        .Build();
    private WebApplicationFactory<Program> _factory = null!;
    private HttpClient _client = null!;

    public async Task InitializeAsync()
    {
        await _database.StartAsync();
        StartApi();
    }

    private void StartApi()
    {
        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            builder.UseSetting("ConnectionStrings:TournamentDatabase", _database.GetConnectionString()));
        _client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false
        });
    }

    public async Task DisposeAsync()
    {
        _client?.Dispose();
        if (_factory is not null) await _factory.DisposeAsync();
        await _database.DisposeAsync();
    }

    [Fact]
    public async Task CreatesAndReadsParticipantAndCategoryThroughHttp()
    {
        Assert.Empty((await _client.GetFromJsonAsync<CategorySummaryDto[]>("/api/categories"))!);
        var request = new CreateParticipantRequest(
            " Jan ", " Kowalski ", 12, " ", Belts.Kyu8, Sex.Male, [CategoryType.Kata]);
        using var participantResponse = await _client.PostAsJsonAsync("/api/participants", request);
        Assert.Equal(HttpStatusCode.Created, participantResponse.StatusCode);
        var participant = (await participantResponse.Content.ReadFromJsonAsync<ParticipantDto>())!;
        Assert.True(participant.Id > 0);
        var storedParticipant = (await _client.GetFromJsonAsync<ParticipantDto>(participantResponse.Headers.Location))!;
        Assert.Equal("Jan", storedParticipant.FirstName);
        Assert.Null(storedParticipant.Club);
        Assert.Equal(request.CategoryTypes, storedParticipant.CategoryTypes);

        var categoryRequest = new CreateCategoryRequest(
            " Kata ", 10, 12, Sex.Unisex, CategoryType.Kata, [Belts.Kyu8, Belts.Kyu7], [participant.Id]);
        using var response = await _client.PostAsJsonAsync("/api/categories", categoryRequest);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = (await response.Content.ReadFromJsonAsync<CategoryDto>())!;
        var category = (await _client.GetFromJsonAsync<CategoryDto>(response.Headers.Location))!;
        Assert.Equal(created.Id, category.Id);
        Assert.Equal("Kata", category.Name);
        Assert.Equal(categoryRequest.AllowedBelts, category.AllowedBelts);
        Assert.Equal(categoryRequest.ParticipantIds, category.ParticipantIds);
        var summary = Assert.Single((await _client.GetFromJsonAsync<CategorySummaryDto[]>("/api/categories"))!);
        Assert.Equal(category.Id, summary.Id);
        Assert.Equal(1, summary.ParticipantCount);
        var snapshot = (await _client.GetFromJsonAsync<TournamentSnapshotDto>("/api/tournament"))!;
        Assert.Equal(participant.Id, Assert.Single(snapshot.Participants).Id);
        Assert.Equal(category.Id, Assert.Single(snapshot.Categories).Id);
        using var missing = await _client.GetAsync("/api/categories/2147483647");
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
    }

    [Fact]
    public async Task RejectsInvalidRequestsWithoutPersistingPartialData()
    {
        using var participantResponse = await _client.PostAsJsonAsync("/api/participants",
            new CreateParticipantRequest(" ", "Kowalski", -1, null, Belts.Kyu8, Sex.Male, [CategoryType.Kata]));
        Assert.Equal(HttpStatusCode.BadRequest, participantResponse.StatusCode);
        using var categoryResponse = await _client.PostAsJsonAsync("/api/categories",
            new CreateCategoryRequest("Kata", null, null, Sex.Unisex, CategoryType.Kata, [Belts.Dan], [123]));
        Assert.Equal(HttpStatusCode.BadRequest, categoryResponse.StatusCode);
        var snapshot = (await _client.GetFromJsonAsync<TournamentSnapshotDto>("/api/tournament"))!;
        Assert.Empty(snapshot.Participants);
        Assert.Empty(snapshot.Categories);
    }
}
