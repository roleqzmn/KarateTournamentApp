namespace KarateTournamentApp.Models
{
    /// <summary>
    /// Stores the result for a participant in an individual competition
    /// </summary>
    public class ParticipantResult : KataPerformanceResult
    {
        public Participant Participant { get; set; } = null!;

        protected override IKataEntrant GetEntrant() => Participant;
    }
}
