using System.Text.Json;
using KarateClassLibrary;
using KarateClassLibrary.Contracts;
using KarateTournament.Api.Access;
using KarateTournament.Api.Data;
using KarateTournament.Api.Entities;
using Microsoft.EntityFrameworkCore;

namespace KarateTournament.Api.Endpoints;

internal static class JudgingHandoffEndpoints
{
    public static void MapJudgingHandoffEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var handoff = endpoints.MapGroup("/api/categories/{categoryId:int}/handoff");
        handoff.MapGet("", CategoryClaimEndpoints.GetRecoveryTarget).WithMetadata(TournamentRouteAccess.Organizer);
        handoff.MapPost("", RequestHandoff).WithMetadata(TournamentRouteAccess.Organizer);
        handoff.MapPost("/status", ReadStatus);
        handoff.MapPost("/checkpoint", ReadCheckpoint);
        handoff.MapPost("/save", SaveAndRelease);
    }

    private static async Task<IResult> RequestHandoff(
        int categoryId, RecoverCategoryClaimRequest request, TournamentDbContext db, CancellationToken cancellationToken)
    {
        if (!CategoryClaimEndpoints.IsHexDigest(request.ClaimReference))
            return Invalid("Provide the claim reference returned by GET handoff.");

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var category = await db.LockCategoryAsync(categoryId, cancellationToken);
        if (category is null)
            return Results.NotFound();
        if (category.IsFinished || category.ClaimToken is not Guid token
            || !string.Equals(CategoryClaimEndpoints.GetClaimReference(token), request.ClaimReference, StringComparison.OrdinalIgnoreCase))
            return Conflict();

        var progress = await GetOrCreateProgressAsync(db, categoryId, cancellationToken);
        progress.RequestedFor = token;
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Results.NoContent();
    }

    private static async Task<IResult> ReadStatus(
        int categoryId, ReleaseCategoryRequest request, TournamentDbContext db, CancellationToken cancellationToken)
    {
        if (request.SessionToken == Guid.Empty)
            return Invalid("Provide the judging session token.");

        var state = await db.Categories.Where(category => category.Id == categoryId)
            .Select(category => new JudgingHandoffState(
                category.ClaimToken == request.SessionToken && category.JudgingProgress != null
                    && category.JudgingProgress.RequestedFor == request.SessionToken,
                category.JudgingProgress != null && category.JudgingProgress.LastReleasedToken == request.SessionToken))
            .SingleOrDefaultAsync(cancellationToken);
        return state is null ? Results.NotFound() : Results.Ok(state);
    }

    private static async Task<IResult> ReadCheckpoint(
        int categoryId, ReleaseCategoryRequest request, TournamentDbContext db, CancellationToken cancellationToken)
    {
        if (request.SessionToken == Guid.Empty)
            return Invalid("Provide the judging session token.");

        var state = await db.Categories.Where(category => category.Id == categoryId)
            .Select(category => new
            {
                category.IsFinished,
                category.ClaimToken,
                Checkpoint = category.JudgingProgress == null ? null : category.JudgingProgress.CheckpointJson
            }).SingleOrDefaultAsync(cancellationToken);
        if (state is null)
            return Results.NotFound();
        if (state.IsFinished || state.ClaimToken != request.SessionToken)
            return Conflict();
        return Results.Ok(new JudgingCheckpointDto(state.Checkpoint is null
            ? null : JsonSerializer.Deserialize<JudgingCheckpoint>(state.Checkpoint)));
    }

    private static async Task<IResult> SaveAndRelease(
        int categoryId, SaveJudgingHandoffRequest request, TournamentDbContext db, CancellationToken cancellationToken)
    {
        if (request.SessionToken == Guid.Empty || request.Checkpoint is not { } checkpoint)
            return Invalid("Provide the judging session token and a checkpoint.");

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var category = await db.LockCategoryAsync(categoryId, cancellationToken);
        if (category is null)
            return Results.NotFound();
        var progress = await db.JudgingProgress.FindAsync([categoryId], cancellationToken);
        if (progress?.LastReleasedToken == request.SessionToken)
            return Results.NoContent();
        if (category.IsFinished || category.ClaimToken != request.SessionToken)
            return Conflict();

        var roster = await db.Set<CategoryParticipant>().Where(link => link.CategoryId == categoryId)
            .OrderBy(link => link.SortOrder).Select(link => link.ParticipantId).ToArrayAsync(cancellationToken);
        if (!ValidCheckpoint(category.CategoryType, roster, checkpoint))
            return Invalid("The checkpoint must describe the category roster, valid notes or a valid bracket.");

        if (progress is null)
        {
            progress = new CategoryJudgingProgress { CategoryId = categoryId };
            db.JudgingProgress.Add(progress);
        }
        progress.CheckpointJson = JsonSerializer.Serialize(checkpoint);
        progress.LastReleasedToken = request.SessionToken;
        progress.RequestedFor = null;
        await CategoryClaimEndpoints.RevokeClaim(category,
            CategoryClaimEndpoints.GetClaimReference(request.SessionToken), db, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Results.NoContent();
    }

    private static async Task<CategoryJudgingProgress> GetOrCreateProgressAsync(
        TournamentDbContext db, int categoryId, CancellationToken cancellationToken)
    {
        var progress = await db.JudgingProgress.FindAsync([categoryId], cancellationToken);
        if (progress is not null)
            return progress;
        progress = new CategoryJudgingProgress { CategoryId = categoryId };
        db.JudgingProgress.Add(progress);
        return progress;
    }

    private static bool ValidCheckpoint(CategoryType discipline, int[] roster, JudgingCheckpoint checkpoint)
    {
        if (roster.Length == 0 || (checkpoint.RankedParticipantIds is { } ranking
            && (!checkpoint.IsCompleteLocally || !ranking.Order().SequenceEqual(roster.Order()))))
            return false;
        if (discipline == CategoryType.Kumite)
            return checkpoint.Individual is null && ValidBracket(roster, checkpoint);
        if (discipline is not (CategoryType.Kata or CategoryType.Kihon or CategoryType.KobudoShort or CategoryType.KobudoLong)
            || checkpoint.Bracket is not null || checkpoint.Individual is not { } draft
            || draft.Results is null || draft.CurrentScores is null
            || draft.ParticipantIndex < 0 || draft.ParticipantIndex >= roster.Length)
            return false;

        var expectedResults = checkpoint.IsCompleteLocally ? roster.Length : draft.ParticipantIndex;
        return (!checkpoint.IsCompleteLocally || (draft.ParticipantIndex == roster.Length - 1 && draft.CurrentScores.Length == 0))
            && draft.Results.Length == expectedResults && draft.CurrentScores.All(ValidNote)
            && draft.Results.Select((result, index) => result is not null && result.ParticipantId == roster[index]
                && result.Notes is { Length: >= 3 } && result.Notes.All(ValidNote)).All(valid => valid);
    }

    private static bool ValidNote(decimal score) => score is >= 0 and <= 10 && decimal.Round(score, 2) == score;

    private static bool ValidBracket(int[] roster, JudgingCheckpoint checkpoint)
    {
        if (checkpoint.Bracket is not { Length: > 0 } matches || (matches.Length & (matches.Length + 1)) != 0)
            return false;
        var participants = roster.ToHashSet();
        for (var i = 0; i < matches.Length; i++)
        {
            var match = matches[i];
            if (match is null || match.BracketPosition != i || match.IsRunning
                || match.AkaScore < 0 || match.ShiroScore < 0 || !double.IsFinite(match.TimeRemaining) || match.TimeRemaining < 0
                || match.AtenaiAka < 0 || match.AtenaiShiro < 0 || match.ChukokuAka < 0 || match.ChukokuShiro < 0
                || match.OvertimeCount < 0 || (match.HasSenshuAka && match.HasSenshuShiro)
                || (match.AkaParticipantId is int aka && (!participants.Contains(aka) || aka == match.ShiroParticipantId))
                || (match.ShiroParticipantId is int shiro && !participants.Contains(shiro))
                || (match.WinnerParticipantId is int winner && (!match.IsFinished
                    || (winner != match.AkaParticipantId && winner != match.ShiroParticipantId))))
                return false;
        }

        var startingParticipants = matches.Skip(matches.Length / 2)
            .SelectMany(match => new[] { match.AkaParticipantId, match.ShiroParticipantId })
            .OfType<int>().Order();
        return startingParticipants.SequenceEqual(roster.Order())
            && (checkpoint.IsCompleteLocally
                ? checkpoint.RankedParticipantIds is { Length: > 0 } ranking && matches[0].WinnerParticipantId == ranking[0]
                : !matches[0].IsFinished);
    }

    private static IResult Conflict() => Results.Problem(statusCode: 409,
        title: "The expected judging session is no longer active. Refresh the category.");

    private static IResult Invalid(string message) => Results.Problem(statusCode: 400, title: message);
}
