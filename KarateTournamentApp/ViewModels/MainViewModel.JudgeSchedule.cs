using System.Globalization;
using System.IO;
using System.Windows.Input;
using KarateClassLibrary.Contracts;
using KarateTournamentApp.Services.Api;

namespace KarateTournamentApp.ViewModels;

public partial class MainViewModel
{
    private ScheduleMatDto[] _judgeMats = [];
    private ScheduleMatDto? _selectedJudgeMat;
    private LocalMatClaim? _judgeMatClaim;
    private FileStream? _judgeMatLock;
    private bool _judgeScheduleClosed;
    private string? _judgeScheduleStatus = "Pobierz turniej z API, aby wczytać maty.";

    public IReadOnlyList<ScheduleMatDto> JudgeMats => _judgeMats;
    public bool CanChooseJudgeMat => _judgeMatClaim is null;
    public string? JudgeScheduleStatus
    {
        get => _judgeScheduleStatus;
        private set { _judgeScheduleStatus = value; OnPropertyChanged(); }
    }

    public ScheduleMatDto? SelectedJudgeMat
    {
        get => _selectedJudgeMat;
        set
        {
            if (_selectedJudgeMat == value)
                return;
            _selectedJudgeMat = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(JudgeMatQueue));
            CommandManager.InvalidateRequerySuggested();
        }
    }

    public IEnumerable<CategoryViewModel> UnscheduledJudgeCategories
    {
        get
        {
            var scheduled = _judgeMats.SelectMany(mat => mat.Slots).Select(slot => slot.CategoryId).ToHashSet();
            return Categories.Where(category => !scheduled.Contains(category.Category.Id));
        }
    }

    public IEnumerable<MatQueueRow> JudgeMatQueue =>
        SelectedJudgeMat?.Slots.Select((slot, index) => new MatQueueRow(index + 1, slot)) ?? [];

    public JudgingSession? JudgeMatSession => _judgeMatClaim is { } active
        ? JudgingSessions.FirstOrDefault(session => session.MatClaimToken == active.Request.SessionToken) : null;

    public ICommand RefreshJudgeScheduleCommand { get; }
    public ICommand ClaimJudgeMatCommand { get; }
    public ICommand ReleaseJudgeMatCommand { get; }
    public ICommand StartNextScheduledCategoryCommand { get; }

    private bool CanUseJudgeSchedule() => IsJudgeStation && !_judgeScheduleClosed && CanUseSessions();
    private bool CanClaimJudgeMat() => CanUseJudgeSchedule() && _judgeMatClaim is null && SelectedJudgeMat is not null;
    private bool CanReleaseJudgeMat() => CanUseJudgeSchedule() && _judgeMatClaim is not null;
    private bool CanStartNextScheduledCategory() =>
        CanUseJudgeSchedule() && _judgeMatClaim is not null && FindNextScheduledCategory() is { } next
        && CanStartCategory(next);

    private CategoryViewModel? FindNextScheduledCategory()
    {
        var mat = _judgeMats.SingleOrDefault(item => item.Number == _judgeMatClaim?.Number);
        var next = mat?.Slots.FirstOrDefault(slot => !slot.IsFinished);
        return next is { ClaimedBy: null }
            ? Categories.SingleOrDefault(category => category.Category.Id == next.CategoryId) : null;
    }

    private Task RefreshJudgeScheduleAsync() => RunJudgeScheduleAsync(async () =>
    {
        var snapshot = await _api!.GetSnapshotAsync();
        ShowApiSnapshot(snapshot);
        await FetchJudgeMatsAsync();
    });

    private async Task FetchJudgeMatsAsync(bool validateClaim = true)
    {
        var schedule = await _api!.GetScheduleAsync();
        if (_judgeScheduleClosed)
            return;
        UpdateJudgeMats(schedule.Mats);
        var lostMat = false;
        if (validateClaim && _judgeMatClaim is { } claim)
        {
            var mat = _judgeMats.SingleOrDefault(item => item.Number == claim.Number);
            if (mat is null || mat.ClaimedBy is null)
                lostMat = true;
            else
            {
                try { await _api.ClaimMatAsync(claim.Number, claim.Request); }
                catch (ApiConflictException) { lostMat = true; }
            }
            if (lostMat && !_judgeScheduleClosed)
                ForgetJudgeMatClaim();
        }
        JudgeScheduleStatus = lostMat ? "Przydział maty wygasł lub został zwolniony."
            : _judgeMatClaim is { } active ? $"Przejęta mata {active.Number} · {active.Request.StationName}"
            : _judgeMats.Length == 0 ? "Organizator nie zapisał jeszcze mat." : "Wybierz matę do przejęcia.";
    }

    private Task ClaimJudgeMatAsync() => RunJudgeScheduleAsync(async () =>
    {
        var mat = SelectedJudgeMat!;
        var heldLock = _sessions!.LockMat(mat.Number);
        try
        {
            var claim = _sessions.LoadMat(mat.Number);
            if (claim is null)
            {
                if (mat.ClaimedBy is not null)
                    throw new InvalidOperationException($"Mata {mat.Number} należy do stanowiska „{mat.ClaimedBy}”.");
                var station = _dialogService.ShowTextInput("Przejęcie maty", "Podaj nazwę stanowiska sędziego.", Environment.MachineName)?.Trim();
                if (string.IsNullOrEmpty(station))
                    return;
                if (station.Length > 100)
                    throw new InvalidOperationException("Nazwa stanowiska może mieć do 100 znaków.");
                claim = new LocalMatClaim(mat.Number, new ClaimMatRequest(Guid.NewGuid(), station, mat.Revision));
                // A retry after a timeout must send the same token and revision.
                _sessions.SaveMat(claim);
            }

            try { await _api!.ClaimMatAsync(mat.Number, claim.Request); }
            catch (ApiConflictException)
            {
                _sessions.DeleteMat(mat.Number);
                UpdateJudgeMats((await _api!.GetScheduleAsync()).Mats);
                throw;
            }

            if (_judgeScheduleClosed)
                return;
            _judgeMatClaim = claim;
            _judgeMatLock = heldLock;
            OnPropertyChanged(nameof(CanChooseJudgeMat));
            OnPropertyChanged(nameof(JudgeMatSession));
            JudgeScheduleStatus = $"Przejęta mata {claim.Number} · {claim.Request.StationName}";
            await FetchJudgeMatsAsync(validateClaim: false);
        }
        finally
        {
            if (_judgeMatLock != heldLock)
                heldLock.Dispose();
        }
    });

    private Task ReleaseJudgeMatAsync() => RunJudgeScheduleAsync(async () =>
    {
        var claim = _judgeMatClaim!;
        await _api!.ReleaseMatAsync(claim.Number, new(claim.Request.SessionToken));
        if (_judgeScheduleClosed)
            return;
        ForgetJudgeMatClaim();
        UpdateJudgeMats((await _api.GetScheduleAsync()).Mats);
        JudgeScheduleStatus = $"Zwolniono matę {claim.Number}.";
    });

    private async Task StartNextScheduledCategoryAsync()
    {
        await RefreshJudgeScheduleAsync();
        if (_judgeMatClaim is not { } claim || FindNextScheduledCategory() is not { } next || !CanStartCategory(next))
            return;
        await OpenJudgingSessionAsync(new JudgingSession(next.Category.Id, next.Name, Guid.NewGuid(), claim.Request.StationName!)
        {
            MatClaimToken = claim.Request.SessionToken
        });
    }

    private void UpdateMatQueueFromSnapshot(TournamentSnapshotDto snapshot)
    {
        var byId = snapshot.Categories.ToDictionary(category => category.Id);
        UpdateJudgeMats(_judgeMats.Select(mat => mat with
        {
            Slots = mat.Slots.Select(slot => byId.TryGetValue(slot.CategoryId, out var category)
                ? slot with { CategoryName = category.Name, IsFinished = category.IsFinished, ClaimedBy = category.ClaimedBy }
                : slot).ToArray()
        }).ToArray());
    }

    private void UpdateJudgeMats(ScheduleMatDto[] mats)
    {
        var number = _judgeMatClaim?.Number ?? SelectedJudgeMat?.Number;
        _judgeMats = mats;
        OnPropertyChanged(nameof(JudgeMats));
        SelectedJudgeMat = _judgeMats.SingleOrDefault(mat => mat.Number == number) ?? _judgeMats.FirstOrDefault();
        OnPropertyChanged(nameof(UnscheduledJudgeCategories));
    }

    private void ForgetJudgeMatClaim()
    {
        try { _sessions!.DeleteMat(_judgeMatClaim!.Number); }
        finally
        {
            _judgeMatClaim = null;
            DisposeJudgeMatLock();
            OnPropertyChanged(nameof(CanChooseJudgeMat));
            OnPropertyChanged(nameof(JudgeMatSession));
        }
    }

    private void DisposeJudgeMatLock()
    {
        _judgeMatLock?.Dispose();
        _judgeMatLock = null;
    }

    internal void CloseJudgeSchedule()
    {
        _judgeScheduleClosed = true;
        DisposeJudgeMatLock();
    }

    private async Task RunJudgeScheduleAsync(Func<Task> task)
    {
        IsLoading = true;
        try { await task(); }
        finally { IsLoading = false; }
    }

    public sealed record MatQueueRow(int Position, ScheduleSlotDto Slot)
    {
        public string StartsAt => FormatTime(Slot.StartsAt);
        public string EndsAt => FormatTime(Slot.EndsAt);
        public string Status => Slot.IsFinished ? "Zakończona"
            : Slot.ClaimedBy is not null ? $"W toku · {Slot.ClaimedBy}" : "Oczekuje";

        private static string FormatTime(DateTime? time) =>
            time?.ToString("dd.MM HH:mm", CultureInfo.InvariantCulture) ?? "—";
    }
}
