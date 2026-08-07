namespace KarateTournament.Api.Entities
{
    public class ParticipantResult
    {
        public Participant Participant { get; set; }

        public decimal Score { get; set; }

        public List<decimal> JudgeScores { get; set; }

        public List<int> DiscardedJudgeScoreIndexes { get; set; }
    }
}
