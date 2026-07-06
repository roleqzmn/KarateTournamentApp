using System.Collections.Generic;

namespace KarateTournamentApp.Models
{
    /// <summary>
    /// Stores the result for a participant in an individual competition
    /// </summary>
    public class ParticipantResult
    {
        public Participant Participant { get; set; }
        
        public decimal Score { get; set; }
        
        public List<decimal> JudgeScores { get; set; }

        public List<int> DiscardedJudgeScoreIndexes { get; set; } = new List<int>();
    }
}