using System.Windows.Input;
using KarateTournamentApp.Models;
using KarateTournamentApp.Views;
using KarateTournamentApp.Commands;

namespace KarateTournamentApp.ViewModels
{
    public class CategoryViewModel : ViewModelBase
    {
        private readonly Category _category;
        private readonly Action<CategoryViewModel> _mergeRequestCallback;
        private readonly Action<CategoryViewModel> _deleteRequestCallback;

        public CategoryViewModel(Category category, Action<CategoryViewModel> mergeRequestCallback, Action<CategoryViewModel> deleteRequestCallback)
        {
            _category = category;
            _mergeRequestCallback = mergeRequestCallback;
            _deleteRequestCallback = deleteRequestCallback;
            MergeCommand = new RelayCommand(o => RequestMerge(), o => true);
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
        public ICommand RemoveParticipantCommand { get; }
        public ICommand StartCompetitionCommand { get; }
        public ICommand DeleteCommand { get; }
        public ICommand InitializeBracketCommand { get; }
        public ICommand ViewResultsCommand { get; }

        private void RequestMerge()
        {
            _mergeRequestCallback?.Invoke(this);
        }

        private void RemoveParticipant(object parameter)
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
                System.Windows.MessageBox.Show(
                    $"Drabinka została zainicjalizowana!\n\n" +
                    $"Kategoria: {_category.Name}\n" +
                    $"Liczba zawodników: {_category.Participants.Count}\n" +
                    $"Liczba meczy: {_category.BracketMatches.Count}",
                    "Sukces", 
                    System.Windows.MessageBoxButton.OK, 
                    System.Windows.MessageBoxImage.Information);
                
                Refresh();
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(
                    $"Błąd podczas inicjalizacji drabinki:\n{ex.Message}",
                    "Błąd",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
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
            var competitionManager = new IndividualCompetitionManagerViewModel(_category);
            
            // Load saved rankings if available, otherwise source from raw judging scores.
            var sourceResults = _category.FinalResults.Any()
                ? _category.FinalResults
                : BuildResultsFromJudgingScores();

            foreach (var result in sourceResults)
            {
                competitionManager.Results.Add(result);
            }

            // Open results window
            var resultsWindow = new System.Windows.Window
            {
                Title = $"Wyniki - {_category.Name}",
                Width = 800,
                Height = 600,
                Content = new Views.ResultsView
                {
                    DataContext = new ResultsViewModel(competitionManager)
                }
            };
            resultsWindow.ShowDialog();
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

                var discarded = GetDiscardedIndexes(scores);
                var discardedSet = new HashSet<int>(discarded);
                var finalScore = scores
                    .Where((score, index) => !discardedSet.Contains(index))
                    .Sum();

                results.Add(new ParticipantResult
                {
                    Participant = participant,
                    Score = finalScore,
                    JudgeScores = new List<decimal>(scores),
                    DiscardedJudgeScoreIndexes = discarded
                });
            }

            return results;
        }

        private static List<int> GetDiscardedIndexes(IReadOnlyList<decimal> scores)
        {
            if (scores.Count <= 3)
            {
                return new List<int>();
            }

            var indexed = scores
                .Select((score, index) => new { score, index })
                .OrderBy(x => x.score)
                .ThenBy(x => x.index)
                .ToList();

            return new List<int>
            {
                indexed[0].index,
                indexed[indexed.Count - 1].index
            };
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
            var competitionManager = new CompetitionManagerViewModel(_category);
            var scoreboardViewModel = new ScoreboardViewModel(competitionManager);
            
            // Create and show Scoreboard View (public display)
            var scoreboardWindow = new System.Windows.Window
            {
                Title = $"Tablica wynikow - {_category.Name}",
                Width = 1200,
                Height = 800,
                WindowState = System.Windows.WindowState.Maximized,
                Content = new ScoreboardView
                {
                    DataContext = scoreboardViewModel
                }
            };

            // Create and show Judge Panel
            var judgeWindow = new System.Windows.Window
            {
                Title = $"Panel sedziowski - {_category.Name}",
                Width = 800,
                Height = 600,
                Content = new ScoreboardJudge
                {
                    DataContext = new ScoreboardJudgeViewModel(competitionManager, scoreboardViewModel)
                }
            };

            scoreboardWindow.Show();
            judgeWindow.Show();

            judgeWindow.Closed += (s, e) => Refresh();
            scoreboardWindow.Closed += (s, e) => Refresh();
        }

        private void StartIndividualCompetition()
        {
            var competitionManager = new IndividualCompetitionManagerViewModel(_category);
            var scoreboardViewModel = new IndividualScoreboardViewModel(competitionManager);

            
            // Create and show Scoreboard View (public display)
            var scoreboardWindow = new System.Windows.Window
            {
                Title = $"Tablica wynikow - {_category.Name}",
                Width = 1200,
                Height = 800,
                WindowState = System.Windows.WindowState.Maximized,
                Content = new IndividualScoreboardView
                {
                    DataContext = scoreboardViewModel
                }
            };

            // Create and show Judge Panel
            var judgeWindow = new System.Windows.Window
            {
                Title = $"Panel sedziowski - {_category.Name}",
                Width = 900,
                Height = 700,
                Content = new IndividualJudgeView
                {
                    DataContext = new IndividualJudgeViewModel(competitionManager)
                }
            };

            scoreboardWindow.Show();
            judgeWindow.Show();
        }
    }


}
