using System.ComponentModel.DataAnnotations;
using KarateClassLibrary;

namespace KarateTournament.Api.Contracts;

public sealed record CreateCategoryRequest(
    [property: Required] string? Name,
    [property: Range(0, int.MaxValue)] int? MinAge,
    [property: Range(0, int.MaxValue)] int? MaxAge,
    [property: Required, EnumDataType(typeof(Sex))] Sex? Sex,
    [property: Required, EnumDataType(typeof(CategoryType))] CategoryType? CategoryType,
    [property: Required, MinLength(1)] Belts[]? AllowedBelts,
    [property: Required] int[]? ParticipantIds) : IValidatableObject
{
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (MinAge.HasValue && MaxAge.HasValue && MinAge > MaxAge)
            yield return new ValidationResult(
                "Minimum age must not exceed maximum age.", [nameof(MinAge), nameof(MaxAge)]);

        if (AllowedBelts is not null)
        {
            if (AllowedBelts.Any(belt => !Enum.IsDefined(belt)))
                yield return new ValidationResult(
                    "Allowed belts must contain only supported values.", [nameof(AllowedBelts)]);
            if (AllowedBelts.Distinct().Count() != AllowedBelts.Length)
                yield return new ValidationResult(
                    "Allowed belts must not contain duplicates.", [nameof(AllowedBelts)]);
        }

        if (ParticipantIds is not null)
        {
            if (ParticipantIds.Any(id => id <= 0))
                yield return new ValidationResult(
                    "Participant IDs must be positive.", [nameof(ParticipantIds)]);
            if (ParticipantIds.Distinct().Count() != ParticipantIds.Length)
                yield return new ValidationResult(
                    "Participant IDs must not contain duplicates.", [nameof(ParticipantIds)]);
        }
    }
}
