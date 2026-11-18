using System.Net;
using System.Net.Http.Json;
using KarateClassLibrary;
using KarateTournament.Api.Contracts;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Xunit;

namespace KarateTournament.Api.Tests;

public partial class TournamentApiIntegrationTests
{
    [Fact]
    public async Task UpdatesParticipantProfileWithoutChangingCategoryMembership()
    {
        var participant = await CreateOrganizerParticipant("Jan");
        var category = await CreateOrganizerCategory([participant.Id]);
        var path = $"/api/participants/{participant.Id}";
        var request = new CreateParticipantRequest(
            " Janusz ", " Nowak ", 13, " Klub ", Belts.Kyu7, Sex.Male, [CategoryType.Kata, CategoryType.Kumite]);

        using var response = await _client.PutAsJsonAsync(path, request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var stored = (await _client.GetFromJsonAsync<ParticipantDto>(path))!;
        Assert.Equal(participant.Id, stored.Id);
        Assert.Equal("Janusz", stored.FirstName);
        Assert.Equal("Nowak", stored.LastName);
        Assert.Equal(13, stored.Age);
        Assert.Equal("Klub", stored.Club);
        Assert.Equal(Belts.Kyu7, stored.Belt);
        Assert.Equal(request.CategoryTypes, stored.CategoryTypes);
        var storedCategory = (await _client.GetFromJsonAsync<CategoryDto>($"/api/categories/{category.Id}"))!;
        Assert.Equal(participant.Id, Assert.Single(storedCategory.ParticipantIds));

        using var invalid = await _client.PutAsJsonAsync(path, request with { FirstName = " " });
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        using var missing = await _client.PutAsJsonAsync("/api/participants/2147483647", request);
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
    }

    [Fact]
    public async Task ReplacesOrderedCategoryLinksAndRejectsUnknownParticipant()
    {
        var first = await CreateOrganizerParticipant("Jan");
        var second = await CreateOrganizerParticipant("Anna");
        var category = await CreateOrganizerCategory([first.Id, second.Id]);
        var path = $"/api/categories/{category.Id}";
        var request = new CreateCategoryRequest(
            " Kata Junior ", 11, 15, Sex.Unisex, CategoryType.Kata, [Belts.Kyu7, Belts.Kyu8], [second.Id, first.Id]);

        using var response = await _client.PutAsJsonAsync(path, request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = (await response.Content.ReadFromJsonAsync<CategoryDto>())!;
        Assert.Equal(request.ParticipantIds, updated.ParticipantIds);
        Assert.Equal(request.AllowedBelts, updated.AllowedBelts);

        using var invalid = await _client.PutAsJsonAsync(path,
            request with { Name = "Should not save", ParticipantIds = [int.MaxValue] });
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        using var missing = await _client.PutAsJsonAsync("/api/categories/2147483647", request);
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);

        var stored = (await _client.GetFromJsonAsync<CategoryDto>(path))!;
        Assert.Equal("Kata Junior", stored.Name);
        Assert.Equal(11, stored.MinAge);
        Assert.Equal(15, stored.MaxAge);
        Assert.Equal(request.ParticipantIds, stored.ParticipantIds);
        Assert.Equal(request.AllowedBelts, stored.AllowedBelts);

        using var clear = await _client.PutAsJsonAsync(path, request with { ParticipantIds = [] });
        Assert.Equal(HttpStatusCode.OK, clear.StatusCode);
        Assert.Empty((await _client.GetFromJsonAsync<CategoryDto>(path))!.ParticipantIds);
    }

    [Fact]
    public async Task CategoryEditWaitingForClaimSeesCommittedOwnerAndReturnsConflict()
    {
        var category = await CreateOrganizerCategory([]);
        var token = Guid.NewGuid();
        var path = $"/api/categories/{category.Id}";
        var request = new CreateCategoryRequest("New name", null, null, Sex.Unisex, CategoryType.Kata, [Belts.Dan], []);
        await using var owner = new NpgsqlConnection(_database.GetConnectionString());
        await using var observer = new NpgsqlConnection(_database.GetConnectionString());
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        await owner.OpenAsync(timeout.Token);
        await observer.OpenAsync(timeout.Token);
        await using var transaction = await owner.BeginTransactionAsync(timeout.Token);
        // Hold the same row update as the claim endpoint, uncommitted until PUT is waiting.
        await using var claim = new NpgsqlCommand(
            """UPDATE "Categories" SET "ClaimToken" = @token, "ClaimedBy" = 'Tatami 1' WHERE "Id" = @id""",
            owner, transaction);
        claim.Parameters.AddWithValue("token", token);
        claim.Parameters.AddWithValue("id", category.Id);
        Assert.Equal(1, await claim.ExecuteNonQueryAsync(timeout.Token));

        var update = _client.PutAsJsonAsync(path, request, timeout.Token);
        await using var blocked = new NpgsqlCommand(
            "SELECT EXISTS (SELECT 1 FROM pg_stat_activity WHERE @owner = ANY(pg_blocking_pids(pid)))", observer);
        blocked.Parameters.AddWithValue("owner", owner.ProcessID);
        while (!update.IsCompleted && !(bool)(await blocked.ExecuteScalarAsync(timeout.Token))!)
            await Task.Delay(25, timeout.Token);
        Assert.False(update.IsCompleted, "The edit should wait for the transaction holding the category row.");
        await transaction.CommitAsync(timeout.Token);
        using var response = await update;
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);

        var stored = (await _client.GetFromJsonAsync<CategoryDto>(path))!;
        Assert.Equal(category.Name, stored.Name);
        Assert.Equal(category.AllowedBelts, stored.AllowedBelts);
        Assert.True(stored.IsClaimed);
        using var release = await _client.PostAsJsonAsync($"{path}/release", new ReleaseCategoryRequest(token));
        Assert.Equal(HttpStatusCode.NoContent, release.StatusCode);
        using var retry = await _client.PutAsJsonAsync(path, request);
        Assert.Equal(HttpStatusCode.OK, retry.StatusCode);
    }

    [Fact]
    public async Task FailedCategoryEditRestoresPreviouslyStoredLinks()
    {
        var participant = await CreateOrganizerParticipant("Jan");
        var category = await CreateOrganizerCategory([participant.Id]);
        var path = $"/api/categories/{category.Id}";
        await using var connection = new NpgsqlConnection(_database.GetConnectionString());
        await connection.OpenAsync();
        // Force an insert failure in the disposable database after both relation deletes ran.
        await using var constraint = new NpgsqlCommand(
            """ALTER TABLE "CategoryAllowedBelt" ADD CONSTRAINT reject_dan CHECK ("Belt" <> 0)""", connection);
        await constraint.ExecuteNonQueryAsync();
        try
        {
            using var failed = await _client.PutAsJsonAsync(path,
                new CreateCategoryRequest("New name", null, null, Sex.Unisex, CategoryType.Kata, [Belts.Dan], []));
            Assert.Equal(HttpStatusCode.InternalServerError, failed.StatusCode);
        }
        catch (DbUpdateException exception)
        {
            Assert.Equal("reject_dan", Assert.IsType<PostgresException>(exception.InnerException).ConstraintName);
        }

        var stored = (await _client.GetFromJsonAsync<CategoryDto>(path))!;
        Assert.Equal(category.Name, stored.Name);
        Assert.Equal(category.ParticipantIds, stored.ParticipantIds);
        Assert.Equal(category.AllowedBelts, stored.AllowedBelts);
    }

    [Fact]
    public async Task FinishedCategoryCannotBeEdited()
    {
        var (id, participantId, token) = await PrepareScoredCategory();
        var path = $"/api/categories/{id}";
        using var completed = await _client.PostAsJsonAsync($"{path}/individual-results",
            new CompleteIndividualCategoryRequest(token, [new(participantId, 1, [8m, 8m, 8m])]));
        Assert.Equal(HttpStatusCode.NoContent, completed.StatusCode);

        using var update = await _client.PutAsJsonAsync(path,
            new CreateCategoryRequest("New name", null, null, Sex.Unisex, CategoryType.Kata, [Belts.Dan], []));
        Assert.Equal(HttpStatusCode.Conflict, update.StatusCode);
        var stored = (await _client.GetFromJsonAsync<CategoryDto>(path))!;
        Assert.True(stored.IsFinished);
        Assert.Equal(participantId, Assert.Single(stored.FinalResults).ParticipantId);
    }

    private async Task<ParticipantDto> CreateOrganizerParticipant(string firstName)
    {
        using var response = await _client.PostAsJsonAsync("/api/participants",
            new CreateParticipantRequest(firstName, "Nowak", 12, null, Belts.Kyu8, Sex.Unisex, [CategoryType.Kata]));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<ParticipantDto>())!;
    }

    private async Task<CategoryDto> CreateOrganizerCategory(int[] participantIds)
    {
        using var response = await _client.PostAsJsonAsync("/api/categories",
            new CreateCategoryRequest("Kata", 10, 14, Sex.Unisex, CategoryType.Kata, [Belts.Kyu8, Belts.Kyu7], participantIds));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<CategoryDto>())!;
    }
}
