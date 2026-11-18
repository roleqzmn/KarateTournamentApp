using System.IO;
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
        var address = string.IsNullOrWhiteSpace(baseAddress) ? "https://localhost:7086" : baseAddress.Trim();
        if (!Uri.TryCreate(address.TrimEnd('/') + "/", UriKind.Absolute, out var baseUri)
            || (baseUri.Scheme != Uri.UriSchemeHttp && baseUri.Scheme != Uri.UriSchemeHttps)
            || baseUri.UserInfo.Length > 0 || baseUri.Query.Length > 0 || baseUri.Fragment.Length > 0)
        {
            throw new InvalidOperationException("Ustaw poprawny adres HTTP lub HTTPS w KARATE_TOURNAMENT_API_URL.");
        }

        try
        {
            return await httpClient.GetFromJsonAsync<TournamentSnapshotDto>(
                new Uri(baseUri, "api/tournament"), JsonOptions, cancellationToken).ConfigureAwait(false)
                ?? throw new InvalidDataException("API zwróciło pusty turniej zamiast danych.");
        }
        catch (HttpRequestException exception) when (exception.StatusCode.HasValue)
        {
            throw new InvalidOperationException(
                $"API nie zwróciło turnieju (HTTP {(int)exception.StatusCode.Value}).", exception);
        }
        catch (HttpRequestException exception)
        {
            throw new InvalidOperationException($"Nie można połączyć się z API pod adresem {baseUri}.", exception);
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
}
