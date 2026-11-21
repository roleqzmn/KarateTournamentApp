namespace KarateTournamentApp.Models;

internal enum StationRole
{
    Judge,
    Organizer,
    Local
}

internal static class StationRoleSettings
{
    public static StationRole Parse(string? configuredRole) => configuredRole?.Trim().ToLowerInvariant() switch
    {
        "organizer" => StationRole.Organizer,
        "local" => StationRole.Local,
        _ => StationRole.Judge
    };
}
