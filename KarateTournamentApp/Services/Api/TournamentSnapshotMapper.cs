using System.Collections.ObjectModel;
using System.IO;
using KarateClassLibrary.Contracts;
using KarateTournamentApp.Models;

namespace KarateTournamentApp.Services.Api;

internal sealed record ApiCategorySnapshot(Category Category, bool IsClaimed, string? ClaimedBy, bool HasJudgingCheckpoint);

internal static class TournamentSnapshotMapper
{
    public static IReadOnlyList<ApiCategorySnapshot> MapCategories(TournamentSnapshotDto snapshot)
    {
        var participants = snapshot.Participants.ToDictionary(
            dto => dto.Id,
            dto => new Participant(dto.FirstName, dto.LastName, dto.Age, dto.Belt, dto.Sex,
                dto.CategoryTypes.ToList(), dto.Club) { Id = dto.Id });

        Participant FindParticipant(int id) =>
            participants.TryGetValue(id, out var participant)
                ? participant
                : throw new InvalidDataException($"Turniej z API odwołuje się do brakującego zawodnika (ID {id}).");

        return snapshot.Categories.Select(dto =>
        {
            Category category = dto.CategoryType switch
            {
                _ when dto.CategoryType.UsesMatchBracket() => new ShobuSanbonCategory(),
                CategoryType.Team => new TeamCategory(),
                _ => new Category()
            };
            category.Id = dto.Id;
            category.Name = dto.Name;
            category.MinAge = dto.MinAge;
            category.MaxAge = dto.MaxAge;
            category.IsFinished = dto.IsFinished;
            category.Sex = dto.Sex;
            category.CategoryType = dto.CategoryType;
            category.AllowedBelts = dto.AllowedBelts.ToList();
            category.Participants = new ObservableCollection<Participant>(dto.ParticipantIds.Select(FindParticipant));
            category.BracketMatches = dto.BracketMatches.OrderBy(match => match.BracketPosition)
                .Select(match => MapMatch(match, dto.CategoryType)).ToList();
            category.FinalResults = dto.FinalResults.OrderBy(result => result.Rank ?? int.MaxValue)
                .ThenBy(result => result.Id).Select(result => MapResult(result, FindParticipant(result.ParticipantId)))
                .ToList();

            if (category is TeamCategory teamCategory)
            {
                teamCategory.Teams = new ObservableCollection<Team>(dto.Teams.Select(team =>
                    new Team(team.Name, team.Sex)
                    {
                        Id = team.Id,
                        Members = team.MemberParticipantIds.Select(FindParticipant).ToList()
                    }));
                var teamsById = teamCategory.Teams.ToDictionary(team => team.Id);
                teamCategory.TeamResults = (dto.TeamResults ?? []).OrderBy(result => result.Rank)
                    .Select(result => MapScore(new TeamKataResult { Team = teamsById[result.TeamId] },
                        result.Score, result.JudgeScores)).ToList();
            }

            return new ApiCategorySnapshot(category, dto.IsClaimed, dto.ClaimedBy, dto.HasJudgingCheckpoint);
        }).ToArray();
    }

    internal static Match MapMatch(MatchDto dto, CategoryType categoryType)
    {
        Match match = categoryType.UsesMatchBracket()
            ? new ShobuSanbonMatch
            {
                TimeRemaining = dto.TimeRemaining,
                AtenaiAka = dto.AtenaiAka,
                AtenaiShiro = dto.AtenaiShiro,
                ChukokuAka = dto.ChukokuAka,
                ChukokuShiro = dto.ChukokuShiro,
                SenshuEnabled = dto.SenshuEnabled,
                HasSenshuAka = dto.HasSenshuAka,
                HasSenshuShiro = dto.HasSenshuShiro,
                IsInOvertime = dto.IsInOvertime,
                OvertimeCount = dto.OvertimeCount
            }
            : new Match();
        match.Id = dto.Id;
        match.Aka = dto.AkaParticipantId;
        match.Shiro = dto.ShiroParticipantId;
        match.WinnerId = dto.WinnerParticipantId;
        match.AkaScore = dto.AkaScore;
        match.ShiroScore = dto.ShiroScore;
        match.IsFinished = dto.IsFinished;
        match.IsDisqualification = dto.IsDisqualification;
        return match;
    }

    private static ParticipantResult MapResult(ParticipantResultDto dto, Participant participant) =>
        MapScore(new ParticipantResult { Participant = participant }, dto.Score, dto.JudgeScores);

    private static TResult MapScore<TResult>(TResult result, decimal score, IReadOnlyList<JudgeScoreDto> judgeScores)
        where TResult : KataPerformanceResult
    {
        var notes = judgeScores.OrderBy(note => note.Ordinal).ToArray();
        result.Score = score;
        result.JudgeScores = notes.Select(note => note.Score).ToList();
        result.DiscardedJudgeScoreIndexes = notes.Select((note, index) => (Note: note, Index: index))
            .Where(item => item.Note.IsDiscarded).Select(item => item.Index).ToList();
        return result;
    }
}
