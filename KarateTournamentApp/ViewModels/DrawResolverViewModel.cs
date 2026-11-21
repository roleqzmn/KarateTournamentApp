using System;
using System.Windows.Input;
using KarateTournamentApp.Models;
using KarateTournamentApp.Commands;

namespace KarateTournamentApp.ViewModels
{
    /// <summary>
    /// ViewModel for resolving draw between two participants through 1v1 match
    /// </summary>
    public class DrawResolverViewModel : ViewModelBase
    {
        private readonly IKataEntrant _akaParticipant;
        private readonly IKataEntrant _shiroParticipant;
        private readonly DrawResolverScoreboardViewModel? _scoreboardViewModel;
        
        public string AkaName => _akaParticipant.JudgingName;
        public string ShiroName => _shiroParticipant.JudgingName;

        private IKataEntrant? _winner;
        public IKataEntrant? Winner
        {
            get => _winner;
            private set
            {
                _winner = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsWinnerSelected));
                OnPropertyChanged(nameof(WinnerName));
                OnPropertyChanged(nameof(WinnerColor));
            }
        }

        public bool IsWinnerSelected => Winner != null;
        
        public string WinnerName => Winner != null ? $"{(Winner == _akaParticipant ? "AKA" : "SHIRO")} WYGRYWA!" : "";
        
        public string WinnerColor => Winner == _akaParticipant ? "Red" : Winner == _shiroParticipant ? "White" : "Transparent";

        public ICommand SelectAkaCommand { get; }
        public ICommand SelectShiroCommand { get; }
        public ICommand ConfirmCommand { get; }

        public event EventHandler<IKataEntrant>? WinnerConfirmed;

        public DrawResolverViewModel(IKataEntrant akaParticipant, IKataEntrant shiroParticipant,
            DrawResolverScoreboardViewModel? scoreboardViewModel = null)
        {
            _akaParticipant = akaParticipant ?? throw new ArgumentNullException(nameof(akaParticipant));
            _shiroParticipant = shiroParticipant ?? throw new ArgumentNullException(nameof(shiroParticipant));
            _scoreboardViewModel = scoreboardViewModel;

            SelectAkaCommand = new RelayCommand(o => SelectWinner(_akaParticipant), o => Winner == null);
            SelectShiroCommand = new RelayCommand(o => SelectWinner(_shiroParticipant), o => Winner == null);
            ConfirmCommand = new RelayCommand(o => ConfirmWinner(), o => IsWinnerSelected);
        }

        private void SelectWinner(IKataEntrant participant)
        {
            Winner = participant;
            
            _scoreboardViewModel?.AnnounceWinner(participant);
        }

        private void ConfirmWinner()
        {
            if (Winner != null)
            {
                WinnerConfirmed?.Invoke(this, Winner);
            }
        }
    }
}

