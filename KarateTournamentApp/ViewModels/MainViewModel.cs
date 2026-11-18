using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Input;
using KarateClassLibrary.Contracts;
using KarateTournamentApp.Models;
using KarateTournamentApp.Services;
using KarateTournamentApp.Services.Api;
using KarateTournamentApp.Services.Dialogs;
using KarateTournamentApp.Services.Navigation;
using KarateTournamentApp.Services.Scheduling;
using KarateTournamentApp.Commands;
using System;
using KarateTournamentApp.Models.ViewItems;

namespace KarateTournamentApp.ViewModels
{
    public partial class MainViewModel : ViewModelBase
    {
        private readonly CategoryManager _categoryManager;
        private readonly Func<CreateParticipantRequest, CancellationToken, Task<ParticipantDto>>? _createApiParticipant;

        // Form data
        private string _firstName;
        public string FirstName { get => _firstName; set { _firstName = value; OnPropertyChanged(); } }

        private string _lastName;
        public string LastName { get => _lastName; set { _lastName = value; OnPropertyChanged(); } }

        private int? _age; 
        public int? Age { get => _age; set { _age = value; OnPropertyChanged(); } }

        private Sex _selectedSex;
        public Sex SelectedSex { get => _selectedSex; set { _selectedSex = value; OnPropertyChanged(); } }

        private Belts _selectedBelt;
        public Belts SelectedBelt { get => _selectedBelt; set { _selectedBelt = value; OnPropertyChanged(); } }

        private string? _club;
        public string? Club { get => _club; set { _club = value; OnPropertyChanged(); } }

        private bool _divideByAge;
        public bool DivideByAge 
        { 
            get => _divideByAge; 
            set 
            { 
                _divideByAge = value; 
                OnPropertyChanged();
                CommandManager.InvalidateRequerySuggested();
            } 
        }

        private bool _divideByBelt;
        public bool DivideByBelt 
        { 
            get => _divideByBelt; 
            set 
            { 
                _divideByBelt = value; 
                OnPropertyChanged();
                CommandManager.InvalidateRequerySuggested();
            } 
        }

        public ObservableCollection<CategorySelectionItem> CategorySelections { get; set; }

        public ObservableCollection<Participant> AllParticipants { get; set; }
        public ObservableCollection<ParticipantDto> ApiParticipants { get; } = new();

        public ObservableCollection<CategoryViewModel> Categories { get; set; }

        private CategoryViewModel? _selectedCategoryForMerge;
        public CategoryViewModel? SelectedCategoryForMerge
        {
            get => _selectedCategoryForMerge;
            set
            {
                _selectedCategoryForMerge = value;
                OnPropertyChanged();
            }
        }

        public ICommand AddParticipantCommand { get; }
        public ICommand ImportCommand { get; }
        public ICommand ExportCommand { get; }
        public ICommand ImportExcelCommand { get; }
        public ICommand CreateExcelTemplateCommand { get; }
        public ICommand LoadApiTournamentCommand { get; }
        public ICommand ReturnToLocalTournamentCommand { get; }

        private bool _isApiPreview;
        public bool IsApiPreview
        {
            get => _isApiPreview;
            private set
            {
                _isApiPreview = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsLocalTournament));
                OnPropertyChanged(nameof(AddParticipantLabel));
                OnPropertyChanged(nameof(CanManageApi));
                CommandManager.InvalidateRequerySuggested();
            }
        }

        public bool IsLocalTournament => !IsApiPreview;
        public bool IsJudgeStation { get; }
        public bool ShowOrganizerTools => !IsJudgeStation;
        public string AddParticipantLabel => IsEditingApiParticipant ? "ZAPISZ ZMIANY"
            : IsApiPreview ? "ZAPISZ ZAWODNIKA W API" : "DODAJ ZAWODNIKA";

        private readonly ImportService _importService;

        private readonly ExportService _exportService;

        private readonly IDialogService _dialogService;
        private readonly IWindowService _windowService;
        private readonly IUiScheduler _uiScheduler;

        private bool _isLoading;
        public bool IsLoading
        {
            get => _isLoading;
            set
            {
                _isLoading = value;
                OnPropertyChanged();
                CommandManager.InvalidateRequerySuggested();
            }
        }

        internal MainViewModel(CategoryManager categoryManager, IDialogService dialogService, IWindowService windowService,
            IUiScheduler uiScheduler, TournamentApiClient api, bool isJudgeStation = false)
            : this(categoryManager, dialogService, windowService, uiScheduler, api.GetSnapshotAsync, api.CreateParticipantAsync)
        {
            _api = api;
            IsJudgeStation = isJudgeStation;
            _sessions = new JudgingSessionStore(api.SessionScope);
            RefreshJudgingSessions();
        }

        public MainViewModel(
            CategoryManager categoryManager,
            IDialogService dialogService,
            IWindowService windowService,
            IUiScheduler uiScheduler,
            Func<CancellationToken, Task<TournamentSnapshotDto>>? loadApiSnapshot = null,
            Func<CreateParticipantRequest, CancellationToken, Task<ParticipantDto>>? createApiParticipant = null)
        {
            _categoryManager = categoryManager;
            _createApiParticipant = createApiParticipant;
            _dialogService = dialogService ?? throw new ArgumentNullException(nameof(dialogService));
            _windowService = windowService ?? throw new ArgumentNullException(nameof(windowService));
            _uiScheduler = uiScheduler ?? throw new ArgumentNullException(nameof(uiScheduler));
            _importService = new ImportService(_dialogService);
            _exportService = new ExportService(_dialogService);

            CategorySelections = new ObservableCollection<CategorySelectionItem>
            {
                new CategorySelectionItem(CategoryType.Kata, "Kata"),
                new CategorySelectionItem(CategoryType.Kumite, "Kumite"),
                new CategorySelectionItem(CategoryType.Kihon, "Kihon"),
                new CategorySelectionItem(CategoryType.KobudoShort, "Kobudo (Short)"),
                new CategorySelectionItem(CategoryType.KobudoLong, "Kobudo (Long)"),
                new CategorySelectionItem(CategoryType.Grappling, "Grappling")
            };

            AllParticipants = new ObservableCollection<Participant>();
            Categories = new ObservableCollection<CategoryViewModel>();

            AddParticipantCommand = new AsyncRelayCommand(CreateParticipantAsync, HandleAsyncCommandError, CanCreateParticipant);
            ImportCommand = new AsyncRelayCommand(ImportDataAsync, HandleAsyncCommandError,
                () => IsLocalTournament && !IsLoading);
            ExportCommand = new AsyncRelayCommand(
                ExportDataAsync,
                HandleAsyncCommandError,
                () => IsLocalTournament && !IsLoading && _categoryManager.DefinedCategories.Any());
            ImportExcelCommand = new AsyncRelayCommand(
                ImportExcelDataAsync,
                HandleAsyncCommandError,
                () => !IsLoading && (CanManageApi || (IsLocalTournament && (DivideByAge || DivideByBelt))));
            CreateExcelTemplateCommand = new AsyncRelayCommand(CreateExcelTemplateAsync, HandleAsyncCommandError);
            LoadApiTournamentCommand = new AsyncRelayCommand(
                () => LoadApiTournamentAsync(loadApiSnapshot!), HandleAsyncCommandError,
                () => loadApiSnapshot is not null && !IsLoading);
            ReturnToLocalTournamentCommand = new RelayCommand(_ => ReturnToLocalTournament(),
                _ => IsApiPreview && !IsLoading && _activeJudgeLock is null);
            StartApiJudgingCommand = new AsyncRelayCommand<CategoryViewModel>(StartApiJudgingAsync,
                HandleAsyncCommandError, CanStartApiJudging);
            ResumeApiJudgingCommand = new AsyncRelayCommand<JudgingSession>(ResumeApiJudgingAsync,
                HandleAsyncCommandError, session => session is not null && CanUseSessions() && session.Category?.IsFinished != true);
            ReleaseApiJudgingCommand = new AsyncRelayCommand<JudgingSession>(ReleaseApiJudgingAsync,
                HandleAsyncCommandError, session => session is not null && CanUseSessions());
            SubmitApiJudgingCommand = new AsyncRelayCommand<JudgingSession>(SubmitApiJudgingAsync,
                HandleAsyncCommandError, session => CanUseSessions() && session?.Category?.IsFinished == true);
            ArchiveApiJudgingCommand = new AsyncRelayCommand<JudgingSession>(ArchiveApiJudgingAsync,
                HandleAsyncCommandError, session => session is not null && CanUseSessions());
            RecoverApiCategoryCommand = new AsyncRelayCommand<CategoryViewModel>(RecoverApiCategoryAsync,
                HandleAsyncCommandError, item => item is not null && CanChangeApi() && _api!.HasRecoveryKey
                    && _apiSnapshot!.Categories.Any(category => category.Id == item.Category.Id && category.IsClaimed && !category.IsFinished));
            CreateApiCategoryCommand = new AsyncRelayCommand(() => EditApiCategoryAsync(null), HandleAsyncCommandError, CanChangeApi);
            EditApiCategoryCommand = new AsyncRelayCommand<CategoryViewModel>(EditApiCategoryAsync, HandleAsyncCommandError, CanEditApiCategory);
            DeleteApiCategoryCommand = new AsyncRelayCommand<CategoryViewModel>(DeleteApiCategoryAsync, HandleAsyncCommandError, CanEditApiCategory);
            EditApiParticipantCommand = new RelayCommand(_ => BeginApiParticipantEdit(), _ => CanChangeApi() && SelectedApiParticipant is not null);
            DeleteApiParticipantCommand = new AsyncRelayCommand(DeleteApiParticipantAsync, HandleAsyncCommandError,
                () => CanChangeApi() && SelectedApiParticipant is not null && !IsEditingApiParticipant);
            CancelApiParticipantEditCommand = new RelayCommand(_ => ResetForm(), _ => IsEditingApiParticipant && !IsLoading);
        }

        private async Task LoadApiTournamentAsync(Func<CancellationToken, Task<TournamentSnapshotDto>> loadSnapshot)
        {
            IsLoading = true;
            try
            {
                ShowApiSnapshot(await loadSnapshot(CancellationToken.None));
            }
            finally
            {
                IsLoading = false;
            }
        }

        private void ReturnToLocalTournament()
        {
            if (IsEditingApiParticipant)
                ResetForm();
            RefreshCategories();
            IsApiPreview = false;
        }

        private static string FormatApiStatus(ApiCategorySnapshot item)
        {
            if (item.Category.IsFinished)
                return "Zakończona";
            if (!item.IsClaimed)
                return "Dostępna";
            return string.IsNullOrWhiteSpace(item.ClaimedBy) ? "Zajęta" : $"Zajęta · {item.ClaimedBy}";
        }

        private void HandleAsyncCommandError(Exception exception)
        {
            _dialogService.ShowMessage(
                $"Wystąpił nieoczekiwany błąd:\n{exception.Message}",
                "Błąd",
                DialogButtons.Ok,
                DialogIcon.Error);
        }

        private bool CanCreateParticipant()
        {
            if (IsJudgeStation || IsLoading || string.IsNullOrWhiteSpace(FirstName) || string.IsNullOrWhiteSpace(LastName)
                || Age is not int age || !CategorySelections.Any(category => category.IsSelected))
                return false;

            return IsApiPreview
                ? _createApiParticipant is not null && age >= 0 && Enum.IsDefined(SelectedSex) && Enum.IsDefined(SelectedBelt)
                : age > 0 && (DivideByAge || DivideByBelt);
        }

        private async Task CreateParticipantAsync()
        {
            if (Age is not int age)
                return;
            var selectedCategories = CategorySelections
                .Where(c => c.IsSelected)
                .Select(c => c.CategoryType)
                .ToArray();

            if (IsApiPreview && _createApiParticipant is not null)
            {
                var request = new CreateParticipantRequest(FirstName, LastName, age, Club,
                    SelectedBelt, SelectedSex, selectedCategories);
                IsLoading = true;
                try
                {
                    var saved = _editingParticipantId is int id
                        ? await _api!.UpdateParticipantAsync(id, request)
                        : await _createApiParticipant(request, CancellationToken.None);
                    ShowApiSnapshot(_apiSnapshot! with
                    {
                        Participants = _apiSnapshot.Participants.Where(person => person.Id != saved.Id).Append(saved).ToArray()
                    });
                    ResetForm();
                }
                finally
                {
                    IsLoading = false;
                }
                return;
            }

            var newParticipant = new Participant(
                FirstName,
                LastName,
                age,
                SelectedBelt,
                SelectedSex,
                selectedCategories.ToList(),
                Club
            );

            _categoryManager.AssignParticipant(newParticipant, DivideByBelt, DivideByAge);
            AllParticipants.Add(newParticipant);

            RefreshCategories();
            ResetForm();
        }

        private void RefreshCategories()
        {
            Categories.Clear();
            foreach (var category in _categoryManager.DefinedCategories)
            {
                Categories.Add(new CategoryViewModel(
                    category,
                    OnMergeRequested,
                    OnDeleteRequested,
                    _dialogService,
                    _windowService,
                    _uiScheduler));
            }
            CommandManager.InvalidateRequerySuggested();
        }

        private void OnDeleteRequested(CategoryViewModel categoryToDelete)
        {
            var result = _dialogService.ShowMessage(
                $"Czy na pewno chcesz usunąć tę kategorię?\n\n" +
                $"Nazwa: '{categoryToDelete.Name}'\n" +
                $"Zawodnicy: {categoryToDelete.ParticipantCount}\n\n" +
                $"Uwaga: Ta operacja jest nieodwracalna!\n" +
                $"Zawodnicy pozostaną w systemie, ale zostaną usunięci z tej kategorii.",
                "Potwierdź usunięcie kategorii", DialogButtons.YesNo, DialogIcon.Warning);

            if (result == DialogResult.Yes)
            {
                _categoryManager.DefinedCategories.Remove(categoryToDelete.Category);
                
                RefreshCategories();
                
                _dialogService.ShowMessage($"Kategoria '{categoryToDelete.Name}' została pomyślnie usunięta!",
                    "Sukces", DialogButtons.Ok, DialogIcon.Information);
            }
        }

        private void OnMergeRequested(CategoryViewModel requestingCategory)
        {
            if (SelectedCategoryForMerge == null)
            {
                SelectedCategoryForMerge = requestingCategory;
            }
            else if (SelectedCategoryForMerge == requestingCategory)
            {
                SelectedCategoryForMerge = null;
                _dialogService.ShowMessage("Anulowano wybór kategorii.", "Anulowano", DialogButtons.Ok, DialogIcon.Information);
            }
            else
            {
                if (SelectedCategoryForMerge.Category.CategoryType != requestingCategory.Category.CategoryType)
                {
                    _dialogService.ShowMessage("Nie można połączyć kategorii różnych typów!\n\n" +
                        $"Wybrana: {SelectedCategoryForMerge.CategoryTypeDisplay}\n" +
                        $"Druga: {requestingCategory.CategoryTypeDisplay}",
                        "Błąd", DialogButtons.Ok, DialogIcon.Error);
                    SelectedCategoryForMerge = null;
                    return;
                }

                var result = _dialogService.ShowMessage(
                    $"Czy na pewno chcesz połączyć kategorie:\n\n" +
                    $"'{SelectedCategoryForMerge.Name}'\n" +
                    $"Zawodnicy: {SelectedCategoryForMerge.ParticipantCount}\n\n" +
                    $"z\n\n" +
                    $"'{requestingCategory.Name}'\n" +
                    $"Zawodnicy: {requestingCategory.ParticipantCount}\n\n" +
                    $"Po połączeniu: {SelectedCategoryForMerge.ParticipantCount + requestingCategory.ParticipantCount} zawodników",
                    "Potwierdź połączenie", DialogButtons.YesNo, DialogIcon.Question);

                if (result == DialogResult.Yes)
                {
                    requestingCategory.Category.MergeWith(SelectedCategoryForMerge.Category);
                    _categoryManager.DefinedCategories.Remove(SelectedCategoryForMerge.Category);
                    
                    SelectedCategoryForMerge = null;
                    RefreshCategories();
                    
                    _dialogService.ShowMessage("Kategorie zostały pomyślnie połączone!", "Sukces", DialogButtons.Ok, DialogIcon.Information);
                }
            }
        }

        private void ResetForm()
        {
            _editingParticipantId = null;
            OnPropertyChanged(nameof(IsEditingApiParticipant));
            OnPropertyChanged(nameof(AddParticipantLabel));
            FirstName = string.Empty;
            LastName = string.Empty;
            Age = null;
            Club = string.Empty;
            
            foreach (var category in CategorySelections)
            {
                category.IsSelected = false;
            }
        }

        private async Task ImportDataAsync()
        {
            var filePath = _dialogService.ShowOpenFileDialog(
                "Import Tournament Data",
                "JSON files (*.json)|*.json|All files (*.*)|*.*");
            if (filePath == null)
            {
                return;
            }

            IsLoading = true;
            try
            {
                await _importService.ImportDataAsync(filePath, _categoryManager, AllParticipants);
                RefreshCategories();
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async Task ImportExcelDataAsync()
        {
            var filePath = _dialogService.ShowOpenFileDialog(
                "Import Participants from Excel",
                "Excel files (*.xlsx;*.xls)|*.xlsx;*.xls|All files (*.*)|*.*");
            if (filePath == null)
            {
                return;
            }

            IsLoading = true;
            try
            {
                if (IsApiPreview)
                {
                    await ImportApiParticipantsAsync(filePath);
                    return;
                }
                await _importService.ImportParticipantsFromExcelAsync(
                    filePath,
                    _categoryManager,
                    AllParticipants,
                    DivideByBelt,
                    DivideByAge);
                RefreshCategories();
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async Task CreateExcelTemplateAsync()
        {
            await _importService.CreateSampleExcelFileAsync();
        }

        private async Task ExportDataAsync()
        {
            var filePath = _dialogService.ShowSaveFileDialog(
                "Exportuj dane turnieju",
                "JSON files (*.json)|*.json|All files (*.*)|*.*",
                $"tournament_data_{DateTime.Now:yyyy-MM-dd_HH-mm}.json");
            if (filePath == null)
            {
                return;
            }

            IsLoading = true;
            try
            {
                await _exportService.ExportDataAsync(filePath, _categoryManager);
            }
            finally
            {
                IsLoading = false;
            }
        }
    }
}
