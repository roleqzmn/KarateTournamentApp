using System.Windows.Input;
using KarateClassLibrary.Contracts;
using KarateTournamentApp.Services;
using KarateTournamentApp.Services.Api;
using KarateTournamentApp.Services.Dialogs;
using KarateTournamentApp.Services.Navigation;

namespace KarateTournamentApp.ViewModels;

public partial class MainViewModel
{
    private readonly TournamentApiClient? _api;
    private TournamentSnapshotDto? _apiSnapshot;
    private int? _editingParticipantId;
    private ParticipantDto? _selectedApiParticipant;

    public bool CanManageApi => ShowOrganizerTools && IsApiPreview && _api is not null;
    public bool IsEditingApiParticipant => _editingParticipantId.HasValue;
    public CategoryGrouping SelectedCategoryGrouping { get; set; } = CategoryGrouping.AgeAndBelt;
    public IReadOnlyDictionary<string, CategoryGrouping> OrganizerGroupingOptions { get; } =
        new Dictionary<string, CategoryGrouping>
        {
            ["Wiek i pas"] = CategoryGrouping.AgeAndBelt,
            ["Wiek"] = CategoryGrouping.Age,
            ["Pas"] = CategoryGrouping.Belt
        };

    public ICommand CreateApiCategoryCommand { get; }
    public ICommand EditApiCategoryCommand { get; }
    public ICommand DeleteApiCategoryCommand { get; }
    public ICommand EditApiParticipantCommand { get; }
    public ICommand DeleteApiParticipantCommand { get; }
    public ICommand CancelApiParticipantEditCommand { get; }

    public ParticipantDto? SelectedApiParticipant
    {
        get => _selectedApiParticipant;
        set
        {
            _selectedApiParticipant = value;
            OnPropertyChanged();
            CommandManager.InvalidateRequerySuggested();
        }
    }

    private bool CanChangeApi() => CanManageApi && !IsLoading;

    private bool CanEditApiCategory(OrganizerCompetitionRow? row) =>
        CanChangeApi() && row is { Status: OrganizerCompetitionStatus.Available, Summary.TeamCount: 0 };

    private static bool CanEditApiCategoryDetails(CategoryDto? category) =>
        category is { IsClaimed: false, IsFinished: false, Teams.Count: 0, BracketMatches.Count: 0, FinalResults.Count: 0 };

    private CategoryViewModel CreateCategoryViewModel(ApiCategorySnapshot item) =>
        new(item.Category, OnMergeRequested, OnDeleteRequested,
            _dialogService, _windowService, _uiScheduler, FormatApiStatus(item));

    private void ShowApiSnapshot(TournamentSnapshotDto snapshot)
    {
        if (IsJudgeStation || _api is null)
        {
            var categories = TournamentSnapshotMapper.MapCategories(snapshot).Select(CreateCategoryViewModel).ToArray();
            SelectedCategoryForMerge = null;
            Categories.Clear();
            foreach (var category in categories)
                Categories.Add(category);
        }

        _apiSnapshot = snapshot;
        if (!IsJudgeStation)
        {
            var selectedId = SelectedApiParticipant?.Id;
            ApiParticipants.Clear();
            foreach (var participant in snapshot.Participants.OrderByDescending(participant => participant.Id))
                ApiParticipants.Add(participant);
            SelectedApiParticipant = ApiParticipants.FirstOrDefault(participant => participant.Id == selectedId);
        }
        IsApiPreview = true;
    }

    private void BeginApiParticipantEdit()
    {
        if (SelectedApiParticipant is not { } participant)
            return;
        _editingParticipantId = participant.Id;
        FirstName = participant.FirstName;
        LastName = participant.LastName;
        Age = participant.Age;
        Club = participant.Club;
        SelectedBelt = participant.Belt;
        SelectedSex = participant.Sex;
        foreach (var category in CategorySelections)
            category.IsSelected = participant.CategoryTypes.Contains(category.CategoryType);
        OnPropertyChanged(nameof(IsEditingApiParticipant));
        OnPropertyChanged(nameof(AddParticipantLabel));
    }

    private async Task EditApiCategoryAsync(OrganizerCompetitionRow? row)
    {
        var categoryId = row?.Summary.Id;
        TournamentSnapshotDto snapshot;
        IsLoading = true;
        try
        {
            snapshot = await _api!.GetSnapshotAsync();
            ShowRefreshedCategories(snapshot.Categories.Select(SummarizeCategory));
        }
        finally { IsLoading = false; }

        var current = snapshot.Categories.SingleOrDefault(item => item.Id == categoryId);
        if (categoryId.HasValue && !CanEditApiCategoryDetails(current))
        {
            _dialogService.ShowMessage(current is null ? "Konkurencja nie jest już dostępna."
                : "Konkurencja jest zajęta, zakończona albo ma dane blokujące edycję.", "Edycja konkurencji");
            return;
        }

        IWindowHandle? window = null;
        var editor = new ApiCategoryEditorViewModel(current, snapshot.Participants, async request =>
        {
            IsLoading = true;
            try
            {
                var saved = await _api!.SaveCategoryAsync(categoryId, request);
                ReplaceOrganizerCategories(_organizerCategories.Where(row => row.Summary.Id != saved.Id)
                    .Append(new OrganizerCompetitionRow(SummarizeCategory(saved))));
                window?.Close();
            }
            catch (ApiConflictException)
            {
                await RefreshOrganizerCategoriesAsync(force: true);
                throw;
            }
            finally { IsLoading = false; }
        });
        _windowService.ShowDialog(editor, new WindowOptions
        {
            Title = current is null ? "Nowa konkurencja" : $"Edycja · {current.Name}",
            Width = 660, Height = 710, StartupPosition = WindowStartupPosition.CenterOwner
        }, handle =>
        {
            window = handle;
            handle.Closed += (_, _) => window = null;
        });
    }

    private async Task DeleteApiCategoryAsync(OrganizerCompetitionRow row)
    {
        if (!ConfirmApiDelete($"Usunąć konkurencję „{row.Summary.Name}”? Zawodnicy pozostaną w rejestrze."))
            return;
        IsLoading = true;
        try
        {
            await _api!.DeleteCategoryAsync(row.Summary.Id);
            ReplaceOrganizerCategories(_organizerCategories.Where(other => other.Summary.Id != row.Summary.Id));
        }
        catch (ApiConflictException)
        {
            await RefreshOrganizerCategoriesAsync(force: true);
            throw;
        }
        finally { IsLoading = false; }
    }

    private async Task RecoverApiCategoryAsync(OrganizerCompetitionRow row)
    {
        IsLoading = true;
        try
        {
            var target = await _api!.GetRecoveryTargetAsync(row.Summary.Id);
            if (_dialogService.ShowMessage(
                $"Odzyskać konkurencję „{row.Summary.Name}” ze stanowiska „{target.StationName}”? Stara sesja utraci prawo przesłania wyników.",
                "Odzyskaj konkurencję", DialogButtons.YesNo, DialogIcon.Warning) != DialogResult.Yes)
                return;
            await _api.RecoverClaimAsync(row.Summary.Id, new(target.ClaimReference));
            ReplaceOrganizerCategories(_organizerCategories.Select(item => item.Summary.Id != row.Summary.Id ? item
                : new OrganizerCompetitionRow(item.Summary with { IsClaimed = false, ClaimedBy = null })));
        }
        catch (ApiConflictException)
        {
            await RefreshOrganizerCategoriesAsync(force: true);
            throw;
        }
        finally { IsLoading = false; }
    }

    private async Task ViewApiCategoryResultsAsync(OrganizerCompetitionRow row)
    {
        CategoryViewModel? category;
        IsLoading = true;
        try
        {
            var snapshot = await _api!.GetSnapshotAsync();
            ShowRefreshedCategories(snapshot.Categories.Select(SummarizeCategory));
            category = TournamentSnapshotMapper.MapCategories(snapshot with
            {
                Categories = snapshot.Categories.Where(item => item.Id == row.Summary.Id && item.IsFinished).ToArray()
            }).Select(CreateCategoryViewModel).SingleOrDefault();
        }
        finally { IsLoading = false; }

        if (category?.ViewResultsCommand.CanExecute(null) != true)
            _dialogService.ShowMessage("Brak zapisanych wyników tej konkurencji.", "Wyniki");
        else
            category.ViewResultsCommand.Execute(null);
    }

    private async Task DeleteApiParticipantAsync()
    {
        if (SelectedApiParticipant is not { } participant
            || !ConfirmApiDelete($"Usunąć zawodnika {participant.FirstName} {participant.LastName} (ID {participant.Id})?"))
            return;
        IsLoading = true;
        try
        {
            await _api!.DeleteParticipantAsync(participant.Id);
            ShowApiSnapshot(_apiSnapshot! with
            {
                Participants = _apiSnapshot.Participants.Where(item => item.Id != participant.Id).ToArray()
            });
        }
        finally { IsLoading = false; }
    }

    private bool ConfirmApiDelete(string message) =>
        _dialogService.ShowMessage(message, "Potwierdź usunięcie", DialogButtons.YesNo, DialogIcon.Warning) == DialogResult.Yes;

    private async Task ImportApiParticipantsAsync(string filePath)
    {
        var participants = await new ExcelImportService().ImportParticipantsFromExcelAsync(filePath, rejectInvalidRows: true);
        if (participants.Count == 0)
        {
            _dialogService.ShowMessage("Plik nie zawiera zawodników.", "Import Excel");
            return;
        }
        if (_dialogService.ShowMessage($"Dodać {participants.Count} zawodników z pliku i przydzielić ich do konkurencji?",
            "Import Excel", DialogButtons.YesNo, DialogIcon.Question) != DialogResult.Yes)
            return;

        var requests = participants.Select(person => new CreateParticipantRequest(person.FirstName, person.LastName,
            person.Age, person.Club, person.Belt, person.Sex, person.Categories.ToArray())).ToArray();
        var saved = await _api!.ImportParticipantsAsync(requests, SelectedCategoryGrouping);
        ShowApiSnapshot(_apiSnapshot! with { Participants = _apiSnapshot.Participants.Concat(saved).ToArray() });
        await RefreshOrganizerCategoriesAsync(force: true);
        _dialogService.ShowMessage($"Zapisano zawodników: {saved.Length}.", "Import Excel");
    }
}
