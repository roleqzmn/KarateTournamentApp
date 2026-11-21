using System.IO;
using KarateClassLibrary.Contracts;
using KarateTournamentApp.Models;

namespace KarateTournamentApp.Services.Api;

internal static class JudgingCheckpointMapper
{
    public static JudgingCheckpoint Capture(JudgingSession session)
    {
        var category = session.Category ?? throw new InvalidDataException("Brak lokalnego stanu konkurencji.");
        var ranking = category.FinalResults.Count == 0 ? null
            : category.FinalResults.Select(result => result.Participant.Id).ToArray();
        if (category.CategoryType != CategoryType.Kumite)
            return new(category.IsFinished, session.Draft ?? IndividualJudgeDraft.Empty, null, ranking);

        var matches = category.BracketMatches.Select((entry, index) =>
        {
            if (entry is not ShobuSanbonMatch match)
                throw new InvalidDataException("Lokalna drabinka zawiera nieobsługiwaną walkę.");
            return new MatchDto(match.Id, index, match.Aka, match.Shiro, match.WinnerId,
                match.AkaScore, match.ShiroScore, match.IsFinished, match.IsDisqualification,
                match.TimeRemaining, match.AtenaiAka, match.AtenaiShiro, match.ChukokuAka, match.ChukokuShiro,
                false, match.SenshuEnabled, match.HasSenshuAka, match.HasSenshuShiro, match.IsInOvertime, match.OvertimeCount);
        }).ToArray();
        return new(category.IsFinished, null, matches, ranking);
    }

    public static void Restore(JudgingSession session, JudgingCheckpoint checkpoint)
    {
        var category = session.Category ?? throw new InvalidDataException("Brak składu konkurencji.");
        category.IsFinished = checkpoint.IsCompleteLocally;
        session.Draft = checkpoint.Individual;
        if (checkpoint.Bracket is { } matches)
            category.BracketMatches = matches.Select(match => TournamentSnapshotMapper.MapMatch(match, category.CategoryType)).ToList();

        var notesByPerson = checkpoint.Individual?.Results.ToDictionary(result => result.ParticipantId, result => result.Notes);
        var people = category.Participants.ToDictionary(person => person.Id);
        category.FinalResults = (checkpoint.RankedParticipantIds ?? []).Select(id =>
        {
            var notes = notesByPerson?.GetValueOrDefault(id) ?? [];
            var (score, discarded) = notes.Length == 0
                ? (0m, new List<int>()) : JudgingScoreCalculator.CalculateFinalScore(notes);
            return new ParticipantResult
            {
                Participant = people[id], Score = score, JudgeScores = notes.ToList(), DiscardedJudgeScoreIndexes = discarded
            };
        }).ToList();
    }
}
