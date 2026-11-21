using KarateClassLibrary.Contracts;

namespace KarateTournamentApp.ViewModels;

public enum OrganizerCompetitionStatus { All, Available, InProgress, Finished }

public sealed record OrganizerCompetitionRow(CategorySummaryDto Summary)
{
    public OrganizerCompetitionStatus Status => Summary switch
    {
        { IsFinished: true } => OrganizerCompetitionStatus.Finished,
        { IsClaimed: true } => OrganizerCompetitionStatus.InProgress,
        _ => OrganizerCompetitionStatus.Available
    };

    public string StatusText => GetStatusText(Status);
    public string? StationName => Status == OrganizerCompetitionStatus.InProgress ? Summary.ClaimedBy : null;

    internal static string GetStatusText(OrganizerCompetitionStatus status) => status switch
    {
        OrganizerCompetitionStatus.Available => "Dostępna",
        OrganizerCompetitionStatus.InProgress => "W trakcie",
        OrganizerCompetitionStatus.Finished => "Zakończona",
        _ => "Wszystkie"
    };
}
