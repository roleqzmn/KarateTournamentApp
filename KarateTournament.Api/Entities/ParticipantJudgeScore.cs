namespace KarateTournament.Api.Entities;

public class ParticipantJudgeScore
{
    public int Id { get; set; }
    public int ParticipantResultId { get; set; }
    public ParticipantResult ParticipantResult { get; set; } = null!;
    public int Ordinal { get; set; }
    public decimal Score { get; set; }
    public bool IsDiscarded { get; set; }
}
