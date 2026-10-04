using System.Windows.Input;
using KarateTournamentApp.Models;
using KarateTournamentApp.Commands;
using KarateTournamentApp.Services.Dialogs;
using KarateTournamentApp.Services.Navigation;

namespace KarateTournamentApp.ViewModels
{
    public class CategoryViewModel : ViewModelBase
    {
        private readonly Category _category;
        private readonly Action<CategoryViewModel> _mergeRequestCallback;
        private readonly Action<CategoryViewModel> _deleteRequestCallback;
        private readonly IDialogService _dialogService;
        private readonly IWindowService _windowService;

        public CategoryViewModel(
            Category category,
            Action<CategoryViewModel> mergeRequestCallback,
            Action<CategoryViewModel> deleteRequestCallback,
            IDialogService dialogService,
            IWindowService windowService)
        {
            _category = category;
            _mergeRequestCallback = mergeRequestCallback;
            _deleteRequestCallback = deleteRequestCallback;
            _dialogService = dialogService ?? throw new ArgumentNullException(nameof(dialogService));
            _windowService = windowService ?? throw new ArgumentNullException(nameof(windowService));
            MergeCommand = new RelayCommand(o => RequestMerge(), o => true);
            RenameCommand = new RelayCommand(o => RenameCategory(), o => true);
            RemoveParticipantCommand = new RelayCommand(RemoveParticipant, o => true);
            StartCompetitionCommand = new RelayCommand(o => StartCompetition(), o => CanStartCompetition());
            DeleteCommand = new RelayCommand(o => RequestDelete(), o => CanDelete());
            InitializeBracketCommand = new RelayCommand(o => InitializeBracket(), o => CanInitializeBracket());
            ViewResultsCommand = new RelayCommand(o => ViewResults(), o => CanViewResults());
        }

        public Category Category => _category;

        public string Name => _category.Name;
        public int ParticipantCount => _category.Participants.Count;
        public string CategoryTypeDisplay => _category.CategoryType.ToString();
        public string SexDisplay => _category.Sex.ToString();
        public bool HasKumiteWinner =>
            _category.CategoryType == CategoryType.Kumite
            && _category.IsFinished
            && _category.BracketMatches.Any()
            && _category.BracketMatches[0].WinnerId.HasValue;

        public string KumiteWinnerDisplay
        {
            get
            {
                if (!HasKumiteWinner)
                {
                    return string.Empty;
                }

                var winnerId = _category.BracketMatches[0].WinnerId!.Value;
                var winner = _category.Participants.FirstOrDefault(p => p.Id == winnerId);
                return winner != null ? $"Zwyciezca: {winner.FullName}" : "Zwyciezca: ---";
            }
        }

        public string AgeRangeDisplay
        {
            get
            {
                if (_category.MinAge.HasValue && _category.MaxAge.HasValue)
                {
                    if (_category.MinAge == _category.MaxAge)
                        return $"{_category.MinAge} lat";
                    return $"{_category.MinAge}-{_category.MaxAge} lat";
                }
                return "Brak";
            }
        }

        private bool _isExpanded;
        public bool IsExpanded
        {
            get => _isExpanded;
            set
            {
                _isExpanded = value;
                OnPropertyChanged();
            }
        }

        public ICommand MergeCommand { get; }
        public ICommand RenameCommand { get; }
        public ICommand RemoveParticipantCommand { get; }
        public ICommand StartCompetitionCommand { get; }
        public ICommand DeleteCommand { get; }
        public ICommand InitializeBracketCommand { get; }
        public ICommand ViewResultsCommand { get; }

        private void RequestMerge()
        {
            _mergeRequestCallback?.Invoke(this);
        }

        private void RenameCategory()
        {
            var newName = _dialogService.ShowTextInput("Zmiana nazwy kategorii", "Podaj nowa nazwe kategorii:", _category.Name);
            if (newName == null)
            {
                return;
            }

            newName = newName.Trim();
            if (string.IsNullOrWhiteSpace(newName))
            {
                _dialogService.ShowMessage("Nazwa kategorii nie moze byc pusta.", "Bledna nazwa", DialogButtons.Ok, DialogIcon.Warning);
                return;
            }

            _category.Name = newName;
            Refresh();
        }

        private void RemoveParticipant(object? parameter)
        {
            if (parameter is Participant participant)
            {
                _category.Participants.Remove(participant);
                OnPropertyChanged(nameof(ParticipantCount));
                Refresh();
            }
        }

        private bool CanDelete()
        {
            // Can delete if category hasn't started or is empty
            return !_category.IsFinished;
        }

        private void RequestDelete()
        {
            _deleteRequestCallback?.Invoke(this);
        }

        private bool CanInitializeBracket()
        {
            // Can initialize bracket if:
            // 1. Category has at least 2 participants
            // 2. Category is a bracket-style category (ShobuSanbonCategory or has bracket matches)
            // 3. Bracket hasn't been initialized yet (no matches)
            // 4. Category hasn't finished
            return _category.Participants.Count >= 2 && 
                   _category.BracketMatches.Count == 0 && 
                   !_category.IsFinished;
        }

        private void InitializeBracket()
        {
            try
            {
                if(_category.CategoryType==CategoryType.Kumite)
                    _category.InitializeBracket();
                else
                    _category.Participants.OrderBy(p => p.Id);
                _dialogService.ShowMessage(
                    $"Drabinka została zainicjalizowana!\n\n" +
                    $"Kategoria: {_category.Name}\n" +
                    $"Liczba zawodników: {_category.Participants.Count}\n" +
                    $"Liczba meczy: {_category.BracketMatches.Count}",
                    "Sukces",
                    DialogButtons.Ok,
                    DialogIcon.Information);

                Refresh();
            }
            catch (Exception ex)
            {
                _dialogService.ShowMessage(
                    $"Błąd podczas inicjalizacji drabinki:\n{ex.Message}",
                    "Błąd",
                    DialogButtons.Ok,
                    DialogIcon.Error);
            }
        }

        public void Refresh()
        {
            OnPropertyChanged(nameof(Name));
            OnPropertyChanged(nameof(ParticipantCount));
            OnPropertyChanged(nameof(CategoryTypeDisplay));
            OnPropertyChanged(nameof(SexDisplay));
            OnPropertyChanged(nameof(AgeRangeDisplay));
            OnPropertyChanged(nameof(HasKumiteWinner));
            OnPropertyChanged(nameof(KumiteWinnerDisplay));
            OnPropertyChanged(nameof(Category));
        }

        private bool CanStartCompetition()
        {
            return _category.Participants.Count >= 2 && !_category.IsFinished;
        }

        private bool CanViewResults()
        {
            // Can view results if category is finished and has saved results
            return _category.IsFinished
                   && ((_category.FinalResults != null && _category.FinalResults.Any())
                       || _category.JudgingScores.Any());
        }

        private void ViewResults()
        {
            // Create a competition manager with saved results
            var competitionManager = new IndividualCompetitionManagerViewModel(_category, _windowService);
            
            // Load saved rankings if available, otherwise source from raw judging scores.
            var sourceResults = _category.FinalResults.Any()
                ? _category.FinalResults
                : BuildResultsFromJudgingScores();

            foreach (var result in sourceResults)
            {
                competitionManager.Results.Add(result);
            }

            var resultsViewModel = new ResultsViewModel(competitionManager);
            _windowService.ShowDialog(resultsViewModel, new WindowOptions
            {
                Title = $"Wyniki - {_category.Name}",
                Width = 800,
                Height = 600
            });
        }

        private List<ParticipantResult> BuildResultsFromJudgingScores()
        {
            var results = new List<ParticipantResult>();

            foreach (var (scores, participantId) in _category.JudgingScores)
            {
                var participant = _category.Participants.FirstOrDefault(p => p.Id == participantId);
                if (participant == null)
                {
                    continue;
                }

                var scoreCalculation = JudgingScoreCalculator.CalculateFinalScore(scores);

                results.Add(new ParticipantResult
                {
                    Participant = participant,
                    Score = scoreCalculation.FinalScore,
                    JudgeScores = new List<decimal>(scores),
                    DiscardedJudgeScoreIndexes = scoreCalculation.DiscardedScoreIndexes
                });
            }

            return results;
        }

        private void StartCompetition()
        {
            // Check if this is a Kumite category (bracket-style competition)
            if (_category.CategoryType == CategoryType.Kumite || _category is ShobuSanbonCategory)
            {
                StartBracketCompetition();
            }
            else
            {
                // All other categories are individual (Kata, Kihon, Kobudo, Grappling)
                StartIndividualCompetition();
            }
        }

        private void StartBracketCompetition()
        {
            var competitionManager = new CompetitionManagerViewModel(_category, _dialogService);
            var scoreboardViewModel = new ScoreboardViewModel(competitionManager);
            var judgeViewModel = new ScoreboardJudgeViewModel(competitionManager, _dialogService, scoreboardViewModel);

            var scoreboardHandle = _windowService.Show(scoreboardViewModel, new WindowOptions
            {
                Title = $"Tablica wynikow - {_category.Name}",
                Width = 1200,
                Height = 800,
                SizeState = WindowSizeState.Maximized
            });

            var judgeHandle = _windowService.Show(judgeViewModel, new WindowOptions
            {
                Title = $"Panel sedziowski - {_category.Name}",
                Width = 800,
                Height = 600
            });

            judgeHandle.Closed += (s, e) => Refresh();
            scoreboardHandle.Closed += (s, e) => Refresh();
        }

        private void StartIndividualCompetition()
        {
            var competitionManager = new IndividualCompetitionManagerViewModel(_category, _windowService);
            var scoreboardViewModel = new IndividualScoreboardViewModel(competitionManager);
            var judgeViewModel = new IndividualJudgeViewModel(competitionManager, _windowService);

            _windowService.Show(scoreboardViewModel, new WindowOptions
            {
                Title = $"Tablica wynikow - {_category.Name}",
                Width = 1200,
                Height = 800,
                SizeState = WindowSizeState.Maximized
            });

            _windowService.Show(judgeViewModel, new WindowOptions
            {
                Title = $"Panel sedziowski - {_category.Name}",
                Width = 900,
                Height = 700
            });
        }
    }


}
