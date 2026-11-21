namespace KarateClassLibrary.Contracts;

public sealed record JudgingCheckpoint(
    bool IsCompleteLocally, IndividualJudgeDraft? Individual, MatchDto[]? Bracket, int[]? RankedParticipantIds);

public sealed record SaveJudgingHandoffRequest(Guid SessionToken, JudgingCheckpoint? Checkpoint);
public sealed record JudgingHandoffState(bool Requested, bool Released);
public sealed record JudgingCheckpointDto(JudgingCheckpoint? Checkpoint);
