using System.Net;
using System.Net.Http.Json;
using KarateClassLibrary;
using KarateClassLibrary.Contracts;
using KarateTournament.Api.Contracts;
using Npgsql;
using Xunit;

namespace KarateTournament.Api.Tests;

public partial class TournamentApiIntegrationTests
{
    [Fact]
    public async Task DeleteCategoryRemovesItsRosterButKeepsTheParticipants()
    {
        var participant = await CreateOrganizerParticipant("Jan");
        var category = await CreateOrganizerCategory([participant.Id]);
        var participantPath = $"/api/participants/{participant.Id}";
        var categoryPath = $"/api/categories/{category.Id}";

        using var used = await _client.DeleteAsync(participantPath);
        Assert.Equal(HttpStatusCode.Conflict, used.StatusCode);
        var roster = await _client.GetFromJsonAsync<CategoryDto>(categoryPath);
        Assert.Equal(participant.Id, Assert.Single(roster!.ParticipantIds));

        using var deletedCategory = await _client.DeleteAsync(categoryPath);
        Assert.Equal(HttpStatusCode.NoContent, deletedCategory.StatusCode);
        using var missingCategory = await _client.GetAsync(categoryPath);
        Assert.Equal(HttpStatusCode.NotFound, missingCategory.StatusCode);
        using var repeatedDelete = await _client.DeleteAsync(categoryPath);
        Assert.Equal(HttpStatusCode.NotFound, repeatedDelete.StatusCode);
        using var claim = await _client.PostAsJsonAsync($"{categoryPath}/claim",
            new ClaimCategoryRequest(Guid.NewGuid(), "Tatami 1"));
        Assert.Equal(HttpStatusCode.NotFound, claim.StatusCode);
        var stored = await _client.GetFromJsonAsync<ParticipantDto>(participantPath);
        Assert.Equal(participant.Id, stored!.Id);

        using var deletedParticipant = await _client.DeleteAsync(participantPath);
        Assert.Equal(HttpStatusCode.NoContent, deletedParticipant.StatusCode);
        using var missingParticipant = await _client.GetAsync(participantPath);
        Assert.Equal(HttpStatusCode.NotFound, missingParticipant.StatusCode);
        using var repeatedParticipantDelete = await _client.DeleteAsync(participantPath);
        Assert.Equal(HttpStatusCode.NotFound, repeatedParticipantDelete.StatusCode);
    }

    [Fact]
    public async Task CategoryDeletionWaitingForClaimReturnsConflict()
    {
        var category = await CreateOrganizerCategory([]);
        var path = $"/api/categories/{category.Id}";
        await using var owner = new NpgsqlConnection(_database.GetConnectionString());
        await using var observer = new NpgsqlConnection(_database.GetConnectionString());
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        await owner.OpenAsync(timeout.Token);
        await observer.OpenAsync(timeout.Token);
        await using var transaction = await owner.BeginTransactionAsync(timeout.Token);
        await using var claim = new NpgsqlCommand(
            """UPDATE "Categories" SET "ClaimToken" = @token, "ClaimedBy" = 'Tatami 1' WHERE "Id" = @id""",
            owner, transaction);
        claim.Parameters.AddWithValue("token", Guid.NewGuid());
        claim.Parameters.AddWithValue("id", category.Id);
        Assert.Equal(1, await claim.ExecuteNonQueryAsync(timeout.Token));

        var delete = _client.DeleteAsync(path, timeout.Token);
        await WaitForBlockedRequest(delete, observer, owner.ProcessID, timeout.Token);
        await transaction.CommitAsync(timeout.Token);
        using var response = await delete;
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var stored = await _client.GetFromJsonAsync<CategoryDto>(path);
        Assert.True(stored!.IsClaimed);
    }

    [Fact]
    public async Task FinishedCategoryAndItsParticipantCannotBeDeleted()
    {
        var (id, participantId, token) = await PrepareScoredCategory();
        var path = $"/api/categories/{id}";
        using var completed = await _client.PostAsJsonAsync($"{path}/individual-results",
            new CompleteIndividualCategoryRequest(token, [new(participantId, 1, [8m, 8m, 8m])]));
        Assert.Equal(HttpStatusCode.NoContent, completed.StatusCode);

        using var categoryDelete = await _client.DeleteAsync(path);
        Assert.Equal(HttpStatusCode.Conflict, categoryDelete.StatusCode);
        using var participantDelete = await _client.DeleteAsync($"/api/participants/{participantId}");
        Assert.Equal(HttpStatusCode.Conflict, participantDelete.StatusCode);
        var stored = await _client.GetFromJsonAsync<CategoryDto>(path);
        Assert.True(stored!.IsFinished);
        Assert.Equal(participantId, Assert.Single(stored.FinalResults).ParticipantId);
    }

    [Fact]
    public async Task ParticipantDeletionWaitingForAssignmentChecksCommittedMembership()
    {
        var participant = await CreateOrganizerParticipant("Jan");
        var category = await CreateOrganizerCategory([]);
        await using var writer = new NpgsqlConnection(_database.GetConnectionString());
        await using var observer = new NpgsqlConnection(_database.GetConnectionString());
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        await writer.OpenAsync(timeout.Token);
        await observer.OpenAsync(timeout.Token);
        await using var transaction = await writer.BeginTransactionAsync(timeout.Token);
        await using var assign = new NpgsqlCommand(
            """
            INSERT INTO "CategoryParticipant" ("CategoryId", "ParticipantId", "SortOrder")
            VALUES (@category, @participant, 0)
            """, writer, transaction);
        assign.Parameters.AddWithValue("category", category.Id);
        assign.Parameters.AddWithValue("participant", participant.Id);
        Assert.Equal(1, await assign.ExecuteNonQueryAsync(timeout.Token));

        var delete = _client.DeleteAsync($"/api/participants/{participant.Id}", timeout.Token);
        await WaitForBlockedRequest(delete, observer, writer.ProcessID, timeout.Token);
        await transaction.CommitAsync(timeout.Token);
        using var response = await delete;
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var stored = await _client.GetFromJsonAsync<CategoryDto>($"/api/categories/{category.Id}");
        Assert.Equal(participant.Id, Assert.Single(stored!.ParticipantIds));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CategoryWriteWaitingForParticipantDeletionReturnsValidationError(bool edit)
    {
        var participant = await CreateOrganizerParticipant("Jan");
        var category = edit ? await CreateOrganizerCategory([]) : null;
        var request = new CreateCategoryRequest(
            "Changed", null, null, Sex.Unisex, CategoryType.Kata, [Belts.Dan], [participant.Id]);
        await using var remover = new NpgsqlConnection(_database.GetConnectionString());
        await using var observer = new NpgsqlConnection(_database.GetConnectionString());
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        await remover.OpenAsync(timeout.Token);
        await observer.OpenAsync(timeout.Token);
        await using var transaction = await remover.BeginTransactionAsync(timeout.Token);
        await using var remove = new NpgsqlCommand(
            """DELETE FROM "Participants" WHERE "Id" = @id""", remover, transaction);
        remove.Parameters.AddWithValue("id", participant.Id);
        Assert.Equal(1, await remove.ExecuteNonQueryAsync(timeout.Token));

        var write = category is null
            ? _client.PostAsJsonAsync("/api/categories", request, timeout.Token)
            : _client.PutAsJsonAsync($"/api/categories/{category.Id}", request, timeout.Token);
        await WaitForBlockedRequest(write, observer, remover.ProcessID, timeout.Token);
        await transaction.CommitAsync(timeout.Token);
        using var response = await write;
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var snapshot = await _client.GetFromJsonAsync<TournamentSnapshotDto>("/api/tournament");
        Assert.Empty(snapshot!.Participants);
        if (category is null)
            Assert.Empty(snapshot.Categories);
        else
        {
            var stored = Assert.Single(snapshot.Categories);
            Assert.Equal(category.Name, stored.Name);
            Assert.Empty(stored.ParticipantIds);
        }
    }

    private static async Task WaitForBlockedRequest(
        Task<HttpResponseMessage> request, NpgsqlConnection observer, int blockingProcessId,
        CancellationToken cancellationToken)
    {
        await using var blocked = new NpgsqlCommand(
            "SELECT EXISTS (SELECT 1 FROM pg_stat_activity WHERE @owner = ANY(pg_blocking_pids(pid)))", observer);
        blocked.Parameters.AddWithValue("owner", blockingProcessId);
        while (!request.IsCompleted && !(bool)(await blocked.ExecuteScalarAsync(cancellationToken))!)
            await Task.Delay(25, cancellationToken);
        Assert.False(request.IsCompleted, "The API request should wait for the transaction holding the database lock.");
    }
}
