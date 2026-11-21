using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using KarateClassLibrary.Contracts;

namespace KarateTournamentApp.Services.Api;

internal sealed class TournamentApiClient(HttpClient httpClient, string? baseAddress, string? recoveryKey = null)
{
    public bool HasRecoveryKey => !string.IsNullOrWhiteSpace(recoveryKey);
    public string SessionScope => (string.IsNullOrWhiteSpace(baseAddress) ? "https://localhost:7086" : baseAddress.Trim()).TrimEnd('/');

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        RespectRequiredConstructorParameters = true,
        RespectNullableAnnotations = true
    };

    public async Task<TournamentSnapshotDto> GetSnapshotAsync(CancellationToken cancellationToken = default)
    {
        var endpoint = GetEndpoint("api/tournament");
        try
        {
            return await httpClient.GetFromJsonAsync<TournamentSnapshotDto>(
                endpoint, JsonOptions, cancellationToken).ConfigureAwait(false)
                ?? throw new InvalidDataException("API zwróciło pusty turniej zamiast danych.");
        }
        catch (HttpRequestException exception) when (exception.StatusCode.HasValue)
        {
            throw new InvalidOperationException(
                $"API nie zwróciło turnieju (HTTP {(int)exception.StatusCode.Value}).", exception);
        }
        catch (HttpRequestException exception)
        {
            throw new InvalidOperationException($"Nie można połączyć się z API pod adresem {endpoint}.", exception);
        }
        catch (OperationCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            throw new InvalidOperationException("API nie odpowiedziało w wyznaczonym czasie.", exception);
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException("API zwróciło dane w nieobsługiwanym formacie.", exception);
        }
    }

    public async Task<CategorySummaryDto[]> GetCategorySummariesAsync(CancellationToken cancellationToken) =>
        await httpClient.GetFromJsonAsync<CategorySummaryDto[]>(GetEndpoint("api/categories"), JsonOptions, cancellationToken)
            .ConfigureAwait(false) ?? throw new InvalidDataException("API zwróciło pustą listę konkurencji zamiast danych.");

    public async Task<ParticipantDto> CreateParticipantAsync(
        CreateParticipantRequest participant, CancellationToken cancellationToken = default, CategoryGrouping? groupBy = null)
    {
        var endpoint = GetEndpoint(RegistrationPath("api/participants", groupBy));
        try
        {
            using var response = await httpClient.PostAsJsonAsync(
                endpoint, participant, JsonOptions, cancellationToken).ConfigureAwait(false);
            if (response.StatusCode == HttpStatusCode.BadRequest)
                throw new InvalidOperationException(await ReadValidationErrorAsync(response, cancellationToken).ConfigureAwait(false)
                    ?? "API odrzuciło dane zawodnika (HTTP 400).");
            if ((int)response.StatusCode is >= 400 and < 500 && response.StatusCode != HttpStatusCode.RequestTimeout)
                throw new InvalidOperationException($"API odrzuciło zapis zawodnika (HTTP {(int)response.StatusCode}).");
            if (response.StatusCode != HttpStatusCode.Created)
                throw UnconfirmedRegistration($"HTTP {(int)response.StatusCode}");

            var saved = await response.Content.ReadFromJsonAsync<ParticipantDto>(JsonOptions, cancellationToken)
                .ConfigureAwait(false);
            return saved is { Id: > 0 }
                ? saved
                : throw UnconfirmedRegistration("brak potwierdzonego ID zawodnika");
        }
        catch (HttpRequestException exception)
        {
            throw UnconfirmedRegistration("utracono połączenie z API", exception);
        }
        catch (OperationCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            throw UnconfirmedRegistration("upłynął limit oczekiwania", exception);
        }
        catch (JsonException exception)
        {
            throw UnconfirmedRegistration("niepoprawna odpowiedź API", exception);
        }
    }

    public Task<ParticipantDto> UpdateParticipantAsync(int id, CreateParticipantRequest request) =>
        WriteAsync<ParticipantDto>(HttpMethod.Put, $"api/participants/{id}", request);

    public Task<ParticipantDto[]> ImportParticipantsAsync(CreateParticipantRequest[] requests, CategoryGrouping groupBy) =>
        WriteAsync<ParticipantDto[]>(HttpMethod.Post, RegistrationPath("api/participants/batch", groupBy), requests);

    private static string RegistrationPath(string path, CategoryGrouping? groupBy) =>
        groupBy is { } grouping ? $"{path}?groupBy={grouping}" : path;

    public Task<CategoryDto> SaveCategoryAsync(int? id, CreateCategoryRequest request) =>
        id.HasValue
            ? WriteAsync<CategoryDto>(HttpMethod.Put, $"api/categories/{id}", request)
            : WriteAsync<CategoryDto>(HttpMethod.Post, "api/categories", request);

    public Task DeleteParticipantAsync(int id) => WriteAsync<object>(HttpMethod.Delete, $"api/participants/{id}");
    public Task DeleteCategoryAsync(int id) => WriteAsync<object>(HttpMethod.Delete, $"api/categories/{id}");
    public Task<TeamDto> SaveTeamAsync(int categoryId, int? teamId, SaveTeamRequest request) =>
        teamId is int id
            ? WriteAsync<TeamDto>(HttpMethod.Put, $"api/categories/{categoryId}/teams/{id}", request)
            : WriteAsync<TeamDto>(HttpMethod.Post, $"api/categories/{categoryId}/teams", request);
    public Task DeleteTeamAsync(int categoryId, int teamId) =>
        WriteAsync<object>(HttpMethod.Delete, $"api/categories/{categoryId}/teams/{teamId}");

    public Task ClaimCategoryAsync(int id, ClaimCategoryRequest request) =>
        WriteAsync<object>(HttpMethod.Post, $"api/categories/{id}/claim", request);
    public Task ReleaseCategoryAsync(int id, ReleaseCategoryRequest request) =>
        WriteAsync<object>(HttpMethod.Post, $"api/categories/{id}/release", request);
    public Task SubmitIndividualResultsAsync(int id, CompleteIndividualCategoryRequest request) =>
        WriteAsync<object>(HttpMethod.Post, $"api/categories/{id}/individual-results", request);
    public Task SubmitTeamResultsAsync(int id, CompleteTeamCategoryRequest request) =>
        WriteAsync<object>(HttpMethod.Post, $"api/categories/{id}/team-results", request);
    public Task SubmitKumiteResultsAsync(int id, CompleteKumiteCategoryRequest request) =>
        WriteAsync<object>(HttpMethod.Post, $"api/categories/{id}/kumite-results", request);
    public Task<ClaimRecoveryDto> GetRecoveryTargetAsync(int id) =>
        WriteAsync<ClaimRecoveryDto>(HttpMethod.Get, $"api/categories/{id}/claim-recovery", useRecoveryKey: true);
    public Task RecoverClaimAsync(int id, RecoverCategoryClaimRequest request) =>
        WriteAsync<object>(HttpMethod.Post, $"api/categories/{id}/claim-recovery", request, useRecoveryKey: true);

    public Task<ClaimRecoveryDto> GetHandoffTargetAsync(int id) =>
        WriteAsync<ClaimRecoveryDto>(HttpMethod.Get, $"api/categories/{id}/handoff");

    public Task RequestHandoffAsync(int id, RecoverCategoryClaimRequest request) =>
        WriteAsync<object>(HttpMethod.Post, $"api/categories/{id}/handoff", request);

    public Task<JudgingHandoffState> GetHandoffStateAsync(int id, Guid token, CancellationToken cancellationToken) =>
        WriteAsync<JudgingHandoffState>(HttpMethod.Post, $"api/categories/{id}/handoff/status",
            new ReleaseCategoryRequest(token), cancellationToken: cancellationToken);

    public Task<JudgingCheckpointDto> GetJudgingCheckpointAsync(int id, Guid token) =>
        WriteAsync<JudgingCheckpointDto>(HttpMethod.Post, $"api/categories/{id}/handoff/checkpoint", new ReleaseCategoryRequest(token));

    public Task SaveAndReleaseJudgingAsync(int id, SaveJudgingHandoffRequest request, CancellationToken cancellationToken = default) =>
        WriteAsync<object>(HttpMethod.Post, $"api/categories/{id}/handoff/save", request, cancellationToken: cancellationToken);

    private async Task<T> WriteAsync<T>(HttpMethod method, string path, object? payload = null, bool useRecoveryKey = false,
        CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(method, GetEndpoint(path));
        if (useRecoveryKey)
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", recoveryKey);
        if (payload is not null)
            request.Content = JsonContent.Create(payload, options: JsonOptions);
        try
        {
            using var response = await httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                var message = await ReadApiErrorAsync(response).ConfigureAwait(false);
                throw response.StatusCode == HttpStatusCode.Conflict
                    ? new ApiConflictException(message)
                    : new InvalidOperationException(message);
            }
            if (response.StatusCode == HttpStatusCode.NoContent && typeof(T) == typeof(object))
                return (T)new object();
            return await response.Content.ReadFromJsonAsync<T>(JsonOptions, cancellationToken).ConfigureAwait(false)
                ?? throw new InvalidDataException("API nie zwróciło danych. Odśwież turniej, aby sprawdzić stan zapisu.");
        }
        catch (HttpRequestException exception)
        {
            throw new InvalidOperationException("Nie można potwierdzić operacji. Sprawdź połączenie i odśwież turniej.", exception);
        }
        catch (OperationCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            throw new InvalidOperationException("Upłynął czas oczekiwania. Odśwież turniej, aby sprawdzić stan zapisu.", exception);
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException("Niepoprawna odpowiedź API. Odśwież turniej, aby sprawdzić stan zapisu.", exception);
        }
    }

    private static async Task<string> ReadApiErrorAsync(HttpResponseMessage response)
    {
        var fallback = $"Operacja nie powiodła się (HTTP {(int)response.StatusCode}).";
        if (response.StatusCode == HttpStatusCode.BadRequest)
        {
            var validation = await ReadValidationErrorAsync(response, CancellationToken.None).ConfigureAwait(false);
            if (validation is not null)
                return validation;
        }
        try
        {
            var problem = await response.Content.ReadFromJsonAsync<ApiErrorResponse>().ConfigureAwait(false);
            return string.IsNullOrWhiteSpace(problem?.Title) ? fallback : $"{fallback}\n{problem.Title}";
        }
        catch (JsonException)
        {
            return fallback;
        }
    }

    private Uri GetEndpoint(string path)
    {
        var address = SessionScope;
        if (!Uri.TryCreate(address.TrimEnd('/') + "/", UriKind.Absolute, out var baseUri)
            || (baseUri.Scheme != Uri.UriSchemeHttp && baseUri.Scheme != Uri.UriSchemeHttps)
            || baseUri.UserInfo.Length > 0 || baseUri.Query.Length > 0 || baseUri.Fragment.Length > 0)
        {
            throw new InvalidOperationException("Ustaw poprawny adres HTTP lub HTTPS w KARATE_TOURNAMENT_API_URL.");
        }
        if ((HasRecoveryKey || httpClient.DefaultRequestHeaders.Authorization is not null) && baseUri.Scheme != Uri.UriSchemeHttps)
            throw new InvalidOperationException("Ustaw adres HTTPS, aby używać kluczy dostępu do API.");
        return new Uri(baseUri, path);
    }

    private static async Task<string?> ReadValidationErrorAsync(
        HttpResponseMessage response, CancellationToken cancellationToken)
    {
        try
        {
            var problem = await response.Content.ReadFromJsonAsync<ValidationErrorResponse>(cancellationToken)
                .ConfigureAwait(false);
            var messages = problem?.Errors?.SelectMany(entry => entry.Value.Select(message =>
                entry.Key.StartsWith("rows[", StringComparison.Ordinal) ? $"{entry.Key}: {message}" : message))
                .Distinct().ToArray();
            return messages is { Length: > 0 } ? string.Join(Environment.NewLine, messages) : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static InvalidOperationException UnconfirmedRegistration(string reason, Exception? inner = null) =>
        new($"Nie udało się potwierdzić zapisu zawodnika ({reason}). Odśwież turniej przed ponowieniem zapisu.", inner);

    private sealed record ValidationErrorResponse(Dictionary<string, string[]>? Errors);
    private sealed record ApiErrorResponse(string? Title);
}

internal sealed class ApiConflictException(string message) : InvalidOperationException(message);
