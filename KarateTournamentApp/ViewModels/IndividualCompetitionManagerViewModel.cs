using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using KarateClassLibrary.Contracts;
using KarateTournamentApp.Models;
using KarateTournamentApp.Commands;
using KarateTournamentApp.Services.Navigation;
using KarateTournamentApp.Services.Scheduling;

namespace KarateTournamentApp.ViewModels
{
    public class IndividualCompetitionManagerViewModel : ViewModelBase
    {
        private readonly Category _category;
        private readonly TeamCategory? _teamCategory;
        private readonly IWindowService _windowService;
        private readonly IUiScheduler _uiScheduler;
        private int _currentParticipantIndex;
        private bool _requireEveryParticipant;
        
        public Category Category => _category;
        
        public bool IsTeamKata => _teamCategory is not null;
        private IReadOnlyList<IKataEntrant> Entrants => _teamCategory is null ? _category.Participants : _teamCategory.Teams;

        private IKataEntrant? _currentEntrant;
        public Participant? CurrentParticipant
        {
            get => CurrentEntrant as Participant;
            set => CurrentEntrant = value;
        }

        public IKataEntrant? CurrentEntrant
        {
            get => _currentEntrant;
            private set
            {
                _currentEntrant = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(CurrentParticipant));
                OnPropertyChanged(nameof(CurrentParticipantNumber));
                OnPropertyChanged(nameof(TotalParticipants));
                
                // Reset judge scores for new participant
                JudgeScores.Clear();
                IsParticipantFinished = false;
                OnPropertyChanged(nameof(FinalScore));
            }
        }

        public int CurrentParticipantNumber => _currentParticipantIndex + 1;
        public int TotalParticipants => Entrants.Count;

        // Collection to store scores from multiple judges
        public ObservableCollection<decimal> JudgeScores { get; set; }

        private bool _isParticipantFinished;
        public bool IsParticipantFinished
        {
            get => _isParticipantFinished;
            set
            {
                _isParticipantFinished = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(FinalScore));
            }
        }

        // Calculate final score (remove highest and lowest, then sum)
        public decimal FinalScore
        {
            get
            {
                if (!IsParticipantFinished || JudgeScores.Count < 3) return 0;

                return JudgingScoreCalculator.CalculateFinalScore(JudgeScores).FinalScore;
            }
        }

        // Store all participant results
        public ObservableCollection<ParticipantResult> Results { get; set; }
        public ObservableCollection<TeamKataResult> TeamResults { get; } = [];

        public ICommand NextParticipantCommand { get; }
        public ICommand FinishParticipantCommand { get; }
        public ICommand AddJudgeScoreCommand { get; }
        public ICommand RemoveLastJudgeScoreCommand { get; }

        public IndividualCompetitionManagerViewModel(
            Category category,
            IWindowService windowService,
            IUiScheduler uiScheduler)
        {
            _category = category;
            _teamCategory = category as TeamCategory;
            _windowService = windowService ?? throw new ArgumentNullException(nameof(windowService));
            _uiScheduler = uiScheduler ?? throw new ArgumentNullException(nameof(uiScheduler));
            _currentParticipantIndex = 0;
            
            JudgeScores = new ObservableCollection<decimal>();
            Results = new ObservableCollection<ParticipantResult>();
            
            if (Entrants.Count > 0)
                CurrentEntrant = Entrants[_currentParticipantIndex];

            NextParticipantCommand = new RelayCommand(o => MoveToNextParticipant(), o => CanMoveToNext());
            FinishParticipantCommand = new RelayCommand(o => FinishCurrentParticipant(), o => CanFinishParticipant());
            AddJudgeScoreCommand = new RelayCommand(AddJudgeScore);
            RemoveLastJudgeScoreCommand = new RelayCommand(o => RemoveLastScore(), o => JudgeScores.Any());
        }

        private bool CanMoveToNext()
        {
            return !_requireEveryParticipant && _currentParticipantIndex < Entrants.Count - 1;
        }

        private bool CanFinishParticipant()
        {
            return CurrentEntrant != null && JudgeScores.Count >= 3;
        }

        private void MoveToNextParticipant()
        {
            if (_currentParticipantIndex < Entrants.Count - 1)
            {
                _currentParticipantIndex++;
                CurrentEntrant = Entrants[_currentParticipantIndex];
            }
            else
            {
                // Also save raw judge scores for backward compatibility
                foreach (var result in Results)
                {
                    _category.JudgingScores.Add((result.JudgeScores, result.Participant.Id));
                }
                
                _category.IsFinished = true;
                CurrentEntrant = null;
            }
        }

        private void FinishCurrentParticipant()
        {
            if (CurrentEntrant is { } entrant && JudgeScores.Count >= 3)
            {
                IsParticipantFinished = true;

                AddResult(entrant, JudgeScores);
                MoveToNextParticipant();
            }
        }

        private void AddJudgeScore(object parameter)
        {
            TryAddScore(parameter as string);
        }

        internal bool TryAddScore(string? input)
        {
            if (CurrentEntrant is null || !decimal.TryParse(input, out var score) || score is < 0 or > 10
                || (_requireEveryParticipant && !KarateClassLibrary.Scoring.JudgingScoreCalculator.IsValidJudgeNote(score)))
                return false;
            JudgeScores.Add(score);
            OnPropertyChanged(nameof(FinalScore));
            return true;
        }

        internal void RestoreDraft(IndividualJudgeDraft draft) => RestoreProgress(
            draft.ParticipantIndex, draft.CurrentScores, draft.Results.Select(result => (result.ParticipantId, result.Notes)));

        internal void RestoreTeamDraft(TeamJudgeDraft draft) => RestoreProgress(
            draft.TeamIndex, draft.CurrentScores, draft.Results.Select(result => (result.TeamId, result.Notes)));

        private void RestoreProgress(int index, decimal[] currentScores, IEnumerable<(int Id, decimal[] Notes)> results)
        {
            _requireEveryParticipant = true;
            _currentParticipantIndex = index;
            CurrentEntrant = _category.IsFinished ? null : Entrants[index];
            Results.Clear();
            TeamResults.Clear();
            foreach (var (id, notes) in results)
                AddResult(Entrants.Single(entrant => entrant.Id == id), notes);
            foreach (var note in currentScores)
                JudgeScores.Add(note);
        }

        internal IndividualJudgeDraft CaptureDraft() => new(_currentParticipantIndex, JudgeScores.ToArray(),
            Results.Select(result => new IndividualDraftScore(result.Participant.Id, result.JudgeScores.ToArray())).ToArray());

        internal TeamJudgeDraft CaptureTeamDraft() => new(_currentParticipantIndex, JudgeScores.ToArray(),
            TeamResults.Select(result => new TeamDraftScore(result.Team.Id, result.JudgeScores.ToArray())).ToArray());

        private void AddResult(IKataEntrant entrant, IList<decimal> notes)
        {
            var scoreNotes = notes.ToList();
            var (score, discarded) = JudgingScoreCalculator.CalculateFinalScore(scoreNotes);
            if (entrant is Team team)
                TeamResults.Add(new TeamKataResult { Team = team, Score = score, JudgeScores = scoreNotes, DiscardedJudgeScoreIndexes = discarded });
            else
                Results.Add(new ParticipantResult { Participant = (Participant)entrant, Score = score,
                    JudgeScores = scoreNotes, DiscardedJudgeScoreIndexes = discarded });
        }

        private void RemoveLastScore()
        {
            if (JudgeScores.Any())
            {
                JudgeScores.RemoveAt(JudgeScores.Count - 1);
                OnPropertyChanged(nameof(FinalScore));
            }
        }

        private TResult? ResolveDraw<TResult>(TResult first, TResult second) where TResult : KataPerformanceResult
        {
            var scoreboardViewModel = new DrawResolverScoreboardViewModel(first.Entrant, second.Entrant);
            var drawResolver = new DrawResolverViewModel(first.Entrant, second.Entrant, scoreboardViewModel);

            var scoreboardHandle = _windowService.Show(scoreboardViewModel, new WindowOptions
            {
                Title = "DOGRYWKA - PUBLICZNA TABLICA",
                Width = 1920,
                Height = 1080,
                SizeState = WindowSizeState.Maximized,
                Borderless = true
            });

            IKataEntrant? winner = null;
            IWindowHandle? judgeHandle = null;

            drawResolver.WinnerConfirmed += (sender, selectedWinner) =>
            {
                winner = selectedWinner;

                _uiScheduler.Schedule(TimeSpan.FromSeconds(3), () =>
                {
                    scoreboardHandle.Close();
                    judgeHandle?.Close();
                });
            };

            _windowService.ShowDialog(drawResolver, new WindowOptions
            {
                Title = "Rozstrzyganie Remisu - Panel Sedziego",
                Width = 800,
                Height = 600,
                StartupPosition = WindowStartupPosition.CenterScreen,
                Resizable = false,
                Topmost = true
            }, handle => judgeHandle = handle);

            if (winner is null && _requireEveryParticipant)
                return null;
            return winner == first.Entrant ? first : second;
        }

        public ObservableCollection<ParticipantResult> GetFinalRankings() => Rank(Results, _category.FinalResults);

        public ObservableCollection<TeamKataResult> GetTeamRankings() => Rank(TeamResults, _teamCategory!.TeamResults);

        private ObservableCollection<TResult> Rank<TResult>(IEnumerable<TResult> recent, List<TResult> saved)
            where TResult : KataPerformanceResult
        {
            if (saved.Count > 0)
                return new ObservableCollection<TResult>(saved);

            var results = new ObservableCollection<TResult>(recent.OrderByDescending(r => r.Score));
            
            System.Diagnostics.Debug.WriteLine($"GetFinalRankings called. Total results: {results.Count}");
            
            if (results.Count >= 2)
            {
                ResolveRankingTie(results, 0);
            }

            if (results.Count >= 3)
            {
                ResolveRankingTie(results, 1);
            }

            if (_category.IsFinished)
            {
                saved.AddRange(results);
            }
            
            return results;
        }

        private void ResolveRankingTie<TResult>(ObservableCollection<TResult> results, int firstIndex) where TResult : KataPerformanceResult
        {
            var first = results[firstIndex];
            var second = results[firstIndex + 1];
            if (first.Score != second.Score)
            {
                return;
            }

            System.Diagnostics.Debug.WriteLine(
                $"Draw detected between {first.Name} and {second.Name}");

            var firstScores = first.JudgeScores.OrderBy(score => score).ToList();
            var secondScores = second.JudgeScores.OrderBy(score => score).ToList();
            if (firstScores.Count < 3 || secondScores.Count < 3)
            {
                return;
            }

            var highestScoreComparison = firstScores[firstScores.Count - 1]
                .CompareTo(secondScores[secondScores.Count - 1]);
            if (highestScoreComparison < 0)
            {
                results.Move(firstIndex + 1, firstIndex);
                return;
            }

            if (highestScoreComparison > 0)
            {
                return;
            }

            var lowestScoreComparison = firstScores[0].CompareTo(secondScores[0]);
            if (lowestScoreComparison < 0)
            {
                results.Move(firstIndex + 1, firstIndex);
                return;
            }

            if (lowestScoreComparison == 0)
            {
                System.Diagnostics.Debug.WriteLine("Still tied - opening DrawResolver window");
                var winner = ResolveDraw(first, second);
                if (winner is null)
                    throw new OperationCanceledException("Potwierdź rozstrzygnięcie remisu przed wysłaniem wyników.");
                if (winner == second)
                {
                    results.Move(firstIndex + 1, firstIndex);
                }
            }
        }

    }

}
