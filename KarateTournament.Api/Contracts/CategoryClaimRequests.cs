namespace KarateTournament.Api.Contracts;

public sealed record ClaimCategoryRequest(Guid SessionToken, string? StationName);
public sealed record ReleaseCategoryRequest(Guid SessionToken);
