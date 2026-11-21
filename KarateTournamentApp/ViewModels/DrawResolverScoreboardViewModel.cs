using System;
using KarateTournamentApp.Models;

namespace KarateTournamentApp.ViewModels
{
    /// <summary>
    /// ViewModel for public scoreboard display during draw resolution
    /// </summary>
    public class DrawResolverScoreboardViewModel : ViewModelBase
    {
        private readonly IKataEntrant _akaParticipant;
        private readonly IKataEntrant _shiroParticipant;

        public string AkaName => _akaParticipant.JudgingName;
        public string ShiroName => _shiroParticipant.JudgingName;

        private IKataEntrant? _winner;
        public IKataEntrant? Winner
        {
            get => _winner;
            set
            {
                _winner = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsWinnerAnnounced));
                OnPropertyChanged(nameof(WinnerText));
                OnPropertyChanged(nameof(WinnerColor));
            }
        }

        public bool IsWinnerAnnounced => Winner != null;

        public string WinnerText
        {
            get
            {
                if (Winner == null) return "";
                return Winner == _akaParticipant ? "AKA WYGRYWA!" : "SHIRO WYGRYWA!";
            }
        }

        public string WinnerColor => Winner == _akaParticipant ? "#E74C3C" : Winner == _shiroParticipant ? "White" : "Transparent";

        public DrawResolverScoreboardViewModel(IKataEntrant akaParticipant, IKataEntrant shiroParticipant)
        {
            _akaParticipant = akaParticipant ?? throw new ArgumentNullException(nameof(akaParticipant));
            _shiroParticipant = shiroParticipant ?? throw new ArgumentNullException(nameof(shiroParticipant));
        }

        public void AnnounceWinner(IKataEntrant winner)
        {
            Winner = winner;
        }
    }
}
