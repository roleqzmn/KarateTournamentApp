using KarateClassLibrary;

namespace KarateTournament.Api.Contracts;

public sealed record CategorySummaryDto(
    int Id,
    string Name,
    CategoryType CategoryType,
    Sex Sex,
    int? MinAge,
    int? MaxAge,
    bool IsFinished,
    int ParticipantCount,
    int TeamCount);
