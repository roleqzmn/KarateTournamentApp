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

    public bool CanManageApi => IsApiPreview && _api is not null;
    public bool IsEditingApiParticipant => _editingParticipantId.HasValue;
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

    private bool CanEditApiCategory(CategoryViewModel category) =>
        CanChangeApi() && _apiSnapshot!.Categories.Any(existing =>
            existing.Id == category.Category.Id && !existing.IsClaimed && !existing.IsFinished
            && existing.Teams.Count == 0 && existing.BracketMatches.Count == 0 && existing.FinalResults.Count == 0);

    private void ShowApiSnapshot(TournamentSnapshotDto snapshot)
    {
        var categories = TournamentSnapshotMapper.MapCategories(snapshot)
            .Select(item => new CategoryViewModel(item.Category, OnMergeRequested, OnDeleteRequested,
                _dialogService, _windowService, _uiScheduler, FormatApiStatus(item))).ToArray();
        _apiSnapshot = snapshot;
        SelectedCategoryForMerge = null;
        Categories.Clear();
        foreach (var category in categories)
            Categories.Add(category);
        ApiParticipants.Clear();
        foreach (var participant in snapshot.Participants.OrderByDescending(participant => participant.Id))
            ApiParticipants.Add(participant);
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

    private Task EditApiCategoryAsync(CategoryViewModel? category)
    {
        var categoryId = category?.Category.Id;
        var current = _apiSnapshot!.Categories.SingleOrDefault(item => item.Id == categoryId);
        IWindowHandle? window = null;
        var editor = new ApiCategoryEditorViewModel(current, _apiSnapshot.Participants, async request =>
        {
            IsLoading = true;
            try
            {
                var saved = await _api!.SaveCategoryAsync(categoryId, request);
                ShowApiSnapshot(_apiSnapshot with
                {
                    Categories = _apiSnapshot.Categories.Where(item => item.Id != saved.Id).Append(saved)
                        .OrderBy(item => item.Id).ToArray()
                });
                window?.Close();
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
        return Task.CompletedTask;
    }

    private async Task DeleteApiCategoryAsync(CategoryViewModel category)
    {
        if (!ConfirmApiDelete($"Usunąć konkurencję „{category.Name}”? Zawodnicy pozostaną w rejestrze."))
            return;
        IsLoading = true;
        try
        {
            await _api!.DeleteCategoryAsync(category.Category.Id);
            ShowApiSnapshot(_apiSnapshot! with
            {
                Categories = _apiSnapshot.Categories.Where(item => item.Id != category.Category.Id).ToArray()
            });
        }
        finally { IsLoading = false; }
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
        if (_dialogService.ShowMessage($"Dodać do rejestru {participants.Count} zawodników z pliku?",
            "Import Excel", DialogButtons.YesNo, DialogIcon.Question) != DialogResult.Yes)
            return;

        var requests = participants.Select(person => new CreateParticipantRequest(person.FirstName, person.LastName,
            person.Age, person.Club, person.Belt, person.Sex, person.Categories.ToArray())).ToArray();
        var saved = await _api!.ImportParticipantsAsync(requests);
        ShowApiSnapshot(_apiSnapshot! with { Participants = _apiSnapshot.Participants.Concat(saved).ToArray() });
        _dialogService.ShowMessage($"Zapisano zawodników: {saved.Length}.", "Import Excel");
    }
}
