namespace KarateClassLibrary.Contracts;

public sealed record JudgingCheckpoint(
    bool IsCompleteLocally, IndividualJudgeDraft? Individual, MatchDto[]? Bracket, int[]? RankedParticipantIds,
    TeamJudgeDraft? Team = null, int[]? RankedTeamIds = null);

public sealed record TeamJudgeDraft(int TeamIndex, decimal[] CurrentScores, TeamDraftScore[] Results)
{
    public static TeamJudgeDraft Empty { get; } = new(0, [], []);
}

public sealed record TeamDraftScore(int TeamId, decimal[] Notes);

public sealed record SaveJudgingHandoffRequest(Guid SessionToken, JudgingCheckpoint? Checkpoint);
public sealed record JudgingHandoffState(bool Requested, bool Released);
public sealed record JudgingCheckpointDto(JudgingCheckpoint? Checkpoint);
