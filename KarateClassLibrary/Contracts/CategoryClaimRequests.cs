namespace KarateClassLibrary.Contracts;

public sealed record ClaimCategoryRequest(Guid SessionToken, string? StationName, Guid? MatClaimToken = null);
public sealed record ReleaseCategoryRequest(Guid SessionToken);
public sealed record ClaimRecoveryDto(string ClaimReference, string? StationName);
public sealed record RecoverCategoryClaimRequest(string? ClaimReference);
