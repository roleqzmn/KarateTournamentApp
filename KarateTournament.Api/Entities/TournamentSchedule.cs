namespace KarateTournament.Api.Entities;

internal sealed class ScheduleState
{
    public int Id { get; set; }
    public long Version { get; set; }
}

internal sealed class ScheduleMat
{
    public int Number { get; set; }
    public Guid Revision { get; set; }
    public Guid? ClaimToken { get; set; }
    public string? ClaimedBy { get; set; }
    public List<ScheduleSlot> Slots { get; set; } = [];
}

internal sealed class ScheduleSlot
{
    public int CategoryId { get; set; }
    public Category Category { get; set; } = null!;
    public int MatNumber { get; set; }
    public ScheduleMat Mat { get; set; } = null!;
    public int Position { get; set; }
    public DateTime? StartsAt { get; set; }
    public DateTime? EndsAt { get; set; }
}
