using System.Collections.ObjectModel;
using System.Collections.ObjectModel;
using static KarateClassLibrary.Enums;

namespace KarateTournament.Api.Entities
{
    public class Category
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public ObservableCollection<Participant> Participants { get; set; }
        public List<Belts> AllowedBelts { get; set; }
        public List<Match> BracketMatches { get; set; }
        public int? MinAge { get; set; }
        public int? MaxAge { get; set; }
        public bool IsFinished { get; set; } = false;
        public Sex Sex { get; set; }
        public CategoryType CategoryType { get; set; }
        public List<(List<decimal> Scores, int ParticipantId)> JudgingScores { get; set; }
        public List<ParticipantResult> FinalResults { get; set; }
    }
}
