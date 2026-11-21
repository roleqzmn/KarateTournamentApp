using KarateClassLibrary;
using KarateTournament.Api.Entities;
using Microsoft.EntityFrameworkCore;

namespace KarateTournament.Api.Data;

internal static class ParticipantCategoryAssignment
{
    // Called inside a transaction. Serialize automatic assignments even when no matching category exists yet.
    public static async Task AssignAsync(TournamentDbContext db, Participant[] participants,
        CategoryGrouping grouping, CancellationToken cancellationToken)
    {
        var disciplines = participants.SelectMany(person => person.CategoryTypes)
            .Select(link => link.CategoryType).Where(type => type != CategoryType.Team)
            .Distinct().Select(type => (int)type).ToArray();
        if (disciplines.Length == 0)
            return;

        await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(57021, 1)", cancellationToken);
        // Category edits and judging take the same row lock. Load the roster and eligibility after acquiring it.
        var lockedIds = await db.Database.SqlQuery<int>($"""
            SELECT "Id" FROM "Categories"
            WHERE "CategoryType" = ANY ({disciplines}) AND NOT "IsFinished" AND "ClaimToken" IS NULL
            ORDER BY "Id" FOR UPDATE
            """).ToArrayAsync(cancellationToken);
        var available = await db.Categories
            .Where(category => lockedIds.Contains(category.Id)
                && !category.Teams.Any() && !category.BracketMatches.Any() && !category.FinalResults.Any()
                && (category.JudgingProgress == null || category.JudgingProgress.CheckpointJson == null))
            .Include(category => category.AllowedBeltLinks).Include(category => category.ParticipantLinks)
            .AsSplitQuery().OrderBy(category => category.Id).ToListAsync(cancellationToken);

        foreach (var participant in participants)
        {
            var senior = participant.Age >= ParticipantCategoryRules.SeniorAge;
            foreach (var discipline in participant.CategoryTypes.Select(link => link.CategoryType)
                         .Where(type => type != CategoryType.Team))
            {
                var category = available.FirstOrDefault(candidate => candidate.CategoryType == discipline
                    && ParticipantCategoryRules.Matches(participant.Age, participant.Sex, senior, grouping,
                        candidate.MinAge, candidate.MaxAge, candidate.Sex,
                        candidate.AllowedBeltLinks.Any(link => link.Belt == participant.Belt)));
                if (category is null)
                {
                    category = GenerateCategory(participant, discipline, grouping, senior);
                    db.Categories.Add(category);
                    available.Add(category);
                }

                category.ParticipantLinks.Add(new CategoryParticipant
                {
                    Participant = participant,
                    SortOrder = category.ParticipantLinks.Select(link => link.SortOrder).DefaultIfEmpty(-1).Max() + 1
                });
            }
        }
    }

    private static Category GenerateCategory(Participant participant, CategoryType discipline,
        CategoryGrouping grouping, bool senior)
    {
        var (minAge, maxAge, singleBelt) = ParticipantCategoryRules.GetSettings(participant.Age, participant.Belt, grouping, senior);
        Belts[] belts = singleBelt is Belts belt ? [belt] : Enum.GetValues<Belts>();
        var sex = participant.Sex switch
        {
            Sex.Male => "M",
            Sex.Female => "K",
            _ => "Unisex"
        };
        var ageLabel = senior ? "Senior" : minAge == maxAge ? $"{minAge} lat" : $"{minAge}–{maxAge} lat";
        return new Category
        {
            Name = $"{discipline} · {sex} · {ageLabel}" + (singleBelt is not null ? $" · {singleBelt}" : ""),
            MinAge = minAge,
            MaxAge = maxAge,
            CategoryType = discipline,
            Sex = participant.Sex,
            AllowedBeltLinks = belts.Select((value, index) =>
                new CategoryAllowedBelt { Belt = value, SortOrder = index }).ToList()
        };
    }
}
