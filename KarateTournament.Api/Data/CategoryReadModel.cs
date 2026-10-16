using KarateTournament.Api.Contracts;
using KarateTournament.Api.Entities;
using Microsoft.EntityFrameworkCore;

namespace KarateTournament.Api.Data;

public static class CategoryReadModel
{
    // Call ToDto only after loading this full graph (or for a newly created category).
    public static IQueryable<Category> WithDetails(this IQueryable<Category> query) => query
        .AsNoTracking()
        .Include(category => category.ParticipantLinks)
        .Include(category => category.AllowedBeltLinks)
        .Include(category => category.BracketMatches)
        .Include(category => category.Teams).ThenInclude(team => team.MemberLinks)
        .Include(category => category.FinalResults).ThenInclude(result => result.JudgeScores)
        .AsSplitQuery();

    public static CategoryDto ToDto(Category category) => new(
        category.Id, category.Name, category.MinAge, category.MaxAge, category.IsFinished,
        category.Sex, category.CategoryType,
        category.AllowedBeltLinks.OrderBy(link => link.SortOrder).Select(link => link.Belt).ToArray(),
        category.ParticipantLinks.OrderBy(link => link.SortOrder).Select(link => link.ParticipantId).ToArray(),
        category.BracketMatches.OrderBy(match => match.BracketPosition).Select(ToMatchDto).ToArray(),
        category.Teams.OrderBy(team => team.SortOrder).Select(team => new TeamDto(
            team.Id, team.Name, team.Sex,
            team.MemberLinks.OrderBy(link => link.SortOrder).Select(link => link.ParticipantId).ToArray()))
            .ToArray(),
        category.FinalResults.OrderBy(result => result.Rank ?? int.MaxValue).ThenBy(result => result.Id)
            .Select(result => new ParticipantResultDto(
                result.Id, result.ParticipantId, result.Rank, result.Score,
                result.JudgeScores.OrderBy(score => score.Ordinal)
                    .Select(score => new JudgeScoreDto(score.Ordinal, score.Score, score.IsDiscarded))
                    .ToArray()))
            .ToArray(), category.ClaimToken.HasValue, category.ClaimedBy);

    private static MatchDto ToMatchDto(Match match) => new(
        match.Id, match.BracketPosition, match.AkaParticipantId, match.ShiroParticipantId,
        match.WinnerParticipantId, match.AkaScore, match.ShiroScore, match.IsFinished,
        match.IsDisqualification, match.TimeRemaining, match.AtenaiAka, match.AtenaiShiro,
        match.ChukokuAka, match.ChukokuShiro, match.IsRunning, match.SenshuEnabled,
        match.HasSenshuAka, match.HasSenshuShiro, match.IsInOvertime, match.OvertimeCount);
}
