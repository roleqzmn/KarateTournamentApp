using KarateTournament.Api.Contracts;
using KarateTournament.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace KarateTournament.Api.Endpoints;

public static class CategoryClaimEndpoints
{
    public static IEndpointRouteBuilder MapCategoryClaimEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/categories/{categoryId:int}/claim", ClaimCategory).WithName("ClaimCategory");
        endpoints.MapPost("/api/categories/{categoryId:int}/release", ReleaseCategory).WithName("ReleaseCategory");
        return endpoints;
    }

    private static async Task<IResult> ClaimCategory(
        int categoryId, ClaimCategoryRequest request, TournamentDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var stationName = request.StationName?.Trim();
        if (request.SessionToken == Guid.Empty || string.IsNullOrWhiteSpace(stationName) || stationName.Length > 100)
            return Results.Problem(statusCode: StatusCodes.Status400BadRequest,
                title: "A non-empty session token and a station name of 1–100 characters are required.");

        // One conditional UPDATE arbitrates competing clients in PostgreSQL.
        // Repeating a claim with the same token and station is idempotent.
        var affected = await dbContext.Categories
            .Where(category => category.Id == categoryId && !category.IsFinished
                && (category.ClaimToken == null
                    || (category.ClaimToken == request.SessionToken && category.ClaimedBy == stationName)))
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(category => category.ClaimToken, (Guid?)request.SessionToken)
                .SetProperty(category => category.ClaimedBy, stationName), cancellationToken);
        return affected > 0 ? Results.NoContent()
            : await UnavailableCategory(categoryId, dbContext, cancellationToken);
    }

    private static async Task<IResult> ReleaseCategory(
        int categoryId, ReleaseCategoryRequest request, TournamentDbContext dbContext,
        CancellationToken cancellationToken)
    {
        if (request.SessionToken == Guid.Empty)
            return Results.Problem(statusCode: StatusCodes.Status400BadRequest,
                title: "A non-empty session token is required.");

        // A delayed release from an old session cannot clear a newer session's claim.
        var affected = await dbContext.Categories
            .Where(category => category.Id == categoryId
                && (category.ClaimToken == request.SessionToken || category.ClaimToken == null))
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(category => category.ClaimToken, (Guid?)null)
                .SetProperty(category => category.ClaimedBy, (string?)null), cancellationToken);
        return affected > 0 ? Results.NoContent()
            : await UnavailableCategory(categoryId, dbContext, cancellationToken);
    }

    private static async Task<IResult> UnavailableCategory(
        int categoryId, TournamentDbContext dbContext, CancellationToken cancellationToken) =>
        await dbContext.Categories.AnyAsync(category => category.Id == categoryId, cancellationToken)
            ? Results.Problem(statusCode: StatusCodes.Status409Conflict,
                title: "Category is finished or belongs to another session.")
            : Results.NotFound();
}
