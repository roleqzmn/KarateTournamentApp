using System.Data;
using KarateTournament.Api.Contracts;
using KarateTournament.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace KarateTournament.Api.Endpoints;

public static class TournamentEndpoints
{
    public static IEndpointRouteBuilder MapTournamentEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/tournament", GetTournamentSnapshot)
            .WithName("GetTournamentSnapshot");

        return endpoints;
    }

    private static async Task<IResult> GetTournamentSnapshot(
        TournamentDbContext dbContext,
        CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(
            IsolationLevel.RepeatableRead,
            cancellationToken);

        var participants = await dbContext.Participants
            .AsNoTracking()
            .Include(participant => participant.CategoryTypes)
            .OrderBy(participant => participant.Id)
            .ToListAsync(cancellationToken);

        var categories = await dbContext.Categories
            .WithDetails()
            .OrderBy(category => category.Id)
            .ToListAsync(cancellationToken);

        var snapshot = new TournamentSnapshotDto(
            participants.Select(participant => new ParticipantDto(
                participant.Id,
                participant.FirstName,
                participant.LastName,
                participant.Age,
                participant.Club,
                participant.Belt,
                participant.Sex,
                participant.CategoryTypes
                    .OrderBy(link => link.CategoryType)
                    .Select(link => link.CategoryType)
                    .ToArray()))
                .ToArray(),
            categories.Select(CategoryReadModel.ToDto).ToArray());

        await transaction.CommitAsync(cancellationToken);
        return Results.Ok(snapshot);
    }
}
