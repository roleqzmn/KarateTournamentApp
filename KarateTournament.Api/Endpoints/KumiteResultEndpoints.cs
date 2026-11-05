using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using KarateClassLibrary;
using KarateTournament.Api.Contracts;
using KarateTournament.Api.Data;
using KarateTournament.Api.Entities;
using Microsoft.EntityFrameworkCore;

namespace KarateTournament.Api.Endpoints;

public static class KumiteResultEndpoints
{
    public static IEndpointRouteBuilder MapKumiteResultEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/categories/{categoryId:int}/kumite-results", CompleteCategory)
            .WithName("CompleteKumiteCategory");
        return endpoints;
    }

    private static async Task<IResult> CompleteCategory(
        int categoryId, CompleteKumiteCategoryRequest request, TournamentDbContext db,
        CancellationToken cancellationToken)
    {
        var error = KumiteResultValidator.Validate(request);
        if (error is not null) return Results.Problem(statusCode: 400, title: error);
        var matches = request.Matches!.OrderBy(match => match.BracketPosition)
            .Select(match => match with { TimeRemaining = match.TimeRemaining == 0 ? 0 : match.TimeRemaining }).ToArray();
        var ranking = request.RankedParticipantIds!;
        var canonical = "kumite:v1:" + JsonSerializer.Serialize(new { Matches = matches, Ranking = ranking });
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var category = await db.Categories.AsNoTracking().Include(category => category.ParticipantLinks)
            .SingleOrDefaultAsync(category => category.Id == categoryId, cancellationToken);
        if (category is null) return Results.NotFound();
        if (category.IsFinished) return RetryOrConflict(category, request.SessionToken, hash);
        if (category.ClaimToken != request.SessionToken) return Conflict();
        if (category.CategoryType != CategoryType.Kumite
            || !category.ParticipantLinks.Select(link => link.ParticipantId).Order().SequenceEqual(ranking.Order()))
            return Results.Problem(statusCode: 400, title: "Provide results for exactly all participants of a Kumite category.");
        var affected = await db.Categories
            .Where(category => category.Id == categoryId && !category.IsFinished && category.ClaimToken == request.SessionToken)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(category => category.IsFinished, true)
                .SetProperty(category => category.ClaimToken, (Guid?)null)
                .SetProperty(category => category.ClaimedBy, (string?)null)
                .SetProperty(category => category.CompletionToken, (Guid?)request.SessionToken)
                .SetProperty(category => category.CompletionHash, hash), cancellationToken);
        if (affected == 0)
            return RetryOrConflict(await db.Categories.AsNoTracking()
                .SingleAsync(category => category.Id == categoryId, cancellationToken), request.SessionToken, hash);
        if (await db.Matches.AnyAsync(match => match.CategoryId == categoryId, cancellationToken)
            || await db.ParticipantResults.AnyAsync(result => result.CategoryId == categoryId, cancellationToken))
            return Conflict();
        db.Matches.AddRange(matches.Select(match => new Match
        {
            CategoryId = categoryId, BracketPosition = match.BracketPosition,
            AkaParticipantId = match.AkaParticipantId, ShiroParticipantId = match.ShiroParticipantId,
            WinnerParticipantId = match.WinnerParticipantId, AkaScore = match.AkaScore, ShiroScore = match.ShiroScore,
            IsFinished = match.WinnerParticipantId.HasValue, IsDisqualification = match.IsDisqualification,
            TimeRemaining = match.TimeRemaining, AtenaiAka = match.AtenaiAka, AtenaiShiro = match.AtenaiShiro,
            ChukokuAka = match.ChukokuAka, ChukokuShiro = match.ChukokuShiro, SenshuEnabled = match.SenshuEnabled,
            HasSenshuAka = match.HasSenshuAka, HasSenshuShiro = match.HasSenshuShiro,
            IsInOvertime = match.IsInOvertime, OvertimeCount = match.OvertimeCount, IsRunning = false
        }));
        db.ParticipantResults.AddRange(ranking.Select((id, index) => new ParticipantResult
        {
            CategoryId = categoryId, ParticipantId = id, Rank = index + 1, Score = ranking.Length - index
        }));
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Results.NoContent();
    }

    private static IResult RetryOrConflict(Category category, Guid token, string hash) =>
        category.IsFinished && category.CompletionToken == token && category.CompletionHash == hash
            ? Results.NoContent() : Conflict();
    private static IResult Conflict() => Results.Problem(statusCode: 409,
        title: "Category belongs to another session, already has competition data, or was completed with different results.");
}
