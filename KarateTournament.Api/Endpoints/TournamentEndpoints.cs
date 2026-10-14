using System.Data;
using KarateTournament.Api.Contracts;
using KarateTournament.Api.Data;
using KarateTournament.Api.Entities;
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
            .AsNoTracking()
            .Include(category => category.ParticipantLinks)
            .Include(category => category.AllowedBeltLinks)
            .Include(category => category.BracketMatches)
            .Include(category => category.Teams)
                .ThenInclude(team => team.MemberLinks)
            .Include(category => category.FinalResults)
                .ThenInclude(result => result.JudgeScores)
            .AsSplitQuery()
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
            categories.Select(category => new CategoryDto(
                category.Id,
                category.Name,
                category.MinAge,
                category.MaxAge,
                category.IsFinished,
                category.Sex,
                category.CategoryType,
                category.AllowedBeltLinks
                    .OrderBy(link => link.SortOrder)
                    .Select(link => link.Belt)
                    .ToArray(),
                category.ParticipantLinks
                    .OrderBy(link => link.SortOrder)
                    .Select(link => link.ParticipantId)
                    .ToArray(),
                category.BracketMatches
                    .OrderBy(match => match.BracketPosition)
                    .Select(ToMatchDto)
                    .ToArray(),
                category.Teams
                    .OrderBy(team => team.SortOrder)
                    .Select(team => new TeamDto(
                        team.Id,
                        team.Name,
                        team.Sex,
                        team.MemberLinks
                            .OrderBy(link => link.SortOrder)
                            .Select(link => link.ParticipantId)
                            .ToArray()))
                    .ToArray(),
                category.FinalResults
                    .OrderBy(result => result.Rank ?? int.MaxValue)
                    .ThenBy(result => result.Id)
                    .Select(result => new ParticipantResultDto(
                        result.Id,
                        result.ParticipantId,
                        result.Rank,
                        result.Score,
                        result.JudgeScores
                            .OrderBy(score => score.Ordinal)
                            .Select(score => new JudgeScoreDto(
                                score.Ordinal,
                                score.Score,
                                score.IsDiscarded))
                            .ToArray()))
                    .ToArray()))
                .ToArray());

        await transaction.CommitAsync(cancellationToken);
        return Results.Ok(snapshot);
    }

    private static MatchDto ToMatchDto(Match match)
    {
        return new MatchDto(
            match.Id,
            match.BracketPosition,
            match.AkaParticipantId,
            match.ShiroParticipantId,
            match.WinnerParticipantId,
            match.AkaScore,
            match.ShiroScore,
            match.IsFinished,
            match.IsDisqualification,
            match.TimeRemaining,
            match.AtenaiAka,
            match.AtenaiShiro,
            match.ChukokuAka,
            match.ChukokuShiro,
            match.IsRunning,
            match.SenshuEnabled,
            match.HasSenshuAka,
            match.HasSenshuShiro,
            match.IsInOvertime,
            match.OvertimeCount);
    }
}
