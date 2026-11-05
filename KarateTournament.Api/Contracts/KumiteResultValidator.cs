namespace KarateTournament.Api.Contracts;

public static class KumiteResultValidator
{
    public static string? Validate(CompleteKumiteCategoryRequest request)
    {
        if (request.SessionToken == Guid.Empty || request.Matches is not { Length: > 0 } matches
            || request.RankedParticipantIds is not { Length: > 0 and <= 99999 } ranking)
            return "Provide a session token, a bracket and a complete ranking (up to 99999 participants).";
        if (matches.Any(match => match is null) || ranking.Any(id => id <= 0)
            || ranking.Distinct().Count() != ranking.Length)
            return "Matches must not be null and ranked participant IDs must be positive and unique.";
        var ordered = matches.OrderBy(match => match.BracketPosition).ToArray();
        // Complete binary tree in heap order: root 0, children 2*i+1 and 2*i+2.
        if ((ordered.Length & (ordered.Length + 1)) != 0
            || !ordered.Select(match => match.BracketPosition).SequenceEqual(Enumerable.Range(0, ordered.Length)))
            return "Bracket positions must form a complete binary tree, numbered consecutively from zero.";
        foreach (var match in ordered)
        {
            if (match.AkaScore < 0 || match.ShiroScore < 0
                || !double.IsFinite(match.TimeRemaining) || match.TimeRemaining < 0
                || match.AtenaiAka < 0 || match.AtenaiShiro < 0 || match.ChukokuAka < 0 || match.ChukokuShiro < 0
                || match.OvertimeCount < 0 || (match.HasSenshuAka && match.HasSenshuShiro))
                return "Scores, penalties and time must be valid; both sides cannot hold senshu.";
            if (match.AkaParticipantId.HasValue && match.AkaParticipantId == match.ShiroParticipantId)
                return "A participant cannot fight themselves.";
            var hasParticipant = match.AkaParticipantId.HasValue || match.ShiroParticipantId.HasValue;
            if (hasParticipant != match.WinnerParticipantId.HasValue
                || (hasParticipant && match.WinnerParticipantId != match.AkaParticipantId
                    && match.WinnerParticipantId != match.ShiroParticipantId))
                return "Every populated match must have a winner who fought in that match.";
            if ((!match.AkaParticipantId.HasValue && (match.AkaScore != 0 || match.AtenaiAka != 0
                    || match.ChukokuAka != 0 || match.HasSenshuAka))
                || (!match.ShiroParticipantId.HasValue && (match.ShiroScore != 0 || match.AtenaiShiro != 0
                    || match.ChukokuShiro != 0 || match.HasSenshuShiro))
                || ((!match.AkaParticipantId.HasValue || !match.ShiroParticipantId.HasValue)
                    && (match.IsDisqualification || match.AkaScore != 0 || match.ShiroScore != 0
                        || match.AtenaiAka != 0 || match.AtenaiShiro != 0 || match.ChukokuAka != 0 || match.ChukokuShiro != 0
                        || match.HasSenshuAka || match.HasSenshuShiro || match.IsInOvertime || match.OvertimeCount != 0)))
                return "Empty sides and byes cannot carry fight results.";
            var left = 2L * match.BracketPosition + 1;
            if (left < ordered.Length && (match.AkaParticipantId != ordered[(int)left].WinnerParticipantId
                || match.ShiroParticipantId != ordered[(int)left + 1].WinnerParticipantId))
                return "A match must contain the winners of its two child matches.";
        }
        var startingIds = ordered.Skip(ordered.Length / 2)
            .SelectMany(match => new[] { match.AkaParticipantId, match.ShiroParticipantId })
            .Where(id => id.HasValue).Select(id => id!.Value).Order();
        if (!startingIds.SequenceEqual(ranking.Order()) || ordered[0].WinnerParticipantId != ranking[0])
            return "Each ranked participant must start exactly once; the final winner must rank first.";
        return null;
    }
}
