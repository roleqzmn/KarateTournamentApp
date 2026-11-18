using System.Data;
using KarateTournament.Api.Contracts;
using KarateTournament.Api.Data;
using KarateTournament.Api.Entities;
using Microsoft.EntityFrameworkCore;

namespace KarateTournament.Api.Endpoints;

public static class CategoryEndpoints
{
    public static IEndpointRouteBuilder MapCategoryEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/categories", CreateCategory).WithName("CreateCategory");
        endpoints.MapGet("/api/categories", GetCategories).WithName("GetCategories");
        endpoints.MapGet("/api/categories/{categoryId:int}", GetCategory).WithName("GetCategory");
        endpoints.MapPut("/api/categories/{categoryId:int}", UpdateCategory).WithName("UpdateCategory");
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
                category.ClaimToken.HasValue, category.ClaimedBy))
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
        if (await ValidateParticipantIds(request.ParticipantIds!, dbContext, cancellationToken) is { } participantProblem)
            return participantProblem;

        var category = new Category();
        ApplySettings(category, request);
        dbContext.Categories.Add(category);
        await dbContext.SaveChangesAsync(cancellationToken);

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

        // Lock the same row that claim and completion update. Keep it locked while replacing the roster.
        var lockedCategories = await dbContext.Categories
            .FromSql($"""SELECT * FROM "Categories" WHERE "Id" = {categoryId} FOR NO KEY UPDATE""")
            .ToListAsync(cancellationToken);
        var category = lockedCategories.SingleOrDefault();
        if (category is null)
            return Results.NotFound();

        if (category.ClaimToken.HasValue || category.IsFinished
            || await dbContext.Categories.AnyAsync(other => other.Id == categoryId
                && (other.Teams.Any() || other.BracketMatches.Any() || other.FinalResults.Any()), cancellationToken))
        {
            return Results.Problem(statusCode: StatusCodes.Status409Conflict,
                title: "Claimed or finished categories and categories with teams, matches or results cannot be edited.");
        }

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

    private static async Task<IResult?> ValidateParticipantIds(
        int[] participantIds, TournamentDbContext dbContext, CancellationToken cancellationToken)
    {
        if (participantIds.Length == 0)
            return null;

        var existingIds = await dbContext.Participants
            .Where(participant => participantIds.Contains(participant.Id))
            .Select(participant => participant.Id)
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
