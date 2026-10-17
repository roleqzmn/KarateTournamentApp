using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using KarateClassLibrary;
using KarateClassLibrary.Scoring;
using KarateTournament.Api.Contracts;
using KarateTournament.Api.Data;
using KarateTournament.Api.Entities;
using Microsoft.EntityFrameworkCore;

namespace KarateTournament.Api.Endpoints;

public static class IndividualResultEndpoints
{
    public static IEndpointRouteBuilder MapIndividualResultEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/categories/{categoryId:int}/individual-results", CompleteCategory)
            .WithName("CompleteIndividualCategory");
        return endpoints;
    }

    private static async Task<IResult> CompleteCategory(
        int categoryId, CompleteIndividualCategoryRequest request, TournamentDbContext db,
        CancellationToken cancellationToken)
    {
        var input = request.Results;
        if (request.SessionToken == Guid.Empty || input is null || input.Length == 0
            || input.Any(result => result is null || result.ParticipantId <= 0
                || result.JudgeScores is null || result.JudgeScores.Length < 3
                || result.JudgeScores.Any(score => score < 0 || score > 10 || decimal.Round(score, 2) != score)))
            return Invalid("Provide a session token, participants and at least three notes per result (0–10, max two decimal places).");
        var ordered = input.OrderBy(result => result.Rank).ToArray();
        if (!ordered.Select(result => result.Rank).SequenceEqual(Enumerable.Range(1, ordered.Length))
            || ordered.Select(result => result.ParticipantId).Distinct().Count() != ordered.Length)
            return Invalid("Participants must be unique; ranks must be exactly 1 through the number of results.");

        var results = ordered.Select(result =>
        {
            var (score, discarded) = JudgingScoreCalculator.CalculateFinalScore(result.JudgeScores!);
            return new ParticipantResult
            {
                CategoryId = categoryId, ParticipantId = result.ParticipantId, Rank = result.Rank, Score = score,
                JudgeScores = result.JudgeScores!.Select((note, index) => new ParticipantJudgeScore
                {
                    Ordinal = index, Score = note, IsDiscarded = discarded.Contains(index)
                }).ToList()
            };
        }).ToArray();
        if (results.Any(result => result.Score > 99999.99m)
            || results.Zip(results.Skip(1)).Any(pair => pair.First.Score < pair.Second.Score))
            return Invalid("Ranks must follow descending final scores; ties may be resolved locally. Score exceeds storage precision.");

        // Canonical numeric formatting makes retries independent of JSON order and decimal scale.
        var canonical = string.Join(";", ordered.Select(result =>
            $"{result.ParticipantId}:{result.Rank}:" + string.Join(",", result.JudgeScores!
                .Select(score => score.ToString("G29", CultureInfo.InvariantCulture)))));
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var category = await db.Categories.AsNoTracking().Include(category => category.ParticipantLinks)
            .SingleOrDefaultAsync(category => category.Id == categoryId, cancellationToken);
        if (category is null) return Results.NotFound();
        if (category.IsFinished) return RetryOrConflict(category, request.SessionToken, hash);
        if (category.ClaimToken != request.SessionToken) return Conflict();
        if (category.CategoryType is not (CategoryType.Kata or CategoryType.Kihon
            or CategoryType.KobudoShort or CategoryType.KobudoLong))
            return Invalid("This endpoint supports only individually scored categories.");
        if (!category.ParticipantLinks.Select(link => link.ParticipantId).Order()
            .SequenceEqual(ordered.Select(result => result.ParticipantId).Order()))
            return Invalid("Results must cover exactly all category participants.");
        // The guarded UPDATE serializes completion against release, reclaim and another submission.
        var affected = await db.Categories
            .Where(category => category.Id == categoryId && !category.IsFinished && category.ClaimToken == request.SessionToken)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(category => category.IsFinished, true)
                .SetProperty(category => category.ClaimToken, (Guid?)null)
                .SetProperty(category => category.ClaimedBy, (string?)null)
                .SetProperty(category => category.CompletionToken, (Guid?)request.SessionToken)
                .SetProperty(category => category.CompletionHash, hash), cancellationToken);
        if (affected == 0)
        {
            var current = await db.Categories.AsNoTracking()
                .SingleAsync(category => category.Id == categoryId, cancellationToken);
            return RetryOrConflict(current, request.SessionToken, hash);
        }
        if (await db.ParticipantResults.AnyAsync(result => result.CategoryId == categoryId, cancellationToken))
            return Conflict();
        db.ParticipantResults.AddRange(results);
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
}
