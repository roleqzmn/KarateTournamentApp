using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using KarateTournamentApp.Models;
using KarateTournamentApp.Commands;

namespace KarateTournamentApp.ViewModels
{
    /// <summary>
    /// Manages individual competitions where participants perform one at a time (Kata, Kumite judging, etc.)
    /// </summary>
    public class IndividualCompetitionManagerViewModel : ViewModelBase
    {
        private readonly Category _category;
        private int _currentParticipantIndex;
        
        public Category Category => _category;
        
        private Participant _currentParticipant;
        public Participant CurrentParticipant
        {
            get => _currentParticipant;
            set
            {
                _currentParticipant = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(CurrentParticipantNumber));
                OnPropertyChanged(nameof(TotalParticipants));
                
                // Reset judge scores for new participant
                JudgeScores.Clear();
                IsParticipantFinished = false;
                OnPropertyChanged(nameof(FinalScore));
            }
        }

        public int CurrentParticipantNumber => _currentParticipantIndex + 1;
        public int TotalParticipants => _category.Participants.Count;

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

                var discardedIndexes = GetDiscardedScoreIndexes(JudgeScores.ToList());
                var discarded = new HashSet<int>(discardedIndexes);
                return JudgeScores
                    .Where((score, index) => !discarded.Contains(index))
                    .Sum();
            }
        }

        // Store all participant results
        public ObservableCollection<ParticipantResult> Results { get; set; }

        public ICommand NextParticipantCommand { get; }
        public ICommand FinishParticipantCommand { get; }
        public ICommand AddJudgeScoreCommand { get; }
        public ICommand RemoveLastJudgeScoreCommand { get; }

        public IndividualCompetitionManagerViewModel(Category category)
        {
            _category = category;
            _currentParticipantIndex = 0;
            
            JudgeScores = new ObservableCollection<decimal>();
            Results = new ObservableCollection<ParticipantResult>();
            
            if (_category.Participants.Any())
            {
                CurrentParticipant = _category.Participants[_currentParticipantIndex];
            }

            NextParticipantCommand = new RelayCommand(o => MoveToNextParticipant(), o => CanMoveToNext());
            FinishParticipantCommand = new RelayCommand(o => FinishCurrentParticipant(), o => CanFinishParticipant());
            AddJudgeScoreCommand = new RelayCommand(AddJudgeScore);
            RemoveLastJudgeScoreCommand = new RelayCommand(o => RemoveLastScore(), o => JudgeScores.Any());
        }

        private bool CanMoveToNext()
        {
            return _currentParticipantIndex < _category.Participants.Count - 1;
        }

        private bool CanFinishParticipant()
        {
            return CurrentParticipant != null && JudgeScores.Count >= 3;
        }

        private void MoveToNextParticipant()
        {
            if (_currentParticipantIndex < _category.Participants.Count - 1)
            {
                _currentParticipantIndex++;
                CurrentParticipant = _category.Participants[_currentParticipantIndex];
            }
            else
            {
                // Also save raw judge scores for backward compatibility
                foreach (var result in Results)
                {
                    _category.JudgingScores.Add((result.JudgeScores, result.Participant.Id));
                }
                
                _category.IsFinished = true;
                CurrentParticipant = null;
            }
        }

        private void FinishCurrentParticipant()
        {
            if (CurrentParticipant != null && JudgeScores.Count >= 3)
            {
                IsParticipantFinished = true;
                
                var result = new ParticipantResult
                {
                    Participant = CurrentParticipant,
                    Score = FinalScore, 
                    JudgeScores = new List<decimal>(JudgeScores),
                    DiscardedJudgeScoreIndexes = GetDiscardedScoreIndexes(JudgeScores.ToList())
                };
                
                Results.Add(result);
                

                MoveToNextParticipant();
            }
        }

        private void AddJudgeScore(object parameter)
        {
            if (parameter is string scoreText && decimal.TryParse(scoreText, out decimal score))
            {
                if (score >= 0 && score <= 10) 
                {
                    JudgeScores.Add(score);
                    OnPropertyChanged(nameof(FinalScore));
                }
            }
        }

        private void RemoveLastScore()
        {
            if (JudgeScores.Any())
            {
                JudgeScores.RemoveAt(JudgeScores.Count - 1);
                OnPropertyChanged(nameof(FinalScore));
            }
        }

        private ParticipantResult ResolveDraw(ParticipantResult participant1, ParticipantResult participant2)
        {
            var scoreboardViewModel = new DrawResolverScoreboardViewModel(participant1.Participant, participant2.Participant);
            
            var drawResolver = new DrawResolverViewModel(participant1.Participant, participant2.Participant, scoreboardViewModel);
            
            var scoreboardWindow = new System.Windows.Window
            {
                Title = "DOGRYWKA - PUBLICZNA TABLICA",
                Width = 1920,
                Height = 1080,
                WindowState = System.Windows.WindowState.Maximized,
                WindowStyle = System.Windows.WindowStyle.None,
                Content = new Views.DrawResolverScoreboardView
                {
                    DataContext = scoreboardViewModel
                }
            };
            
            var judgeWindow = new System.Windows.Window
            {
                Title = "Rozstrzyganie Remisu - Panel Sedziego",
                Width = 800,
                Height = 600,
                WindowStartupLocation = System.Windows.WindowStartupLocation.CenterScreen,
                ResizeMode = System.Windows.ResizeMode.NoResize,
                Topmost = true,
                Content = new Views.DrawResolverView
                {
                    DataContext = drawResolver
                }
            };

            Participant winner = null;
            drawResolver.WinnerConfirmed += (sender, selectedWinner) =>
            {
                winner = selectedWinner;
                
                System.Threading.Tasks.Task.Delay(3000).ContinueWith(_ =>
                {
                    System.Windows.Application.Current.Dispatcher.Invoke(() =>
                    {
                        scoreboardWindow.Close();
                        judgeWindow.Close();
                    });
                });
            };

            scoreboardWindow.Show();
            
            judgeWindow.ShowDialog();

            return winner == participant1.Participant ? participant1 : participant2;
        }

        public ObservableCollection<ParticipantResult> GetFinalRankings()
        {
            // If final rankings are already resolved and saved, use them directly.
            if (_category.FinalResults.Any())
            {
                return new ObservableCollection<ParticipantResult>(_category.FinalResults);
            }

            var results = new ObservableCollection<ParticipantResult>(Results.OrderByDescending(r => r.Score));
            
            System.Diagnostics.Debug.WriteLine($"GetFinalRankings called. Total results: {results.Count}");
            
            if (results.Count < 2) return results;

            if (results[0].Score == results[1].Score)
            {
                System.Diagnostics.Debug.WriteLine($"Draw detected between 1st ({results[0].Participant.FullName}) and 2nd ({results[1].Participant.FullName})");
                
                var sortedScores1 = new List<decimal>(results[0].JudgeScores).OrderBy(s => s).ToList();
                var sortedScores2 = new List<decimal>(results[1].JudgeScores).OrderBy(s => s).ToList();
                
                if (sortedScores1.Count >= 3 && sortedScores2.Count >= 3)
                {
                    System.Diagnostics.Debug.WriteLine($"Scores count OK. Comparing highest and lowest scores.");
                    System.Diagnostics.Debug.WriteLine($"Player 1 scores: {string.Join(", ", sortedScores1)}");
                    System.Diagnostics.Debug.WriteLine($"Player 2 scores: {string.Join(", ", sortedScores2)}");
                    
                    var highest1 = sortedScores1[sortedScores1.Count - 1];
                    var highest2 = sortedScores2[sortedScores2.Count - 1];
                    
                    if (highest1 < highest2)
                    {
                        System.Diagnostics.Debug.WriteLine($"Swapping based on highest score: {highest1} < {highest2}");
                        SwapResults(results, 0, 1);
                    }
    
                    else if (highest1 == highest2)
                    {
                        var lowest1 = sortedScores1[0];
                        var lowest2 = sortedScores2[0];
                        
                        if (lowest1 < lowest2)
                        {
                            System.Diagnostics.Debug.WriteLine($"Swapping based on lowest score: {lowest1} < {lowest2}");
                            SwapResults(results, 0, 1);
                        }
                        // If still tied, resolve with 1v1 match
                        else if (lowest1 == lowest2)
                        {
                            System.Diagnostics.Debug.WriteLine("Still tied - opening DrawResolver window");
                            var winner = ResolveDraw(results[0], results[1]);
                            if (winner == results[1])
                            {
                                SwapResults(results, 0, 1);
                            }
                        }
                    }
                }
            }

            if (results.Count < 3) return results;

            // Resolve draw between 2nd and 3rd place
            if (results[1].Score == results[2].Score)
            {
                System.Diagnostics.Debug.WriteLine($"Draw detected between 2nd ({results[1].Participant.FullName}) and 3rd ({results[2].Participant.FullName})");
                
                var sortedScores1 = new List<decimal>(results[1].JudgeScores).OrderBy(s => s).ToList();
                var sortedScores2 = new List<decimal>(results[2].JudgeScores).OrderBy(s => s).ToList();
                
                if (sortedScores1.Count >= 3 && sortedScores2.Count >= 3)
                {
                    var highest1 = sortedScores1[sortedScores1.Count - 1];
                    var highest2 = sortedScores2[sortedScores2.Count - 1];
                    
                    if (highest1 < highest2)
                    {
                        SwapResults(results, 1, 2);
                    }
                    else if (highest1 == highest2)
                    {
                        var lowest1 = sortedScores1[0];
                        var lowest2 = sortedScores2[0];
                        
                        if (lowest1 < lowest2)
                        {
                            SwapResults(results, 1, 2);
                        }
                        else if (lowest1 == lowest2)
                        {
                            System.Diagnostics.Debug.WriteLine("Still tied - opening DrawResolver window");
                            var winner = ResolveDraw(results[1], results[2]);
                            if (winner == results[2])
                            {
                                SwapResults(results, 1, 2);
                            }
                        }
                    }
                }
            }

            if (_category.IsFinished)
            {
                _category.FinalResults = results.ToList();
            }
            
            return results;
        }

        private static List<int> GetDiscardedScoreIndexes(IReadOnlyList<decimal> scores)
        {
            // For exactly 3 scores none are discarded; above 3 discard one lowest and one highest.
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

        private void SwapResults(ObservableCollection<ParticipantResult> results, int index1, int index2)
        {
            var temp = results[index1];
            results[index1] = results[index2];
            results[index2] = temp;
        }
    }

}
