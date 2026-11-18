using System.Data;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using KarateClassLibrary.Contracts;
using KarateTournament.Api.Access;
using KarateTournament.Api.Contracts;
using KarateTournament.Api.Data;
using KarateTournament.Api.Entities;
using Microsoft.EntityFrameworkCore;

namespace KarateTournament.Api.Endpoints;

public static class CategoryClaimEndpoints
{
    public static IEndpointRouteBuilder MapCategoryClaimEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/categories/{categoryId:int}/claim", ClaimCategory).WithName("ClaimCategory");
        endpoints.MapPost("/api/categories/{categoryId:int}/release", ReleaseCategory).WithName("ReleaseCategory");

        var recovery = endpoints.MapGroup("/api/categories/{categoryId:int}")
            .AddEndpointFilter(RequireOrganizerRecoveryKey).WithMetadata(TournamentRouteAccess.Recovery);
        recovery.MapGet("/claim-recovery", GetRecoveryTarget).WithName("GetCategoryClaimRecovery");
        recovery.MapPost("/claim-recovery", RecoverClaim).WithName("RecoverCategoryClaim");
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

        await using var transaction = await dbContext.Database.BeginTransactionAsync(
            IsolationLevel.ReadCommitted, cancellationToken);
        var category = await dbContext.LockCategoryAsync(categoryId, cancellationToken);
        if (category is null)
            return Results.NotFound();
        if (category.IsFinished)
            return ClaimConflict();
        if (category.ClaimToken.HasValue)
            return category.ClaimToken == request.SessionToken && category.ClaimedBy == stationName
                ? Results.NoContent() : ClaimConflict();

        // A separate query after acquiring the lock sees revocations that committed while we waited.
        var claimReference = GetClaimReference(request.SessionToken);
        if (await dbContext.RevokedCategoryClaims.AnyAsync(
            claim => claim.CategoryId == categoryId && claim.ClaimReference == claimReference, cancellationToken))
            return ClaimConflict();

        category.ClaimToken = request.SessionToken;
        category.ClaimedBy = stationName;
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Results.NoContent();
    }

    private static async Task<IResult> ReleaseCategory(
        int categoryId, ReleaseCategoryRequest request, TournamentDbContext dbContext,
        CancellationToken cancellationToken)
    {
        if (request.SessionToken == Guid.Empty)
            return Results.Problem(statusCode: StatusCodes.Status400BadRequest,
                title: "A non-empty session token is required.");

        await using var transaction = await dbContext.Database.BeginTransactionAsync(
            IsolationLevel.ReadCommitted, cancellationToken);
        var category = await dbContext.LockCategoryAsync(categoryId, cancellationToken);
        if (category is null)
            return Results.NotFound();
        if (category.ClaimToken is null)
            return Results.NoContent();
        if (category.ClaimToken != request.SessionToken)
            return ClaimConflict();

        await RevokeClaim(category, GetClaimReference(request.SessionToken), dbContext, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Results.NoContent();
    }

    private static async Task<IResult> GetRecoveryTarget(
        int categoryId, TournamentDbContext dbContext, CancellationToken cancellationToken)
    {
        var category = await dbContext.Categories.AsNoTracking()
            .SingleOrDefaultAsync(category => category.Id == categoryId, cancellationToken);
        if (category is null)
            return Results.NotFound();
        if (category.IsFinished || category.ClaimToken is not Guid token)
            return RecoveryConflict();
        return Results.Ok(new ClaimRecoveryDto(GetClaimReference(token), category.ClaimedBy));
    }

    private static async Task<IResult> RecoverClaim(
        int categoryId, RecoverCategoryClaimRequest request, TournamentDbContext dbContext,
        CancellationToken cancellationToken)
    {
        if (!IsHexDigest(request.ClaimReference))
            return Results.Problem(statusCode: StatusCodes.Status400BadRequest,
                title: "Provide a valid claim reference from GET claim-recovery.");

        await using var transaction = await dbContext.Database.BeginTransactionAsync(
            IsolationLevel.ReadCommitted, cancellationToken);
        var category = await dbContext.LockCategoryAsync(categoryId, cancellationToken);
        if (category is null)
            return Results.NotFound();
        if (category.IsFinished || category.ClaimToken is not Guid token
            || !string.Equals(GetClaimReference(token), request.ClaimReference, StringComparison.OrdinalIgnoreCase))
            return RecoveryConflict();

        await RevokeClaim(category, GetClaimReference(token), dbContext, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Results.NoContent();
    }

    private static async Task RevokeClaim(
        Category category, string claimReference, TournamentDbContext dbContext, CancellationToken cancellationToken)
    {
        dbContext.RevokedCategoryClaims.Add(new RevokedCategoryClaim
        {
            CategoryId = category.Id,
            ClaimReference = claimReference
        });
        category.ClaimToken = null;
        category.ClaimedBy = null;
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static async ValueTask<object?> RequireOrganizerRecoveryKey(
        EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var http = context.HttpContext;
        http.Response.Headers.CacheControl = "no-store";
        var key = http.RequestServices.GetRequiredService<IConfiguration>()["Organizer:RecoveryKey"];
        if (!IsHexDigest(key))
            return Results.Problem(statusCode: StatusCodes.Status503ServiceUnavailable,
                title: "Organizer claim recovery is not configured.");

        if (!AuthenticationHeaderValue.TryParse(http.Request.Headers.Authorization, out var authorization)
            || !authorization.Scheme.Equals("Bearer", StringComparison.OrdinalIgnoreCase)
            || authorization.Parameter is not { Length: 64 } provided
            || !CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(key!), Encoding.UTF8.GetBytes(provided)))
        {
            http.Response.Headers.WWWAuthenticate = "Bearer";
            return Results.Unauthorized();
        }

        return await next(context);
    }

    private static bool IsHexDigest(string? value) => value is { Length: 64 } && value.All(Uri.IsHexDigit);
    private static string GetClaimReference(Guid token) => Convert.ToHexString(SHA256.HashData(token.ToByteArray()));

    private static IResult ClaimConflict() => Results.Problem(statusCode: StatusCodes.Status409Conflict,
        title: "Category is finished, belongs to another session, or the session was revoked.");
    private static IResult RecoveryConflict() => Results.Problem(statusCode: StatusCodes.Status409Conflict,
        title: "The expected claim is not active. Refresh the category before attempting recovery.");
}
