using System.ComponentModel.DataAnnotations;
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
        return endpoints;
    }

    private static async Task<IResult> CreateCategory(
        CreateCategoryRequest request,
        TournamentDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var validationResults = new List<ValidationResult>();
        if (!Validator.TryValidateObject(request, new ValidationContext(request), validationResults, true))
        {
            var errors = validationResults
                .SelectMany(result => result.MemberNames.Select(member => new
                {
                    Member = member,
                    Message = result.ErrorMessage ?? "Invalid value."
                }))
                .GroupBy(error => error.Member)
                .ToDictionary(group => group.Key, group => group.Select(error => error.Message).ToArray());
            return Results.ValidationProblem(errors);
        }

        var participantIds = request.ParticipantIds!;
        var existingIds = await dbContext.Participants
            .Where(participant => participantIds.Contains(participant.Id))
            .Select(participant => participant.Id)
            .ToArrayAsync(cancellationToken);
        var missingIds = participantIds.Except(existingIds).ToArray();
        if (missingIds.Length > 0)
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                [nameof(request.ParticipantIds)] = [$"Unknown participant IDs: {string.Join(", ", missingIds)}."]
            });

        var category = new Category
        {
            Name = request.Name!.Trim(),
            MinAge = request.MinAge,
            MaxAge = request.MaxAge,
            Sex = request.Sex!.Value,
            CategoryType = request.CategoryType!.Value,
            AllowedBeltLinks = request.AllowedBelts!
                .Select((belt, index) => new CategoryAllowedBelt { Belt = belt, SortOrder = index })
                .ToList(),
            ParticipantLinks = participantIds
                .Select((id, index) => new CategoryParticipant { ParticipantId = id, SortOrder = index })
                .ToList()
        };

        dbContext.Categories.Add(category);
        await dbContext.SaveChangesAsync(cancellationToken);

        var dto = new CategoryDto(
            category.Id, category.Name, category.MinAge, category.MaxAge, category.IsFinished,
            category.Sex, category.CategoryType, request.AllowedBelts!, participantIds, [], [], []);
        return Results.CreatedAtRoute("GetTournamentSnapshot", value: dto);
    }
}
