using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using KarateClassLibrary.Contracts;

namespace KarateTournamentApp.Services.Api;

internal sealed class TournamentApiClient(HttpClient httpClient, string? baseAddress)
{
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

    public async Task<ParticipantDto> CreateParticipantAsync(
        CreateParticipantRequest participant, CancellationToken cancellationToken = default)
    {
        var endpoint = GetEndpoint("api/participants");
        try
        {
            using var response = await httpClient.PostAsJsonAsync(
                endpoint, participant, JsonOptions, cancellationToken).ConfigureAwait(false);
            if (response.StatusCode == HttpStatusCode.BadRequest)
                throw new InvalidOperationException(await ReadValidationErrorAsync(response, cancellationToken).ConfigureAwait(false));
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

    private Uri GetEndpoint(string path)
    {
        var address = string.IsNullOrWhiteSpace(baseAddress) ? "https://localhost:7086" : baseAddress.Trim();
        if (!Uri.TryCreate(address.TrimEnd('/') + "/", UriKind.Absolute, out var baseUri)
            || (baseUri.Scheme != Uri.UriSchemeHttp && baseUri.Scheme != Uri.UriSchemeHttps)
            || baseUri.UserInfo.Length > 0 || baseUri.Query.Length > 0 || baseUri.Fragment.Length > 0)
        {
            throw new InvalidOperationException("Ustaw poprawny adres HTTP lub HTTPS w KARATE_TOURNAMENT_API_URL.");
        }
        return new Uri(baseUri, path);
    }

    private static async Task<string> ReadValidationErrorAsync(
        HttpResponseMessage response, CancellationToken cancellationToken)
    {
        const string fallback = "API odrzuciło dane zawodnika (HTTP 400).";
        try
        {
            var problem = await response.Content.ReadFromJsonAsync<ValidationErrorResponse>(cancellationToken)
                .ConfigureAwait(false);
            var messages = problem?.Errors?.Values.SelectMany(errors => errors).Distinct().ToArray();
            return messages is { Length: > 0 } ? string.Join(Environment.NewLine, messages) : fallback;
        }
        catch (JsonException)
        {
            return fallback;
        }
    }

    private static InvalidOperationException UnconfirmedRegistration(string reason, Exception? inner = null) =>
        new($"Nie udało się potwierdzić zapisu zawodnika ({reason}). Odśwież turniej przed ponowieniem zapisu.", inner);

    private sealed record ValidationErrorResponse(Dictionary<string, string[]>? Errors);
}
