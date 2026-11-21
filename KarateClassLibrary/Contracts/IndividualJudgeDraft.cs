namespace KarateClassLibrary.Contracts;

public sealed record IndividualJudgeDraft(int ParticipantIndex, decimal[] CurrentScores, IndividualDraftScore[] Results)
{
    public static IndividualJudgeDraft Empty { get; } = new(0, [], []);
}

public sealed record IndividualDraftScore(int ParticipantId, decimal[] Notes);
