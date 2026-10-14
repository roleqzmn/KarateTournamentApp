using static KarateClassLibrary.Enums;

namespace KarateTournament.Api.Contracts;

public sealed record TournamentSnapshotDto(
    IReadOnlyList<ParticipantDto> Participants,
    IReadOnlyList<CategoryDto> Categories);

public sealed record ParticipantDto(
    int Id,
    string FirstName,
    string LastName,
    int Age,
    string? Club,
    Belts Belt,
    Sex Sex,
    IReadOnlyList<CategoryType> CategoryTypes);

public sealed record CategoryDto(
    int Id,
    string Name,
    int? MinAge,
    int? MaxAge,
    bool IsFinished,
    Sex Sex,
    CategoryType CategoryType,
    IReadOnlyList<Belts> AllowedBelts,
    IReadOnlyList<int> ParticipantIds,
    IReadOnlyList<MatchDto> BracketMatches,
    IReadOnlyList<TeamDto> Teams,
    IReadOnlyList<ParticipantResultDto> FinalResults);

public sealed record MatchDto(
    int Id,
    int BracketPosition,
    int? AkaParticipantId,
    int? ShiroParticipantId,
    int? WinnerParticipantId,
    short AkaScore,
    short ShiroScore,
    bool IsFinished,
    bool IsDisqualification,
    double TimeRemaining,
    int AtenaiAka,
    int AtenaiShiro,
    int ChukokuAka,
    int ChukokuShiro,
    bool IsRunning,
    bool SenshuEnabled,
    bool HasSenshuAka,
    bool HasSenshuShiro,
    bool IsInOvertime,
    int OvertimeCount);

public sealed record TeamDto(
    int Id,
    string Name,
    Sex Sex,
    IReadOnlyList<int> MemberParticipantIds);

public sealed record ParticipantResultDto(
    int Id,
    int ParticipantId,
    int? Rank,
    decimal Score,
    IReadOnlyList<JudgeScoreDto> JudgeScores);

public sealed record JudgeScoreDto(
    int Ordinal,
    decimal Score,
    bool IsDiscarded);
