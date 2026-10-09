namespace KarateTournament.Api.Entities;

public class Match
{
    public int Id { get; set; }
    public int CategoryId { get; set; }
    public Category Category { get; set; } = null!;
    public int BracketPosition { get; set; }

    public int? AkaParticipantId { get; set; }
    public Participant? AkaParticipant { get; set; }
    public int? ShiroParticipantId { get; set; }
    public Participant? ShiroParticipant { get; set; }
    public int? WinnerParticipantId { get; set; }
    public Participant? WinnerParticipant { get; set; }

    public short AkaScore { get; set; }
    public short ShiroScore { get; set; }
    public bool IsFinished { get; set; }
    public bool IsDisqualification { get; set; }

    public double TimeRemaining { get; set; } = 180;
    public int AtenaiAka { get; set; }
    public int AtenaiShiro { get; set; }
    public int ChukokuAka { get; set; }
    public int ChukokuShiro { get; set; }
    public bool IsRunning { get; set; }
    public bool SenshuEnabled { get; set; } = true;
    public bool HasSenshuAka { get; set; }
    public bool HasSenshuShiro { get; set; }
    public bool IsInOvertime { get; set; }
    public int OvertimeCount { get; set; }
}
