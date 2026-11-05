namespace KarateTournament.Api.Contracts;

public sealed record CompleteKumiteCategoryRequest(
    Guid SessionToken, KumiteMatchResultRequest[]? Matches, int[]? RankedParticipantIds);

public sealed record KumiteMatchResultRequest(
    int BracketPosition,
    int? AkaParticipantId,
    int? ShiroParticipantId,
    int? WinnerParticipantId,
    short AkaScore = 0,
    short ShiroScore = 0,
    bool IsDisqualification = false,
    double TimeRemaining = 0,
    int AtenaiAka = 0,
    int AtenaiShiro = 0,
    int ChukokuAka = 0,
    int ChukokuShiro = 0,
    bool SenshuEnabled = true,
    bool HasSenshuAka = false,
    bool HasSenshuShiro = false,
    bool IsInOvertime = false,
    int OvertimeCount = 0);
