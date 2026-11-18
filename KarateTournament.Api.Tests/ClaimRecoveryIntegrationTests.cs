using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using KarateTournament.Api.Contracts;
using Npgsql;
using Xunit;

namespace KarateTournament.Api.Tests;

public partial class TournamentApiIntegrationTests
{
    [Fact]
    public async Task RecoveryRequiresOrganizerKeyAndDoesNotExposeSessionToken()
    {
        var (id, _, token) = await PrepareScoredCategory();
        var path = $"/api/categories/{id}/claim-recovery";
        var key = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));

        using var disabledRead = await _client.GetAsync(path);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, disabledRead.StatusCode);
        using var disabledWrite = await _client.PostAsJsonAsync(path, new RecoverCategoryClaimRequest(new string('A', 64)));
        Assert.Equal(HttpStatusCode.ServiceUnavailable, disabledWrite.StatusCode);

        await RestartWithRecoveryKey(key);
        using var anonymous = await _client.GetAsync(path);
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
        using var invalidKey = await SendRecoveryRequest(HttpMethod.Post, path,
            Convert.ToHexString(RandomNumberGenerator.GetBytes(32)), new(new string('A', 64)));
        Assert.Equal(HttpStatusCode.Unauthorized, invalidKey.StatusCode);
        Assert.Contains(invalidKey.Headers.WwwAuthenticate, header => header.Scheme == "Bearer");
        using var authorized = await SendRecoveryRequest(HttpMethod.Get, path, key);
        Assert.Equal(HttpStatusCode.OK, authorized.StatusCode);
        Assert.True(authorized.Headers.CacheControl?.NoStore);
        var json = await authorized.Content.ReadAsStringAsync();
        Assert.DoesNotContain(token.ToString(), json, StringComparison.OrdinalIgnoreCase);
        var target = (await authorized.Content.ReadFromJsonAsync<ClaimRecoveryDto>())!;
        Assert.Equal("Tatami 1", target.StationName);
        Assert.Equal(64, target.ClaimReference.Length);
    }

    [Fact]
    public async Task RecoveredSessionCannotResubmitOrReclaimAndStaleRecoveryCannotClearNewOwner()
    {
        var (id, participantId, token) = await PrepareScoredCategory();
        var key = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        await RestartWithRecoveryKey(key);
        var categoryPath = $"/api/categories/{id}";
        var path = $"{categoryPath}/claim-recovery";
        using var current = await SendRecoveryRequest(HttpMethod.Get, path, key);
        var target = (await current.Content.ReadFromJsonAsync<ClaimRecoveryDto>())!;
        var recovery = new RecoverCategoryClaimRequest(target.ClaimReference);

        using var recovered = await SendRecoveryRequest(HttpMethod.Post, path, key, recovery);
        Assert.Equal(HttpStatusCode.NoContent, recovered.StatusCode);
        await RestartWithRecoveryKey(key);
        using var oldResult = await _client.PostAsJsonAsync($"{categoryPath}/individual-results",
            new CompleteIndividualCategoryRequest(token, [new(participantId, 1, [8m, 8m, 8m])]));
        Assert.Equal(HttpStatusCode.Conflict, oldResult.StatusCode);
        using var oldClaim = await _client.PostAsJsonAsync($"{categoryPath}/claim", new ClaimCategoryRequest(token, "Tatami 1"));
        Assert.Equal(HttpStatusCode.Conflict, oldClaim.StatusCode);

        var nextToken = Guid.NewGuid();
        using var next = await _client.PostAsJsonAsync($"{categoryPath}/claim", new ClaimCategoryRequest(nextToken, "Tatami 2"));
        Assert.Equal(HttpStatusCode.NoContent, next.StatusCode);
        using var staleRecovery = await SendRecoveryRequest(HttpMethod.Post, path, key, recovery);
        Assert.Equal(HttpStatusCode.Conflict, staleRecovery.StatusCode);
        using var staleRelease = await _client.PostAsJsonAsync($"{categoryPath}/release", new ReleaseCategoryRequest(token));
        Assert.Equal(HttpStatusCode.Conflict, staleRelease.StatusCode);
        var stored = (await _client.GetFromJsonAsync<CategoryDto>(categoryPath))!;
        Assert.Equal("Tatami 2", stored.ClaimedBy);
        Assert.Empty(stored.FinalResults);

        using var completed = await _client.PostAsJsonAsync($"{categoryPath}/individual-results",
            new CompleteIndividualCategoryRequest(nextToken, [new(participantId, 1, [8m, 8m, 8m])]));
        Assert.Equal(HttpStatusCode.NoContent, completed.StatusCode);
        using var afterCompletion = await SendRecoveryRequest(HttpMethod.Post, path, key, recovery);
        Assert.Equal(HttpStatusCode.Conflict, afterCompletion.StatusCode);
    }

    [Fact]
    public async Task WaitingClaimSeesRevocationAfterTheRowLockIsReleased()
    {
        var (id, _, token) = await PrepareScoredCategory();
        var key = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        await RestartWithRecoveryKey(key);
        var path = $"/api/categories/{id}";
        using var current = await SendRecoveryRequest(HttpMethod.Get, $"{path}/claim-recovery", key);
        var target = (await current.Content.ReadFromJsonAsync<ClaimRecoveryDto>())!;
        await using var writer = new NpgsqlConnection(_database.GetConnectionString());
        await using var observer = new NpgsqlConnection(_database.GetConnectionString());
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        await writer.OpenAsync(timeout.Token);
        await observer.OpenAsync(timeout.Token);
        await using var transaction = await writer.BeginTransactionAsync(timeout.Token);

        // Model a recovery transaction that has staged both writes but not committed.
        await using var revoke = new NpgsqlCommand(
            """
            UPDATE "Categories" SET "ClaimToken" = NULL, "ClaimedBy" = NULL WHERE "Id" = @id;
            INSERT INTO "RevokedCategoryClaims" ("CategoryId", "ClaimReference") VALUES (@id, @reference);
            """, writer, transaction);
        revoke.Parameters.AddWithValue("id", id);
        revoke.Parameters.AddWithValue("reference", target.ClaimReference);
        Assert.Equal(2, await revoke.ExecuteNonQueryAsync(timeout.Token));

        var retry = _client.PostAsJsonAsync($"{path}/claim", new ClaimCategoryRequest(token, "Tatami 1"), timeout.Token);
        await WaitForBlockedRequest(retry, observer, writer.ProcessID, timeout.Token);
        await transaction.CommitAsync(timeout.Token);
        using var response = await retry;
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.False((await _client.GetFromJsonAsync<CategoryDto>(path))!.IsClaimed);
    }

    [Fact]
    public async Task ClaimRevocationSchemaUpgradePreservesExistingClaims()
    {
        var (id, _, token) = await PrepareScoredCategory();
        await using var connection = new NpgsqlConnection(_database.GetConnectionString());
        await connection.OpenAsync();
        // Restore the preceding schema in this disposable database before applying the script.
        await using (var oldSchema = new NpgsqlCommand("""DROP TABLE "RevokedCategoryClaims" """, connection))
            await oldSchema.ExecuteNonQueryAsync();
        var script = await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "add-category-claim-revocations.sql"));
        for (var attempt = 0; attempt < 2; attempt++)
        {
            await using var upgrade = new NpgsqlCommand(script, connection);
            await upgrade.ExecuteNonQueryAsync();
        }

        var path = $"/api/categories/{id}";
        Assert.True((await _client.GetFromJsonAsync<CategoryDto>(path))!.IsClaimed);
        using var released = await _client.PostAsJsonAsync($"{path}/release", new ReleaseCategoryRequest(token));
        Assert.Equal(HttpStatusCode.NoContent, released.StatusCode);
        using var retry = await _client.PostAsJsonAsync($"{path}/claim", new ClaimCategoryRequest(token, "Tatami 1"));
        Assert.Equal(HttpStatusCode.Conflict, retry.StatusCode);
    }

    private async Task RestartWithRecoveryKey(string key)
    {
        _client.Dispose();
        await _factory.DisposeAsync();
        StartApi(key);
    }

    private async Task<HttpResponseMessage> SendRecoveryRequest(
        HttpMethod method, string path, string key, RecoverCategoryClaimRequest? payload = null)
    {
        using var request = new HttpRequestMessage(method, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", key);
        if (payload is not null)
            request.Content = JsonContent.Create(payload);
        return await _client.SendAsync(request);
    }
}
