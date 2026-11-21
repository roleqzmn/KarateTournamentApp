using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Input;
using KarateClassLibrary.Contracts;
using KarateTournamentApp.Models;
using KarateTournamentApp.Services.Api;
using KarateTournamentApp.Services.Dialogs;

namespace KarateTournamentApp.ViewModels;

public partial class MainViewModel
{
    private readonly JudgingSessionStore? _sessions;
    private FileStream? _activeJudgeLock;
    private string? _judgingWarning;
    public ObservableCollection<JudgingSession> JudgingSessions { get; } = new();
    public ICommand StartApiJudgingCommand { get; }
    public ICommand ResumeApiJudgingCommand { get; }
    public ICommand ReleaseApiJudgingCommand { get; }
    public ICommand SubmitApiJudgingCommand { get; }
    public ICommand ArchiveApiJudgingCommand { get; }
    public ICommand RecoverApiCategoryCommand { get; }

    public string? JudgingWarning
    {
        get => _judgingWarning;
        private set { _judgingWarning = value; OnPropertyChanged(); }
    }

    private bool CanUseSessions() => _api is not null && _sessions is not null && !IsLoading && _activeJudgeLock is null;

    private bool CanStartApiJudging(CategoryViewModel item) =>
        item is not null && IsApiPreview && CanUseSessions() && !item.Category.IsFinished
        && CanJudge(item.Category)
        && !JudgingSessions.Any(session => session.CategoryId == item.Category.Id)
        && _apiSnapshot!.Categories.Any(category => category.Id == item.Category.Id && !category.IsClaimed);

    private static bool CanJudge(Category category) => category.Participants.Count > 0
        && category.CategoryType is CategoryType.Kumite or CategoryType.Kata or CategoryType.Kihon
            or CategoryType.KobudoShort or CategoryType.KobudoLong;

    private async Task StartApiJudgingAsync(CategoryViewModel item)
    {
        var station = _dialogService.ShowTextInput("Przejęcie konkurencji",
            "Podaj nazwę stanowiska sędziego.", Environment.MachineName);
        if (string.IsNullOrWhiteSpace(station))
            return;
        station = station.Trim();
        if (station.Length > 100)
            throw new InvalidOperationException("Nazwa stanowiska może mieć do 100 znaków.");
        await OpenJudgingSessionAsync(new JudgingSession(item.Category.Id, item.Name, Guid.NewGuid(), station));
    }

    private Task ResumeApiJudgingAsync(JudgingSession session) => OpenJudgingSessionAsync(session);

    private async Task OpenJudgingSessionAsync(JudgingSession requested)
    {
        var heldLock = _sessions!.Lock(requested.CategoryId);
        IsLoading = true;
        try
        {
            var session = _sessions.Load(requested.CategoryId) ?? requested;
            if (session.Category is null)
            {
                _sessions.Save(session);
                await _api!.ClaimCategoryAsync(session.CategoryId, new ClaimCategoryRequest(session.Token, session.StationName));

                var snapshot = await _api.GetSnapshotAsync();
                var category = snapshot.Categories.Single(item => item.Id == session.CategoryId);
                session.Category = TournamentSnapshotMapper.MapCategories(snapshot with { Categories = [category] }).Single().Category;
                if (!CanJudge(session.Category))
                {
                    session.Category = null;
                    throw new InvalidOperationException("Ta konkurencja nie ma obsługiwanego składu. Zwolnij sesję albo popraw skład po zwolnieniu.");
                }
                await _api.ClaimCategoryAsync(session.CategoryId, new ClaimCategoryRequest(session.Token, session.StationName));
                _sessions.Save(session);
                ShowApiSnapshot(snapshot);
            }
            if (session.Category.IsFinished)
                return;

            var view = new CategoryViewModel(session.Category, _ => { }, _ => { }, _dialogService,
                _windowService, _uiScheduler, "Sesja lokalna");
            var judge = view.OpenApiJudge(session.Draft, draft => SaveJudgingCheckpoint(session, draft));
            _activeJudgeLock = heldLock;
            judge.Closed += (_, _) =>
            {
                _activeJudgeLock?.Dispose();
                _activeJudgeLock = null;
                RefreshJudgingSessions();
                CommandManager.InvalidateRequerySuggested();
            };
        }
        finally
        {
            if (_activeJudgeLock != heldLock)
                heldLock.Dispose();
            IsLoading = false;
            RefreshJudgingSessions();
        }
    }

    private void SaveJudgingCheckpoint(JudgingSession session, IndividualJudgeDraft? draft)
    {
        session.Draft = draft;
        try
        {
            _sessions!.Save(session);
            JudgingWarning = null;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            JudgingWarning = $"Nie zapisano postępu sesji „{session.Name}”: {exception.Message}";
        }
    }

    private async Task ReleaseApiJudgingAsync(JudgingSession selected)
    {
        if (!ConfirmApiDelete($"Zwolnić konkurencję „{selected.Name}”? Lokalny zapis tej sesji zostanie usunięty po potwierdzeniu przez API."))
            return;
        using var heldLock = _sessions!.Lock(selected.CategoryId);
        IsLoading = true;
        try
        {
            var session = _sessions.Load(selected.CategoryId)
                ?? throw new InvalidOperationException("Lokalna sesja już nie istnieje.");
            await _api!.ReleaseCategoryAsync(session.CategoryId, new ReleaseCategoryRequest(session.Token));
            _sessions.Delete(session.CategoryId);
            ShowApiSnapshot(await _api.GetSnapshotAsync());
        }
        finally
        {
            IsLoading = false;
            RefreshJudgingSessions();
        }
    }

    private void RefreshJudgingSessions()
    {
        if (_sessions is null)
            return;
        try
        {
            var entries = _sessions.LoadAll(error => JudgingWarning = error).ToArray();
            JudgingSessions.Clear();
            foreach (var session in entries)
                JudgingSessions.Add(session);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            JudgingWarning = $"Nie można odczytać lokalnych sesji: {exception.Message}";
        }
    }

    private async Task SubmitApiJudgingAsync(JudgingSession selected)
    {
        using var heldLock = _sessions!.Lock(selected.CategoryId);
        IsLoading = true;
        try
        {
            var session = _sessions.Load(selected.CategoryId);
            if (session?.Category is not { IsFinished: true } category)
                throw new InvalidOperationException("Zakończ konkurencję przed wysłaniem wyników.");

            if (category.CategoryType == CategoryType.Kumite)
            {
                var matches = category.BracketMatches.Select((item, position) =>
                {
                    if (item is not ShobuSanbonMatch match)
                        throw new InvalidDataException("Lokalna drabinka zawiera nieobsługiwaną walkę.");
                    return new KumiteMatchResultRequest(position, match.Aka, match.Shiro, match.WinnerId,
                        match.AkaScore, match.ShiroScore, match.IsDisqualification, match.TimeRemaining,
                        match.AtenaiAka, match.AtenaiShiro, match.ChukokuAka, match.ChukokuShiro,
                        match.SenshuEnabled, match.HasSenshuAka, match.HasSenshuShiro, match.IsInOvertime, match.OvertimeCount);
                }).ToArray();
                var ranking = category.FinalResults.Select(result => result.Participant.Id).ToArray();
                await _api!.SubmitKumiteResultsAsync(session.CategoryId, new(session.Token, matches, ranking));
            }
            else
            {
                var judge = new IndividualCompetitionManagerViewModel(category, _windowService, _uiScheduler);
                var draft = session.Draft ?? throw new InvalidDataException("Nie ma lokalnego zapisu ocen.");
                judge.RestoreDraft(draft);
                if (draft.Results.Length != category.Participants.Count)
                    throw new InvalidDataException("Lokalny wynik nie zawiera ocen wszystkich zawodników.");
                var results = judge.GetFinalRankings().Select((result, index) =>
                    new IndividualResultRequest(result.Participant.Id, index + 1, result.JudgeScores.ToArray())).ToArray();
                // Persist the resolved ranking before sending so retries keep the same tie decisions.
                _sessions.Save(session);
                await _api!.SubmitIndividualResultsAsync(session.CategoryId, new(session.Token, results));
            }
            _sessions.Delete(session.CategoryId);
            _dialogService.ShowMessage("Wyniki zapisano na serwerze.", "Wyniki");
            ShowApiSnapshot(await _api.GetSnapshotAsync());
        }
        finally
        {
            IsLoading = false;
            RefreshJudgingSessions();
        }
    }

    private Task ArchiveApiJudgingAsync(JudgingSession selected)
    {
        if (ConfirmApiDelete($"Zarchiwizować lokalną sesję „{selected.Name}”? Operacja nie zwalnia blokady na serwerze."))
        {
            using var heldLock = _sessions!.Lock(selected.CategoryId);
            _sessions.Archive(selected.CategoryId);
            RefreshJudgingSessions();
        }
        return Task.CompletedTask;
    }
}
