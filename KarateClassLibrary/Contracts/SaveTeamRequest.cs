using System.ComponentModel.DataAnnotations;

namespace KarateClassLibrary.Contracts;

public sealed record SaveTeamRequest(
    [property: Required] string? Name,
    [property: Required, EnumDataType(typeof(Sex))] Sex? Sex,
    [property: Required, MinLength(1)] int[]? MemberParticipantIds) : IValidatableObject
{
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (MemberParticipantIds is not { } memberIds)
            yield break;

        if (memberIds.Any(id => id <= 0))
            yield return new ValidationResult("Member participant IDs must be positive.", [nameof(MemberParticipantIds)]);
        if (memberIds.Distinct().Count() != memberIds.Length)
            yield return new ValidationResult("A participant can only appear once in a team.", [nameof(MemberParticipantIds)]);
    }
}
