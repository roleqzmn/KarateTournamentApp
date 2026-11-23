namespace KarateClassLibrary.Contracts;

public sealed record TournamentScheduleDto(long Version, ScheduleMatDto[] Mats);

public sealed record ScheduleMatDto(int Number, Guid Revision, string? ClaimedBy, ScheduleSlotDto[] Slots);

public sealed record ScheduleSlotDto(
    int CategoryId, string CategoryName, CategoryType CategoryType, bool IsFinished, string? ClaimedBy,
    DateTime? StartsAt, DateTime? EndsAt);

public sealed record SaveTournamentScheduleRequest(long Version, int MatCount, ScheduleSlotRequest?[]? Slots);

// Times are local to the venue. The order of slots for a given mat is the running order.
public sealed record ScheduleSlotRequest(int CategoryId, int MatNumber, DateTime? StartsAt, DateTime? EndsAt);

public sealed record ClaimMatRequest(Guid SessionToken, string? StationName, Guid ExpectedRevision);
public sealed record ReleaseMatRequest(Guid SessionToken);
public sealed record RecoverMatRequest(Guid ExpectedRevision);
