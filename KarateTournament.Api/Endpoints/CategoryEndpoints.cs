using System.Data;
using KarateClassLibrary.Contracts;
using KarateTournament.Api.Access;
using KarateTournament.Api.Data;
using KarateTournament.Api.Entities;
using Microsoft.EntityFrameworkCore;

namespace KarateTournament.Api.Endpoints;

public static class CategoryEndpoints
{
    public static IEndpointRouteBuilder MapCategoryEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/categories", CreateCategory).WithName("CreateCategory").WithMetadata(TournamentRouteAccess.Organizer);
        endpoints.MapGet("/api/categories", GetCategories).WithName("GetCategories");
        endpoints.MapGet("/api/categories/{categoryId:int}", GetCategory).WithName("GetCategory");
        endpoints.MapPut("/api/categories/{categoryId:int}", UpdateCategory).WithName("UpdateCategory").WithMetadata(TournamentRouteAccess.Organizer);
        endpoints.MapDelete("/api/categories/{categoryId:int}", DeleteCategory).WithName("DeleteCategory").WithMetadata(TournamentRouteAccess.Organizer);
        return endpoints;
    }

    private static async Task<IResult> GetCategories(
        TournamentDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var categories = await dbContext.Categories.AsNoTracking()
            .OrderBy(category => category.Id)
            .Select(category => new CategorySummaryDto(
                category.Id, category.Name, category.CategoryType, category.Sex,
                category.MinAge, category.MaxAge, category.IsFinished,
                category.ParticipantLinks.Count, category.Teams.Count,
                category.ClaimToken.HasValue, category.ClaimedBy,
                !category.IsFinished && category.JudgingProgress != null && category.JudgingProgress.CheckpointJson != null,
                category.ClaimToken.HasValue && category.JudgingProgress != null
                    && category.JudgingProgress.RequestedFor == category.ClaimToken))
            .ToArrayAsync(cancellationToken);
        return Results.Ok(categories);
    }

    private static async Task<IResult> GetCategory(
        int categoryId,
        TournamentDbContext dbContext,
        CancellationToken cancellationToken)
    {
        // WithDetails runs several queries; all must see the same database snapshot.
        await using var transaction = await dbContext.Database.BeginTransactionAsync(
            IsolationLevel.RepeatableRead, cancellationToken);
        var category = await dbContext.Categories.WithDetails()
            .SingleOrDefaultAsync(category => category.Id == categoryId, cancellationToken);
        var dto = category is null ? null : CategoryReadModel.ToDto(category);
        await transaction.CommitAsync(cancellationToken);
        return dto is null ? Results.NotFound() : Results.Ok(dto);
    }

    private static async Task<IResult> CreateCategory(
        CreateCategoryRequest request,
        TournamentDbContext dbContext,
        CancellationToken cancellationToken)
    {
        if (RequestValidation.GetProblem(request) is { } validationProblem)
            return validationProblem;

        await using var transaction = await dbContext.Database.BeginTransactionAsync(
            IsolationLevel.ReadCommitted, cancellationToken);
        if (await ValidateParticipantIds(request.ParticipantIds!, dbContext, cancellationToken) is { } participantProblem)
            return participantProblem;

        var category = new Category();
        ApplySettings(category, request);
        dbContext.Categories.Add(category);
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return Results.CreatedAtRoute("GetCategory", new { categoryId = category.Id },
            CategoryReadModel.ToDto(category));
    }

    private static async Task<IResult> UpdateCategory(
        int categoryId,
        CreateCategoryRequest request,
        TournamentDbContext dbContext,
        CancellationToken cancellationToken)
    {
        if (RequestValidation.GetProblem(request) is { } validationProblem)
            return validationProblem;

        await using var transaction = await dbContext.Database.BeginTransactionAsync(
            IsolationLevel.ReadCommitted, cancellationToken);

        var category = await dbContext.LockCategoryAsync(categoryId, cancellationToken);
        if (category is null)
            return Results.NotFound();

        if (await HasCompetitionData(category, dbContext, cancellationToken))
            return CategoryConflict();

        if (await ValidateParticipantIds(request.ParticipantIds!, dbContext, cancellationToken) is { } participantProblem)
            return participantProblem;

        // Delete first so a swap of two positions cannot violate the unique SortOrder index mid-update.
        await dbContext.Set<CategoryAllowedBelt>().Where(link => link.CategoryId == categoryId)
            .ExecuteDeleteAsync(cancellationToken);
        await dbContext.Set<CategoryParticipant>().Where(link => link.CategoryId == categoryId)
            .ExecuteDeleteAsync(cancellationToken);
        ApplySettings(category, request);
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Results.Ok(CategoryReadModel.ToDto(category));
    }

    private static async Task<IResult> DeleteCategory(
        int categoryId,
        TournamentDbContext dbContext,
        CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(
            IsolationLevel.ReadCommitted, cancellationToken);
        var category = await dbContext.LockCategoryAsync(categoryId, cancellationToken);
        if (category is null)
            return Results.NotFound();
        if (await HasCompetitionData(category, dbContext, cancellationToken))
            return CategoryConflict();

        dbContext.Categories.Remove(category);
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Results.NoContent();
    }

    private static async Task<bool> HasCompetitionData(
        Category category, TournamentDbContext dbContext, CancellationToken cancellationToken) =>
        category.ClaimToken.HasValue || category.IsFinished
        || await dbContext.Categories.AnyAsync(other => other.Id == category.Id
            && (other.Teams.Any() || other.BracketMatches.Any() || other.FinalResults.Any()
                || (other.JudgingProgress != null && other.JudgingProgress.CheckpointJson != null)), cancellationToken);

    private static IResult CategoryConflict() => Results.Problem(statusCode: StatusCodes.Status409Conflict,
        title: "Claimed or finished categories and categories with judging progress, teams, matches or results cannot be edited or deleted.");

    private static async Task<IResult?> ValidateParticipantIds(
        int[] participantIds, TournamentDbContext dbContext, CancellationToken cancellationToken)
    {
        if (participantIds.Length == 0)
            return null;

        // Keep validated IDs alive until the category and its memberships commit.
        var existingIds = await dbContext.Database.SqlQuery<int>($"""
            SELECT "Id" FROM "Participants" WHERE "Id" = ANY ({participantIds})
            ORDER BY "Id" FOR KEY SHARE
            """)
            .ToArrayAsync(cancellationToken);
        var missingIds = participantIds.Except(existingIds).ToArray();
        if (missingIds.Length > 0)
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                [nameof(CreateCategoryRequest.ParticipantIds)] = [$"Unknown participant IDs: {string.Join(", ", missingIds)}."]
            });

        return null;
    }

    private static void ApplySettings(Category category, CreateCategoryRequest request)
    {
        category.Name = request.Name!.Trim();
        category.MinAge = request.MinAge;
        category.MaxAge = request.MaxAge;
        category.Sex = request.Sex!.Value;
        category.CategoryType = request.CategoryType!.Value;
        category.AllowedBeltLinks = request.AllowedBelts!
            .Select((belt, index) => new CategoryAllowedBelt { Belt = belt, SortOrder = index })
            .ToList();
        category.ParticipantLinks = request.ParticipantIds!
            .Select((id, index) => new CategoryParticipant { ParticipantId = id, SortOrder = index })
            .ToList();
    }
}
