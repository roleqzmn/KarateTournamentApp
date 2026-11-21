using KarateClassLibrary;
using KarateClassLibrary.Contracts;
using KarateTournament.Api.Access;
using KarateTournament.Api.Data;
using KarateTournament.Api.Entities;
using Microsoft.EntityFrameworkCore;

namespace KarateTournament.Api.Endpoints;

internal static class TeamEndpoints
{
    public static void MapTeamEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var teams = endpoints.MapGroup("/api/categories/{categoryId:int}/teams");
        teams.MapGet("/{teamId:int}", GetTeam).WithName("GetTeam");
        teams.MapPost("", (int categoryId, SaveTeamRequest request, TournamentDbContext db, CancellationToken token) =>
            SaveTeamAsync(categoryId, null, request, db, token)).WithMetadata(TournamentRouteAccess.Organizer);
        teams.MapPut("/{teamId:int}", (int categoryId, int teamId, SaveTeamRequest request, TournamentDbContext db,
            CancellationToken token) => SaveTeamAsync(categoryId, teamId, request, db, token))
            .WithMetadata(TournamentRouteAccess.Organizer);
        teams.MapDelete("/{teamId:int}", DeleteTeam).WithMetadata(TournamentRouteAccess.Organizer);
    }

    private static async Task<IResult> GetTeam(int categoryId, int teamId, TournamentDbContext db, CancellationToken token)
    {
        var team = await db.Teams.AsNoTracking().Include(team => team.MemberLinks)
            .SingleOrDefaultAsync(team => team.Id == teamId && team.CategoryId == categoryId, token);
        return team is null ? Results.NotFound() : Results.Ok(CategoryReadModel.ToTeamDto(team));
    }

    private static async Task<IResult> SaveTeamAsync(
        int categoryId, int? teamId, SaveTeamRequest request, TournamentDbContext db, CancellationToken token)
    {
        if (RequestValidation.GetProblem(request) is { } problem)
            return problem;

        await using var transaction = await db.Database.BeginTransactionAsync(token);
        if (await CheckCategoryForWriteAsync(categoryId, db, token) is { } categoryProblem)
            return categoryProblem;

        Team team;
        if (teamId is int id)
        {
            var existing = await db.Teams.SingleOrDefaultAsync(team => team.Id == id && team.CategoryId == categoryId, token);
            if (existing is null)
                return Results.NotFound();
            team = existing;
        }
        else
        {
            var lastOrder = await db.Teams.Where(team => team.CategoryId == categoryId)
                .MaxAsync(team => (int?)team.SortOrder, token);
            team = new Team { CategoryId = categoryId, SortOrder = (lastOrder ?? -1) + 1 };
            db.Teams.Add(team);
        }

        if (await CategoryEndpoints.ValidateParticipantIds(request.MemberParticipantIds!, db, token,
            nameof(SaveTeamRequest.MemberParticipantIds)) is { } participantProblem)
            return participantProblem;

        if (teamId.HasValue)
        {
            // Remove old positions first so reordering members cannot violate the unique position index.
            await db.Set<TeamMember>().Where(member => member.TeamId == team.Id).ExecuteDeleteAsync(token);
        }

        team.Name = request.Name!.Trim();
        team.Sex = request.Sex!.Value;
        team.MemberLinks = request.MemberParticipantIds!.Select((id, index) =>
            new TeamMember { ParticipantId = id, SortOrder = index }).ToList();
        await db.SaveChangesAsync(token);
        await transaction.CommitAsync(token);

        var dto = CategoryReadModel.ToTeamDto(team);
        return teamId.HasValue ? Results.Ok(dto)
            : Results.CreatedAtRoute("GetTeam", new { categoryId, teamId = team.Id }, dto);
    }

    private static async Task<IResult> DeleteTeam(
        int categoryId, int teamId, TournamentDbContext db, CancellationToken token)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(token);
        if (await CheckCategoryForWriteAsync(categoryId, db, token) is { } problem)
            return problem;
        var team = await db.Teams.SingleOrDefaultAsync(team => team.Id == teamId && team.CategoryId == categoryId, token);
        if (team is null)
            return Results.NotFound();

        db.Teams.Remove(team);
        await db.SaveChangesAsync(token);
        await transaction.CommitAsync(token);
        return Results.NoContent();
    }

    private static async Task<IResult?> CheckCategoryForWriteAsync(int categoryId, TournamentDbContext db, CancellationToken token)
    {
        var category = await db.LockCategoryAsync(categoryId, token);
        if (category is null)
            return Results.NotFound();
        if (category.CategoryType != CategoryType.Team)
            return Conflict("Rosters can only be managed in a Team Kata category.");
        return await CategoryEndpoints.HasCompetitionData(category, db, token, includeTeams: false)
            ? Conflict("A claimed category or a category with judging data cannot change team rosters.")
            : null;
    }

    private static IResult Conflict(string title) => Results.Problem(statusCode: StatusCodes.Status409Conflict, title: title);
}
