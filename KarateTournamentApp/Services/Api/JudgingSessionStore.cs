using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using KarateClassLibrary.Contracts;
using KarateTournamentApp.Models;

namespace KarateTournamentApp.Services.Api;

public sealed class JudgingSession(int categoryId, string name, Guid token, string stationName)
{
    public int CategoryId { get; } = categoryId;
    public string Name { get; } = name;
    public Guid Token { get; } = token;
    public string StationName { get; } = stationName;
    public Guid? MatClaimToken { get; init; }
    public Category? Category { get; set; }
    public bool AwaitingHandoff { get; set; }
    // JsonInclude serializes this internal checkpoint without exposing it to the view.
    [JsonInclude] internal IndividualJudgeDraft? Draft { get; set; }
    [JsonInclude] internal TeamJudgeDraft? TeamDraft { get; set; }
    [JsonIgnore] public string Label => $"{Name} · {StationName}";
}

internal sealed record LocalMatClaim(int Number, ClaimMatRequest Request);

internal sealed class JudgingSessionStore
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { IncludeFields = true };
    private readonly string _folder;

    public JudgingSessionStore(string serverScope, string? baseFolder = null)
    {
        var folderName = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(serverScope)));
        _folder = Path.Combine(baseFolder ?? Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "KarateTournamentApp", "Judging", folderName);
    }

    public FileStream Lock(int categoryId) => LockFile($"{categoryId}.lock", "Ta sesja jest otwarta w innej instancji aplikacji.");

    internal FileStream LockMat(int number) => LockFile($"mat-{number}.lock", "Ta mata jest otwarta w innej instancji aplikacji.");

    private FileStream LockFile(string fileName, string failure)
    {
        Directory.CreateDirectory(_folder);
        try { return new FileStream(Path.Combine(_folder, fileName), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None); }
        catch (IOException exception) { throw new IOException(failure, exception); }
    }

    internal LocalMatClaim? LoadMat(int number)
    {
        var path = MatPath(number);
        if (!File.Exists(path))
            return null;
        var claim = JsonSerializer.Deserialize<LocalMatClaim>(File.ReadAllBytes(path), JsonOptions);
        return claim is { Request: { } request } && claim.Number == number && request.SessionToken != Guid.Empty
            ? claim : throw new InvalidDataException($"Nie można odczytać lokalnego przydziału maty {number}.");
    }

    internal void SaveMat(LocalMatClaim claim) => SaveJson(MatPath(claim.Number), claim);
    internal void DeleteMat(int number) => File.Delete(MatPath(number));
    private string MatPath(int number) => Path.Combine(_folder, $"mat-{number}.json");

    public JudgingSession? Load(int categoryId)
    {
        var path = GetPath(categoryId);
        if (!File.Exists(path))
            return null;
        var session = JsonSerializer.Deserialize<JudgingSession>(File.ReadAllBytes(path), JsonOptions);
        if (session is null || session.CategoryId != categoryId || session.Token == Guid.Empty
            || (session.Category is { } category && category.Id != categoryId))
            throw new InvalidDataException($"Nie można odczytać lokalnego zapisu konkurencji {categoryId}.");

        if (session.Category is { } saved)
            foreach (var match in saved.BracketMatches.OfType<ShobuSanbonMatch>())
                match.IsRunning = false;
        return session;
    }

    public IEnumerable<JudgingSession> LoadAll(Action<string> onError)
    {
        if (!Directory.Exists(_folder))
            yield break;
        foreach (var file in Directory.EnumerateFiles(_folder, "*.json").Order())
        {
            if (!int.TryParse(Path.GetFileNameWithoutExtension(file), out var id))
                continue;
            JudgingSession? session = null;
            try { session = Load(id); }
            catch (Exception exception) when (exception is IOException or JsonException or UnauthorizedAccessException)
            {
                onError($"Nie odczytano sesji {id}: {exception.Message}");
            }
            if (session is not null)
                yield return session;
        }
    }

    // The caller holds Lock(categoryId) during all writes.
    public void Save(JudgingSession session) => SaveJson(GetPath(session.CategoryId), session);

    private static void SaveJson<T>(string path, T value)
    {
        var temp = path + ".tmp";
        using (var output = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            JsonSerializer.Serialize(output, value, JsonOptions);
            output.Flush(flushToDisk: true);
        }
        File.Move(temp, path, overwrite: true);
    }

    public void Delete(int categoryId) => File.Delete(GetPath(categoryId));
    public void Archive(int categoryId) =>
        File.Move(GetPath(categoryId), Path.Combine(_folder, $"{categoryId}.{Guid.NewGuid():N}.archive.json.bak"));
    private string GetPath(int categoryId) => Path.Combine(_folder, $"{categoryId}.json");
}
