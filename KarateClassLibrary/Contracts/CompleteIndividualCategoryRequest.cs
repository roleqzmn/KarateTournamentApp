namespace KarateClassLibrary.Contracts;

public sealed record CompleteIndividualCategoryRequest(Guid SessionToken, IndividualResultRequest[]? Results);
public sealed record IndividualResultRequest(int ParticipantId, int Rank, decimal[]? JudgeScores);
