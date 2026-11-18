using System.Data;
using KarateClassLibrary.Contracts;
using KarateTournament.Api.Data;
using KarateTournament.Api.Entities;
using Microsoft.EntityFrameworkCore;

namespace KarateTournament.Api.Endpoints;

public static class ParticipantEndpoints
{
    public static IEndpointRouteBuilder MapParticipantEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/participants", CreateParticipant)
            .WithName("CreateParticipant");
        endpoints.MapGet("/api/participants/{participantId:int}", GetParticipant)
            .WithName("GetParticipant");
        endpoints.MapPut("/api/participants/{participantId:int}", UpdateParticipant)
            .WithName("UpdateParticipant");
        endpoints.MapDelete("/api/participants/{participantId:int}", DeleteParticipant)
            .WithName("DeleteParticipant");
        return endpoints;
    }

    private static async Task<IResult> CreateParticipant(
        CreateParticipantRequest request,
        TournamentDbContext dbContext,
        CancellationToken cancellationToken)
    {
        if (RequestValidation.GetProblem(request) is { } validationProblem)
            return validationProblem;

        var participant = new Participant();
        ApplyProfile(participant, request);
        dbContext.Participants.Add(participant);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Results.Created($"/api/participants/{participant.Id}", ToDto(participant));
    }

    private static async Task<IResult> UpdateParticipant(
        int participantId,
        CreateParticipantRequest request,
        TournamentDbContext dbContext,
        CancellationToken cancellationToken)
    {
        if (RequestValidation.GetProblem(request) is { } validationProblem)
            return validationProblem;

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var lockedParticipants = await dbContext.Participants
            .FromSql($"""SELECT * FROM "Participants" WHERE "Id" = {participantId} FOR NO KEY UPDATE""")
            .ToListAsync(cancellationToken);
        var participant = lockedParticipants.SingleOrDefault();
        if (participant is null)
            return Results.NotFound();

        await dbContext.Set<ParticipantCategoryType>().Where(link => link.ParticipantId == participantId)
            .ExecuteDeleteAsync(cancellationToken);
        ApplyProfile(participant, request);
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Results.Ok(ToDto(participant));
    }

    private static async Task<IResult> DeleteParticipant(
        int participantId,
        TournamentDbContext dbContext,
        CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(
            IsolationLevel.ReadCommitted, cancellationToken);
        var rows = await dbContext.Participants
            .FromSql($"""SELECT * FROM "Participants" WHERE "Id" = {participantId} FOR UPDATE""")
            .ToListAsync(cancellationToken);
        var participant = rows.SingleOrDefault();
        if (participant is null)
            return Results.NotFound();

        // Read references after acquiring the lock, in case an assignment committed while waiting.
        var hasMemberships = await dbContext.Participants.AnyAsync(person => person.Id == participantId
            && (person.CategoryMemberships.Any() || person.TeamMemberships.Any()), cancellationToken);
        if (hasMemberships
            || await dbContext.Matches.AnyAsync(match => match.AkaParticipantId == participantId
                || match.ShiroParticipantId == participantId || match.WinnerParticipantId == participantId, cancellationToken)
            || await dbContext.ParticipantResults.AnyAsync(result => result.ParticipantId == participantId, cancellationToken))
        {
            return Results.Problem(statusCode: StatusCodes.Status409Conflict,
                title: "A participant referenced by a category, team, match or result cannot be deleted.");
        }

        dbContext.Participants.Remove(participant);
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Results.NoContent();
    }

    private static void ApplyProfile(Participant participant, CreateParticipantRequest request)
    {
        participant.FirstName = request.FirstName!.Trim();
        participant.LastName = request.LastName!.Trim();
        participant.Age = request.Age!.Value;
        participant.Club = string.IsNullOrWhiteSpace(request.Club) ? null : request.Club.Trim();
        participant.Belt = request.Belt!.Value;
        participant.Sex = request.Sex!.Value;
        participant.CategoryTypes = request.CategoryTypes!
            .Select(type => new ParticipantCategoryType { CategoryType = type })
            .ToList();
    }

    private static async Task<IResult> GetParticipant(
        int participantId,
        TournamentDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var participant = await dbContext.Participants
            .AsNoTracking()
            .Include(participant => participant.CategoryTypes)
            .SingleOrDefaultAsync(participant => participant.Id == participantId, cancellationToken);

        return participant is null ? Results.NotFound() : Results.Ok(ToDto(participant));
    }

    private static ParticipantDto ToDto(Participant participant) => new(
        participant.Id,
        participant.FirstName,
        participant.LastName,
        participant.Age,
        participant.Club,
        participant.Belt,
        participant.Sex,
        participant.CategoryTypes.OrderBy(link => link.CategoryType).Select(link => link.CategoryType).ToArray());
}
