using KarateClassLibrary;
using KarateClassLibrary.Contracts;

namespace KarateTournamentApp.ViewModels;

public sealed class ParticipantResultHistoryViewModel(IEnumerable<ParticipantResultHistoryEntryDto> results)
{
    public IReadOnlyList<Row> Rows { get; } = results.Select(result => new Row(
        result.CategoryName, result.CategoryType == CategoryType.Team ? "Team Kata" : result.CategoryType.ToString(),
        result.Rank, result.JudgedScore, result.TeamName)).ToArray();

    public bool HasResults => Rows.Count > 0;

    public sealed record Row(string CategoryName, string DisciplineName, int? Rank, decimal? JudgedScore, string? TeamName);
}
