namespace KarateClassLibrary.Contracts;

public sealed record CompleteTeamCategoryRequest(Guid SessionToken, TeamResultRequest[]? Results);
public sealed record TeamResultRequest(int TeamId, int Rank, decimal[]? JudgeScores);
