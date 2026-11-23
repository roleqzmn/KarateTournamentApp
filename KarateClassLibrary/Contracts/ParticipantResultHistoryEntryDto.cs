namespace KarateClassLibrary.Contracts;

public sealed record ParticipantResultHistoryEntryDto(
    int CategoryId,
    string CategoryName,
    CategoryType CategoryType,
    int? Rank,
    decimal? JudgedScore,
    int? TeamId,
    string? TeamName);
