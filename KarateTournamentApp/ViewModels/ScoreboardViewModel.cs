using System;
using System.Timers;
using KarateTournamentApp.Models;

namespace KarateTournamentApp.ViewModels
{
    /// <summary>
    /// ViewModel for public scoreboard display
    /// </summary>
    public class ScoreboardViewModel : ViewModelBase
    {
        private readonly CompetitionManagerViewModel _competitionManager;
        private readonly System.Timers.Timer _timer;

        public CompetitionManagerViewModel CompetitionManager => _competitionManager;

        public string CategoryName => _competitionManager.Category.Name;

        public string AkaName => _competitionManager.AkaParticipant?.FullName ?? "---";
        public string ShiroName => _competitionManager.ShiroParticipant?.FullName ?? "---";

        public int AkaScore => _competitionManager.CurrentMatch?.AkaScore ?? 0;
        public int ShiroScore => _competitionManager.CurrentMatch?.ShiroScore ?? 0;

        public string TimeDisplay
        {
            get
            {
                if (_competitionManager.CurrentMatch is ShobuSanbonMatch shobuMatch)
                {
                    var minutes = (int)(shobuMatch.TimeRemaining / 60);
                    var seconds = (int)(shobuMatch.TimeRemaining % 60);
                    return $"{minutes:D2}:{seconds:D2}";
                }
                return "";
            }
        }

        public int AkaAtenai => _competitionManager.CurrentMatch is ShobuSanbonMatch shobuMatch 
            ? shobuMatch.AtenaiAka : 0;

        public int AkaChukoku => _competitionManager.CurrentMatch is ShobuSanbonMatch shobuMatch
            ? shobuMatch.ChukokuAka : 0;

        public int ShiroAtenai => _competitionManager.CurrentMatch is ShobuSanbonMatch shobuMatch 
            ? shobuMatch.AtenaiShiro : 0;

        public int ShiroChukoku => _competitionManager.CurrentMatch is ShobuSanbonMatch shobuMatch
            ? shobuMatch.ChukokuShiro : 0;

        public bool AkaHansoku => AkaAtenai >= 3 || AkaChukoku >= 4;
        public bool ShiroHansoku => ShiroAtenai >= 3 || ShiroChukoku >= 4;

        public bool IsShobuSanbon => _competitionManager.Category.CategoryType == CategoryType.Kumite;

        public ScoreboardViewModel(CompetitionManagerViewModel competitionManager)
        {
            _competitionManager = competitionManager;

            // Setup timer for Kumite / Shobu Sanbon mode
            if (IsShobuSanbon)
            {
                _timer = new System.Timers.Timer(100); // Update every 100ms
                _timer.Elapsed += OnTimerElapsed;
            }

            // Subscribe to changes in competition manager
            _competitionManager.PropertyChanged += (s, e) => RefreshAll();
        }

        private void OnTimerElapsed(object sender, ElapsedEventArgs e)
        {
            if (_competitionManager.CurrentMatch is ShobuSanbonMatch shobuMatch && shobuMatch.IsRunning)
            {
                shobuMatch.TimeRemaining = Math.Max(0, shobuMatch.TimeRemaining - 0.1);
                OnPropertyChanged(nameof(TimeDisplay));

                if (shobuMatch.TimeRemaining <= 0)
                {
                    StopTimer();
                }
            }
        }

        public void StartTimer()
        {
            if (_competitionManager.CurrentMatch is ShobuSanbonMatch shobuMatch)
            {
                shobuMatch.IsRunning = true;
                _timer?.Start();
            }
        }

        public void StopTimer()
        {
            if (_competitionManager.CurrentMatch is ShobuSanbonMatch shobuMatch)
            {
                shobuMatch.IsRunning = false;
                _timer?.Stop();
            }
        }

        private void RefreshAll()
        {
            OnPropertyChanged(nameof(AkaName));
            OnPropertyChanged(nameof(ShiroName));
            OnPropertyChanged(nameof(AkaScore));
            OnPropertyChanged(nameof(ShiroScore));
            OnPropertyChanged(nameof(AkaAtenai));
            OnPropertyChanged(nameof(AkaChukoku));
            OnPropertyChanged(nameof(ShiroAtenai));
            OnPropertyChanged(nameof(ShiroChukoku));
            OnPropertyChanged(nameof(AkaHansoku));
            OnPropertyChanged(nameof(ShiroHansoku));
            OnPropertyChanged(nameof(TimeDisplay));
            OnPropertyChanged(nameof(IsShobuSanbon));
        }
    }
}
