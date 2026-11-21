using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using KarateClassLibrary;
using KarateClassLibrary.Scoring;
using KarateClassLibrary.Contracts;
using KarateTournament.Api.Data;
using KarateTournament.Api.Entities;
using Microsoft.EntityFrameworkCore;

namespace KarateTournament.Api.Endpoints;

internal static class KataResultEndpoints
{
    public static void MapKataResultEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/categories/{categoryId:int}/individual-results", CompleteIndividual)
            .WithName("CompleteIndividualCategory");
        endpoints.MapPost("/api/categories/{categoryId:int}/team-results", CompleteTeam)
            .WithName("CompleteTeamCategory");
    }

    private static Task<IResult> CompleteIndividual(int categoryId, CompleteIndividualCategoryRequest request,
        TournamentDbContext db, CancellationToken cancellationToken) =>
        CompleteCategory(categoryId, request.SessionToken, request.Results?.Select(result => result is null ? null
            : new RankedNotes(result.ParticipantId, result.Rank, result.JudgeScores)).ToArray(),
            teams: false, db, cancellationToken);

    private static Task<IResult> CompleteTeam(int categoryId, CompleteTeamCategoryRequest request,
        TournamentDbContext db, CancellationToken cancellationToken) =>
        CompleteCategory(categoryId, request.SessionToken, request.Results?.Select(result => result is null ? null
            : new RankedNotes(result.TeamId, result.Rank, result.JudgeScores)).ToArray(),
            teams: true, db, cancellationToken);

    private static async Task<IResult> CompleteCategory(
        int categoryId, Guid sessionToken, RankedNotes?[]? input, bool teams, TournamentDbContext db,
        CancellationToken cancellationToken)
    {
        if (sessionToken == Guid.Empty || input is null || input.Length == 0
            || input.Any(result => result is null || result.EntryId <= 0
                || result.JudgeScores is null || result.JudgeScores.Length < 3
                || !result.JudgeScores.All(JudgingScoreCalculator.IsValidJudgeNote)))
            return Invalid("Provide a session token, entries and at least three notes per result (0–10, max two decimal places).");
        var ordered = input.OfType<RankedNotes>().OrderBy(result => result.Rank).ToArray();
        if (!ordered.Select(result => result.Rank).SequenceEqual(Enumerable.Range(1, ordered.Length))
            || ordered.Select(result => result.EntryId).Distinct().Count() != ordered.Length)
            return Invalid("Entries must be unique; ranks must be exactly 1 through the number of results.");

        var results = ordered.Select(result =>
        {
            var (score, discarded) = JudgingScoreCalculator.CalculateFinalScore(result.JudgeScores!);
            var notes = result.JudgeScores!.Select((note, index) => new JudgeScoreDto(index, note, discarded.Contains(index))).ToArray();
            return (result.EntryId, result.Rank, Score: score, Notes: notes);
        }).ToArray();
        if (results.Any(result => result.Score > 99999.99m)
            || results.Zip(results.Skip(1)).Any(pair => pair.First.Score < pair.Second.Score))
            return Invalid("Ranks must follow descending final scores; ties may be resolved locally. Score exceeds storage precision.");

        // Canonical numeric formatting makes retries independent of JSON order and decimal scale.
        var canonical = string.Join(";", ordered.Select(result =>
            $"{result.EntryId}:{result.Rank}:" + string.Join(",", result.JudgeScores!
                .Select(score => score.ToString("G29", CultureInfo.InvariantCulture)))));
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var category = await db.LockCategoryAsync(categoryId, cancellationToken);
        if (category is null) return Results.NotFound();
        var supported = teams ? category.CategoryType == CategoryType.Team
            : category.CategoryType is CategoryType.Kata or CategoryType.Kihon or CategoryType.KobudoShort or CategoryType.KobudoLong;
        if (!supported)
            return Invalid("Use the result endpoint for this category discipline.");
        if (category.IsFinished) return RetryOrConflict(category, sessionToken, hash);
        if (category.ClaimToken != sessionToken) return Conflict();

        var roster = teams ? db.Teams.Where(team => team.CategoryId == categoryId).Select(team => team.Id)
            : db.Set<CategoryParticipant>().Where(link => link.CategoryId == categoryId).Select(link => link.ParticipantId);
        if (!(await roster.Order().ToArrayAsync(cancellationToken)).SequenceEqual(ordered.Select(result => result.EntryId).Order()))
            return Invalid("Results must cover exactly all entries in the category roster.");

        if (teams)
        {
            if (await db.TeamResults.AnyAsync(result => result.CategoryId == categoryId, cancellationToken))
                return Conflict();
            db.TeamResults.AddRange(results.Select(result => new TeamResult
            {
                CategoryId = categoryId, TeamId = result.EntryId, Rank = result.Rank, Score = result.Score,
                JudgeScores = result.Notes.Select(note => new TeamJudgeScore
                    { Ordinal = note.Ordinal, Score = note.Score, IsDiscarded = note.IsDiscarded }).ToList()
            }));
        }
        else
        {
            if (await db.ParticipantResults.AnyAsync(result => result.CategoryId == categoryId, cancellationToken))
                return Conflict();
            db.ParticipantResults.AddRange(results.Select(result => new ParticipantResult
            {
                CategoryId = categoryId, ParticipantId = result.EntryId, Rank = result.Rank, Score = result.Score,
                JudgeScores = result.Notes.Select(note => new ParticipantJudgeScore
                    { Ordinal = note.Ordinal, Score = note.Score, IsDiscarded = note.IsDiscarded }).ToList()
            }));
        }

        category.IsFinished = true;
        category.ClaimToken = null;
        category.ClaimedBy = null;
        category.CompletionToken = sessionToken;
        category.CompletionHash = hash;
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Results.NoContent();
    }

    private static IResult RetryOrConflict(Category category, Guid token, string hash) =>
        category.IsFinished && category.CompletionToken == token && category.CompletionHash == hash
            ? Results.NoContent() : Conflict();
    private static IResult Conflict() => Results.Problem(statusCode: 409,
        title: "Category belongs to another session, already has results, or was completed with different results.");
    private static IResult Invalid(string message) => Results.Problem(statusCode: 400, title: message);

    private sealed record RankedNotes(int EntryId, int Rank, decimal[]? JudgeScores);
}
