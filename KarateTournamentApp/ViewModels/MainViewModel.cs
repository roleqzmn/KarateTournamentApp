using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Input;
using KarateTournamentApp.Models;
using KarateTournamentApp.Services;
using KarateTournamentApp.Services.Dialogs;
using KarateTournamentApp.Services.Navigation;
using KarateTournamentApp.Services.Scheduling;
using KarateTournamentApp.Commands;
using System;
using KarateTournamentApp.Models.ViewItems;

namespace KarateTournamentApp.ViewModels
{
    public class MainViewModel : ViewModelBase
    {
        private readonly CategoryManager _categoryManager;

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

        public ObservableCollection<CategoryViewModel> Categories { get; set; }

        private CategoryViewModel _selectedCategoryForMerge;
        public CategoryViewModel SelectedCategoryForMerge
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
            }
        }

        public MainViewModel(
            CategoryManager categoryManager,
            IDialogService dialogService,
            IWindowService windowService,
            IUiScheduler uiScheduler)
        {
            _categoryManager = categoryManager;
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

            AddParticipantCommand = new RelayCommand(o => CreateParticipant(), o => CanCreateParticipant());
            ImportCommand = new AsyncRelayCommand(ImportDataAsync, HandleAsyncCommandError);
            ExportCommand = new AsyncRelayCommand(
                ExportDataAsync,
                HandleAsyncCommandError,
                () => _categoryManager.DefinedCategories.Any());
            ImportExcelCommand = new AsyncRelayCommand(
                ImportExcelDataAsync,
                HandleAsyncCommandError,
                () => DivideByAge || DivideByBelt);
            CreateExcelTemplateCommand = new AsyncRelayCommand(CreateExcelTemplateAsync, HandleAsyncCommandError);
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
            return !string.IsNullOrWhiteSpace(FirstName) &&
                   !string.IsNullOrWhiteSpace(LastName) &&
                   Age.HasValue && Age > 0 &&
                   CategorySelections.Any(c => c.IsSelected) &&
                   (DivideByAge || DivideByBelt);
        }

        private void CreateParticipant()
        {
            var selectedCategories = CategorySelections
                .Where(c => c.IsSelected)
                .Select(c => c.CategoryType)
                .ToList();

            var newParticipant = new Participant(
                FirstName,
                LastName,
                Age.Value,
                SelectedBelt,
                SelectedSex,
                selectedCategories,
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
            IsLoading = true;
            try
            {
                await _importService.ImportDataAsync(_categoryManager, AllParticipants);
                RefreshCategories();
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async Task ImportExcelDataAsync()
        {
            IsLoading = true;
            try
            {
                await _importService.ImportParticipantsFromExcelAsync(_categoryManager, AllParticipants, DivideByBelt, DivideByAge);
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
            IsLoading = true;
            try
            {
                await _exportService.ExportDataAsync(_categoryManager);
            }
            finally
            {
                IsLoading = false;
            }
        }
    }
}