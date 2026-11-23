using System.Data;
using KarateClassLibrary.Contracts;
using KarateTournament.Api.Access;
using KarateTournament.Api.Data;
using KarateTournament.Api.Entities;
using Microsoft.EntityFrameworkCore;

namespace KarateTournament.Api.Endpoints;

internal static class ScheduleEndpoints
{
    public static void MapScheduleEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var schedule = endpoints.MapGroup("/api/schedule");
        schedule.MapGet("", GetSchedule);
        schedule.MapPut("", SaveSchedule).WithMetadata(TournamentRouteAccess.Organizer);
        schedule.MapPost("/mats/{matNumber:int}/claim", ClaimMat);
        schedule.MapPost("/mats/{matNumber:int}/release", ReleaseMat);
        schedule.MapPost("/mats/{matNumber:int}/recover", RecoverMat).WithMetadata(TournamentRouteAccess.Organizer);
    }

    private static async Task<IResult> GetSchedule(TournamentDbContext db, CancellationToken token)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, token);
        var version = await db.ScheduleStates.Select(state => state.Version).SingleAsync(token);
        var mats = await db.ScheduleMats.AsNoTracking().OrderBy(mat => mat.Number)
            .Include(mat => mat.Slots).ThenInclude(slot => slot.Category).ToArrayAsync(token);
        await transaction.CommitAsync(token);

        return Results.Ok(new TournamentScheduleDto(version, mats.Select(mat => new ScheduleMatDto(
            mat.Number, mat.Revision, mat.ClaimedBy, mat.Slots.OrderBy(slot => slot.Position)
                .Select(slot => new ScheduleSlotDto(slot.CategoryId, slot.Category.Name, slot.Category.CategoryType,
                    slot.Category.IsFinished, slot.Category.ClaimedBy, slot.StartsAt, slot.EndsAt)).ToArray())).ToArray()));
    }

    private static async Task<IResult> SaveSchedule(
        SaveTournamentScheduleRequest request, TournamentDbContext db, CancellationToken token)
    {
        if (request.MatCount is < 0 or > 100 || request.Slots is null)
            return Problem(400, "Provide a mat count from 0 to 100 and a list of schedule entries.");
        if (request.Slots.Any(slot => slot is null || slot.CategoryId <= 0 || slot.MatNumber < 1
            || slot.MatNumber > request.MatCount || !HasValidTimes(slot)))
            return Problem(400, "Provide an existing mat number and local times with the end after the start.");

        var entries = request.Slots.OfType<ScheduleSlotRequest>().GroupBy(slot => slot.MatNumber)
            .SelectMany(mat => mat.Select((slot, position) => new ScheduleSlot
            {
                CategoryId = slot.CategoryId, MatNumber = mat.Key, Position = position,
                StartsAt = slot.StartsAt, EndsAt = slot.EndsAt
            })).ToArray();
        var ids = entries.Select(slot => slot.CategoryId).ToArray();
        if (ids.Distinct().Count() != ids.Length)
            return Problem(400, "A competition can be scheduled only once.");

        await using var transaction = await db.Database.BeginTransactionAsync(token);
        var state = await db.LockScheduleAsync(token);
        if (state.Version != request.Version)
            return Problem(409, "The schedule changed. Reload it before saving.");

        var mats = await db.ScheduleMats.OrderBy(mat => mat.Number).ToArrayAsync(token);
        if (mats.Any(mat => mat.Number > request.MatCount && mat.ClaimToken.HasValue))
            return Problem(409, "Release the mat before removing it from the schedule.");
        var previous = await db.ScheduleSlots.AsNoTracking().ToDictionaryAsync(slot => slot.CategoryId, token);
        var includedIds = previous.Keys.Union(ids).ToArray();
        var categories = await db.Categories.Where(category => includedIds.Contains(category.Id))
            .Select(category => new { category.Id, IsClaimed = category.ClaimToken.HasValue }).ToArrayAsync(token);
        if (ids.Except(categories.Select(category => category.Id)).Any())
            return Problem(400, "The schedule contains an unknown competition ID.");

        var proposed = entries.ToDictionary(slot => slot.CategoryId);
        if (categories.Where(category => category.IsClaimed).Any(category =>
            !previous.TryGetValue(category.Id, out var oldSlot) || !proposed.TryGetValue(category.Id, out var newSlot)
            || oldSlot.MatNumber != newSlot.MatNumber || oldSlot.Position != newSlot.Position))
            return Problem(409, "Release the competition before adding, removing or moving it in the schedule.");

        await db.ScheduleSlots.ExecuteDeleteAsync(token);
        db.ScheduleMats.RemoveRange(mats.Where(mat => mat.Number > request.MatCount));
        for (var number = mats.Length + 1; number <= request.MatCount; number++)
            db.ScheduleMats.Add(new ScheduleMat { Number = number, Revision = Guid.NewGuid() });
        db.ScheduleSlots.AddRange(entries);
        state.Version++;
        await db.SaveChangesAsync(token);
        await transaction.CommitAsync(token);
        return Results.NoContent();
    }

    private static bool HasValidTimes(ScheduleSlotRequest slot) =>
        slot.StartsAt?.Kind is null or DateTimeKind.Unspecified
        && slot.EndsAt?.Kind is null or DateTimeKind.Unspecified
        && (slot.StartsAt is null || slot.EndsAt is null || slot.StartsAt < slot.EndsAt);

    private static async Task<IResult> ClaimMat(int matNumber, ClaimMatRequest request,
        TournamentDbContext db, CancellationToken token)
    {
        var station = request.StationName?.Trim();
        if (request.SessionToken == Guid.Empty || string.IsNullOrEmpty(station) || station.Length > 100)
            return Problem(400, "Provide a session token and a station name of 1–100 characters.");

        await using var transaction = await db.Database.BeginTransactionAsync(token);
        await db.LockScheduleAsync(token);
        var mat = await db.ScheduleMats.FindAsync([matNumber], token);
        if (mat is null)
            return Results.NotFound();
        if (mat.ClaimToken == request.SessionToken && mat.ClaimedBy == station)
            return Results.NoContent();
        if (mat.ClaimToken.HasValue || mat.Revision != request.ExpectedRevision
            || await db.ScheduleMats.AnyAsync(other => other.ClaimToken == request.SessionToken, token))
            return Problem(409, "The mat belongs to another session or its claim changed. Refresh the schedule.");

        mat.ClaimToken = request.SessionToken;
        mat.ClaimedBy = station;
        mat.Revision = Guid.NewGuid();
        await db.SaveChangesAsync(token);
        await transaction.CommitAsync(token);
        return Results.NoContent();
    }

    private static Task<IResult> ReleaseMat(int matNumber, ReleaseMatRequest request,
        TournamentDbContext db, CancellationToken token) =>
        ClearMatClaim(matNumber, mat => mat.ClaimToken == request.SessionToken, db, token);

    private static Task<IResult> RecoverMat(int matNumber, RecoverMatRequest request,
        TournamentDbContext db, CancellationToken token) =>
        ClearMatClaim(matNumber, mat => mat.Revision == request.ExpectedRevision, db, token);

    private static async Task<IResult> ClearMatClaim(int matNumber, Func<ScheduleMat, bool> matches,
        TournamentDbContext db, CancellationToken token)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(token);
        await db.LockScheduleAsync(token);
        var mat = await db.ScheduleMats.FindAsync([matNumber], token);
        if (mat is null)
            return Results.NotFound();
        if (!mat.ClaimToken.HasValue)
            return Results.NoContent();
        if (!matches(mat))
            return Problem(409, "The expected mat claim is no longer active. Refresh the schedule.");
        if (await db.ScheduleSlots.AnyAsync(slot => slot.MatNumber == matNumber && slot.Category.ClaimToken.HasValue, token))
            return Problem(409, "Finish or hand off the active competition before releasing the mat.");

        mat.ClaimToken = null;
        mat.ClaimedBy = null;
        mat.Revision = Guid.NewGuid();
        await db.SaveChangesAsync(token);
        await transaction.CommitAsync(token);
        return Results.NoContent();
    }

    private static IResult Problem(int status, string message) => Results.Problem(statusCode: status, title: message);
}
