using KarateTournamentApp.Models;
using System.Collections.ObjectModel;
using System.Linq;

namespace KarateTournamentApp.ViewModels
{
    public class ResultsViewModel : ViewModelBase
    {
        private readonly IndividualCompetitionManagerViewModel _competitionManager;

        public string CategoryName => _competitionManager.Category.Name;
        public ObservableCollection<ParticipantResult> Rankings { get; set; }
        public bool HasKumiteWinner =>
            _competitionManager.Category.CategoryType == CategoryType.Kumite
            && _competitionManager.Category.BracketMatches.Any()
            && _competitionManager.Category.BracketMatches[0].WinnerId.HasValue;

        public bool HasKumiteFinalMatchSummary => HasKumiteWinner && _competitionManager.Category.BracketMatches[0].Aka.HasValue && _competitionManager.Category.BracketMatches[0].Shiro.HasValue;

        public string KumiteWinnerDisplay
        {
            get
            {
                if (!HasKumiteWinner)
                {
                    return string.Empty;
                }

                var winnerId = _competitionManager.Category.BracketMatches[0].WinnerId!.Value;
                var winner = _competitionManager.Category.Participants.FirstOrDefault(p => p.Id == winnerId);
                return winner != null ? $"Zwyciezca: {winner.FullName}" : "Zwyciezca: ---";
            }
        }

        public string KumiteFinalMatchSummary
        {
            get
            {
                if (!HasKumiteFinalMatchSummary)
                {
                    return string.Empty;
                }

                var finalMatch = _competitionManager.Category.BracketMatches[0];
                var aka = _competitionManager.Category.Participants.FirstOrDefault(p => p.Id == finalMatch.Aka!.Value);
                var shiro = _competitionManager.Category.Participants.FirstOrDefault(p => p.Id == finalMatch.Shiro!.Value);

                string akaName = aka?.FullName ?? "AKA";
                string shiroName = shiro?.FullName ?? "SHIRO";
                string resolution = BuildResolutionSuffix(finalMatch);

                return $"Final: {akaName} {finalMatch.AkaScore}:{finalMatch.ShiroScore} {shiroName}{resolution}";
            }
        }

        public ResultsViewModel(IndividualCompetitionManagerViewModel competitionManager)
        {
            _competitionManager = competitionManager;
            
            Rankings = _competitionManager.GetFinalRankings();
        }

        private static string BuildResolutionSuffix(Match finalMatch)
        {
            if (finalMatch is not ShobuSanbonMatch shobuMatch)
            {
                return string.Empty;
            }

            if (finalMatch.IsDisqualification)
            {
                return " przez HANSOKU";
            }

            if (shobuMatch.AkaScore == shobuMatch.ShiroScore && (shobuMatch.HasSenshuAka || shobuMatch.HasSenshuShiro))
            {
                return " przez SENSHU";
            }

            if (shobuMatch.TimeRemaining <= 0 && shobuMatch.AkaScore != shobuMatch.ShiroScore)
            {
                return shobuMatch.OvertimeCount > 0 ? " po czasie w dogrywce" : " po czasie";
            }

            if (shobuMatch.AkaScore >= 6 || shobuMatch.ShiroScore >= 6)
            {
                return " do 6 punktow";
            }

            if (shobuMatch.OvertimeCount > 0)
            {
                return " po dogrywce";
            }

            return string.Empty;
        }
    }
}



