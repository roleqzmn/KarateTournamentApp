using System.ComponentModel.DataAnnotations;
using KarateClassLibrary;

namespace KarateTournament.Api.Contracts;

public sealed record CreateParticipantRequest(
    [property: Required] string? FirstName,
    [property: Required] string? LastName,
    [property: Required, Range(0, int.MaxValue)] int? Age,
    string? Club,
    [property: Required, EnumDataType(typeof(Belts))] Belts? Belt,
    [property: Required, EnumDataType(typeof(Sex))] Sex? Sex,
    [property: Required, MinLength(1)] CategoryType[]? CategoryTypes) : IValidatableObject
{
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (CategoryTypes is not null)
        {
            if (CategoryTypes.Any(type => !Enum.IsDefined(type)))
                yield return new ValidationResult(
                    "Category types must contain only supported values.", [nameof(CategoryTypes)]);

            if (CategoryTypes.Distinct().Count() != CategoryTypes.Length)
                yield return new ValidationResult(
                    "Category types must not contain duplicates.", [nameof(CategoryTypes)]);
        }
    }
}
