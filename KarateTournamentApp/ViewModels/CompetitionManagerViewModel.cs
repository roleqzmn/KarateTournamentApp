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
    /// Manages the state of an ongoing competition/category
    /// </summary>
    public class CompetitionManagerViewModel : ViewModelBase
    {
        private const int MaxScoreToWin = 6;
        private const int AtenaiHansokuThreshold = 3;
        private const int ChukokuHansokuThreshold = 4;

        private readonly Category _category;
        private int _currentMatchIndex;
        
        public Category Category => _category;
        
        private Match _currentMatch;
        public Match CurrentMatch
        {
            get => _currentMatch;
            set
            {
                _currentMatch = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(AkaParticipant));
                OnPropertyChanged(nameof(ShiroParticipant));
                OnPropertyChanged(nameof(IsShobuSanbon));
                OnPropertyChanged(nameof(SenshuEnabled));
                OnPropertyChanged(nameof(HasSenshuAka));
                OnPropertyChanged(nameof(HasSenshuShiro));
                OnPropertyChanged(nameof(IsInOvertime));
            }
        }

        public bool IsShobuSanbon => _category.CategoryType == CategoryType.Kumite;

        // Senshu properties
        public bool SenshuEnabled
        {
            get => CurrentMatch is ShobuSanbonMatch match && match.SenshuEnabled;
            set
            {
                if (CurrentMatch is ShobuSanbonMatch match)
                {
                    match.SenshuEnabled = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool HasSenshuAka => CurrentMatch is ShobuSanbonMatch match && match.HasSenshuAka;
        public bool HasSenshuShiro => CurrentMatch is ShobuSanbonMatch match && match.HasSenshuShiro;
        public bool IsInOvertime => CurrentMatch is ShobuSanbonMatch match && match.IsInOvertime;

        public Participant? AkaParticipant => CurrentMatch?.Aka.HasValue == true 
            ? _category.Participants.FirstOrDefault(p => p.Id == CurrentMatch.Aka.Value)
            : null;

        public Participant? ShiroParticipant => CurrentMatch?.Shiro.HasValue == true
            ? _category.Participants.FirstOrDefault(p => p.Id == CurrentMatch.Shiro.Value)
            : null;

        public ICommand NextMatchCommand { get; }
        public ICommand FinishMatchCommand { get; }
        public ICommand StartOvertimeCommand { get; }
        public ICommand SetTimeCommand { get; }

        public CompetitionManagerViewModel(Category category)
        {
            _category = category;

            bool isKumiteCategory = _category.CategoryType == CategoryType.Kumite;
            bool hasLegacyMatchTypes = isKumiteCategory
                && _category.BracketMatches.Any(m => m is not ShobuSanbonMatch);
            bool hasNoPlayableMatches = isKumiteCategory
                && _category.BracketMatches.Any()
                && !_category.BracketMatches.Any(m => !m.IsFinished && m.Aka.HasValue && m.Shiro.HasValue)
                && _category.Participants.Count >= 2
                && !_category.IsFinished;

            if (!_category.BracketMatches.Any())
            {
                if (isKumiteCategory)
                {
                    InitializeKumiteBracketAsShobu();
                }
                else
                {
                    _category.InitializeBracket();
                }
            }
            else if (hasLegacyMatchTypes || hasNoPlayableMatches)
            {
                InitializeKumiteBracketAsShobu();
            }

            // Find first unfinished match
            _currentMatchIndex = FindNextUnfinishedMatch();
            if (_currentMatchIndex >= 0)
            {
                CurrentMatch = _category.BracketMatches[_currentMatchIndex];
            }
            else
            {
                CurrentMatch = null;
            }

            NextMatchCommand = new RelayCommand(o => MoveToNextMatch(), o => CanMoveToNextMatch());
            FinishMatchCommand = new RelayCommand(o => FinishCurrentMatch(), o => CurrentMatch != null && !CurrentMatch.IsFinished);
            StartOvertimeCommand = new RelayCommand(o => StartOvertime(), o => CanStartOvertime());
            SetTimeCommand = new RelayCommand(SetTime, o => CurrentMatch is ShobuSanbonMatch);
        }

        private int FindNextUnfinishedMatch()
        {
            for (int i = _category.BracketMatches.Count - 1; i >= 0; i--)
            {
                var match = _category.BracketMatches[i];
                if (!match.IsFinished && match.Aka.HasValue && match.Shiro.HasValue)
                {
                    return i;
                }
            }
            return -1;
        }

        private bool CanMoveToNextMatch()
        {
            return CurrentMatch?.IsFinished == true;
        }

        private void MoveToNextMatch()
        {
            if (CurrentMatch?.IsFinished == true)
            {
                PromoteWinnerAndSelectNextMatch();
            }
        }

        private void FinishCurrentMatch()
        {
            if (CurrentMatch != null && !CurrentMatch.IsFinished)
            {
                // Check for draw
                if (CurrentMatch.AkaScore == CurrentMatch.ShiroScore)
                {
                    if (CurrentMatch is ShobuSanbonMatch shobuMatch)
                    {
                        // Check Senshu if enabled
                        if (shobuMatch.SenshuEnabled && (shobuMatch.HasSenshuAka || shobuMatch.HasSenshuShiro))
                        {
                            // Winner by Senshu
                            var winnerId = shobuMatch.HasSenshuAka ? CurrentMatch.Aka : CurrentMatch.Shiro;
                            CompleteMatch(winnerId, false, true);
                            
                            System.Windows.MessageBox.Show(
                                $"Remis! Zwyci�zca przez SENSHU: {(shobuMatch.HasSenshuAka ? "AKA" : "SHIRO")}",
                                "Rozstrzygni�cie przez Senshu",
                                System.Windows.MessageBoxButton.OK,
                                System.Windows.MessageBoxImage.Information);
                        }
                        else
                        {
                            // Draw without Senshu - need overtime
                            var result = System.Windows.MessageBox.Show(
                                "Remis! Czy rozpocz�� dogrywk� (+60 sekund)?",
                                "Dogrywka",
                                System.Windows.MessageBoxButton.YesNo,
                                System.Windows.MessageBoxImage.Question);

                            if (result == System.Windows.MessageBoxResult.Yes)
                            {
                                StartOvertime();
                                return; // Don't finish the match yet
                            }
                            else
                            {
                                // Manual decision or cancel
                                System.Windows.MessageBox.Show(
                                    "Walka niezako�czona. U�yj DrawResolver lub r�cznie wybierz zwyci�zc�.",
                                    "Uwaga",
                                    System.Windows.MessageBoxButton.OK,
                                    System.Windows.MessageBoxImage.Warning);
                                return;
                            }
                        }
                    }
                    else
                    {
                        // Non-Shobu Sanbon draw (shouldn't happen, but handle it)
                        System.Windows.MessageBox.Show("Remis! R�cznie wybierz zwyci�zc�.", "Remis", 
                            System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                        return;
                    }
                }
                else
                {
                    // Determine winner based on score
                    if (CurrentMatch.AkaScore > CurrentMatch.ShiroScore)
                    {
                        CompleteMatch(CurrentMatch.Aka, false, true);
                    }
                    else
                    {
                        CompleteMatch(CurrentMatch.Shiro, false, true);
                    }
                }
                
                OnPropertyChanged(nameof(CurrentMatch));
            }
        }

        private bool CanStartOvertime()
        {
            return CurrentMatch is ShobuSanbonMatch match 
                   && !match.IsFinished 
                   && match.TimeRemaining <= 0
                   && match.AkaScore == match.ShiroScore;
        }

        private void StartOvertime()
        {
            if (CurrentMatch is ShobuSanbonMatch shobuMatch)
            {
                shobuMatch.IsInOvertime = true;
                shobuMatch.OvertimeCount++;
                shobuMatch.TimeRemaining = 60; // Add 60 seconds
                shobuMatch.IsRunning = false; // Stop timer, judge needs to start it manually
                
                // Reset Senshu for overtime (fresh start)
                shobuMatch.HasSenshuAka = false;
                shobuMatch.HasSenshuShiro = false;
                
                OnPropertyChanged(nameof(CurrentMatch));
                OnPropertyChanged(nameof(IsInOvertime));
                
                System.Windows.MessageBox.Show(
                    $"Dogrywka {shobuMatch.OvertimeCount} rozpocz�ta!\n+60 sekund dodane.\nSenshu zresetowane.",
                    "Dogrywka",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Information);
            }
        }

        private void SetTime(object parameter)
        {
            if (CurrentMatch is ShobuSanbonMatch shobuMatch)
            {
                if (parameter is string timeText && double.TryParse(timeText, out double seconds))
                {
                    if (seconds >= 0 && seconds <= 600) // Max 10 minutes
                    {
                        shobuMatch.TimeRemaining = seconds;
                        shobuMatch.IsRunning = false; // Stop timer when manually setting time
                        OnPropertyChanged(nameof(CurrentMatch));
                        
                        System.Diagnostics.Debug.WriteLine($"Time set to {seconds} seconds");
                    }
                }
            }
        }

        public void UpdateMatchScore(bool isAka, int points)
        {
            if (CurrentMatch != null && !CurrentMatch.IsFinished)
            {
                // Check if this is the first point scored (for Senshu)
                if (CurrentMatch is ShobuSanbonMatch shobuMatch && shobuMatch.SenshuEnabled)
                {
                    bool isFirstPoint = shobuMatch.AkaScore == 0 && shobuMatch.ShiroScore == 0;
                    
                    if (isFirstPoint && points > 0)
                    {
                        if (isAka)
                        {
                            shobuMatch.HasSenshuAka = true;
                            System.Diagnostics.Debug.WriteLine("SENSHU dla AKA - pierwszy punkt!");
                        }
                        else
                        {
                            shobuMatch.HasSenshuShiro = true;
                            System.Diagnostics.Debug.WriteLine("SENSHU dla SHIRO - pierwszy punkt!");
                        }
                        
                        OnPropertyChanged(nameof(HasSenshuAka));
                        OnPropertyChanged(nameof(HasSenshuShiro));
                    }
                }
                
                // Update score
                if (isAka)
                {
                    CurrentMatch.AkaScore += (short)points;
                    if (CurrentMatch.AkaScore < 0) CurrentMatch.AkaScore = 0;

                    if (CurrentMatch.AkaScore >= MaxScoreToWin)
                    {
                        CompleteMatch(CurrentMatch.Aka, false, true);
                    }
                }
                else
                {
                    CurrentMatch.ShiroScore += (short)points;
                    if (CurrentMatch.ShiroScore < 0) CurrentMatch.ShiroScore = 0;

                    if (CurrentMatch.ShiroScore >= MaxScoreToWin)
                    {
                        CompleteMatch(CurrentMatch.Shiro, false, true);
                    }
                }
                
                OnPropertyChanged(nameof(CurrentMatch));
            }
        }

        public void UpdatePenalty(bool isAka, PenaltyType penaltyType, int change)
        {
            if (CurrentMatch is ShobuSanbonMatch shobuMatch && !CurrentMatch.IsFinished)
            {
                if (isAka)
                {
                    if (penaltyType == PenaltyType.Atenai)
                    {
                        shobuMatch.AtenaiAka = Math.Max(0, shobuMatch.AtenaiAka + change);
                    }
                    else
                    {
                        shobuMatch.ChukokuAka = Math.Max(0, shobuMatch.ChukokuAka + change);
                    }
                }
                else
                {
                    if (penaltyType == PenaltyType.Atenai)
                    {
                        shobuMatch.AtenaiShiro = Math.Max(0, shobuMatch.AtenaiShiro + change);
                    }
                    else
                    {
                        shobuMatch.ChukokuShiro = Math.Max(0, shobuMatch.ChukokuShiro + change);
                    }
                }

                bool akaHansoku = shobuMatch.AtenaiAka >= AtenaiHansokuThreshold
                    || shobuMatch.ChukokuAka >= ChukokuHansokuThreshold;
                bool shiroHansoku = shobuMatch.AtenaiShiro >= AtenaiHansokuThreshold
                    || shobuMatch.ChukokuShiro >= ChukokuHansokuThreshold;

                if (akaHansoku)
                {
                    CompleteMatch(CurrentMatch.Shiro, true, true);
                    System.Windows.MessageBox.Show(
                        "AKA otrzymuje HANSOKU. Zwyci�a SHIRO.",
                        "Dyskwalifikacja",
                        System.Windows.MessageBoxButton.OK,
                        System.Windows.MessageBoxImage.Information);
                }
                else if (shiroHansoku)
                {
                    CompleteMatch(CurrentMatch.Aka, true, true);
                    System.Windows.MessageBox.Show(
                        "SHIRO otrzymuje HANSOKU. Zwyci�a AKA.",
                        "Dyskwalifikacja",
                        System.Windows.MessageBoxButton.OK,
                        System.Windows.MessageBoxImage.Information);
                }

                OnPropertyChanged(nameof(CurrentMatch));
            }
        }

        private void CompleteMatch(Guid? winnerId, bool disqualification, bool autoAdvance)
        {
            if (CurrentMatch == null)
            {
                return;
            }

            CurrentMatch.WinnerId = winnerId;
            CurrentMatch.IsDisqualification = disqualification;
            CurrentMatch.IsFinished = true;

            if (CurrentMatch is ShobuSanbonMatch shobuMatch)
            {
                shobuMatch.IsRunning = false;
            }

            if (autoAdvance)
            {
                PromoteWinnerAndSelectNextMatch();
            }
        }

        private void PromoteWinnerAndSelectNextMatch()
        {
            _category.PromoteWinner(_currentMatchIndex);
            _currentMatchIndex = FindNextUnfinishedMatch();

            if (_currentMatchIndex >= 0)
            {
                CurrentMatch = _category.BracketMatches[_currentMatchIndex];
            }
            else
            {
                CurrentMatch = null;
                _category.IsFinished = true;
                SaveKumiteFinalResults();
            }

            CommandManager.InvalidateRequerySuggested();
        }

        private void InitializeKumiteBracketAsShobu()
        {
            var shobuCategory = new ShobuSanbonCategory(
                _category.AllowedBelts,
                _category.CategoryType,
                _category.Sex,
                _category.MinAge,
                _category.MaxAge)
            {
                Participants = _category.Participants
            };

            shobuCategory.InitializeBracket();
            _category.BracketMatches = shobuCategory.BracketMatches;
        }

        private void SaveKumiteFinalResults()
        {
            if (_category.CategoryType != CategoryType.Kumite || !_category.BracketMatches.Any())
            {
                return;
            }

            var totalPoints = new Dictionary<Guid, int>();
            foreach (var participant in _category.Participants)
            {
                totalPoints[participant.Id] = 0;
            }

            foreach (var match in _category.BracketMatches.Where(m => m.IsFinished))
            {
                if (match.Aka.HasValue && totalPoints.ContainsKey(match.Aka.Value))
                {
                    totalPoints[match.Aka.Value] += match.AkaScore;
                }

                if (match.Shiro.HasValue && totalPoints.ContainsKey(match.Shiro.Value))
                {
                    totalPoints[match.Shiro.Value] += match.ShiroScore;
                }
            }

            var winnerId = _category.BracketMatches[0].WinnerId;

            var orderedParticipants = _category.Participants
                .OrderByDescending(p => winnerId.HasValue && p.Id == winnerId.Value)
                .ThenByDescending(p => totalPoints.TryGetValue(p.Id, out int points) ? points : 0)
                .ThenBy(p => p.FullName)
                .ToList();

            var finalResults = new List<ParticipantResult>();
            for (int i = 0; i < orderedParticipants.Count; i++)
            {
                finalResults.Add(new ParticipantResult
                {
                    Participant = orderedParticipants[i],
                    Score = orderedParticipants.Count - i,
                    JudgeScores = new List<decimal>()
                });
            }

            _category.FinalResults = finalResults;
        }
    }
}
