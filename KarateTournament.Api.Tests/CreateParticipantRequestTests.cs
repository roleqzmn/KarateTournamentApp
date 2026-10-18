using System.ComponentModel.DataAnnotations;
using KarateClassLibrary;
using KarateTournament.Api.Contracts;
using Xunit;

namespace KarateTournament.Api.Tests;

public class CreateParticipantRequestTests
{
    private static CreateParticipantRequest ValidRequest() =>
        new("Jan", "Kowalski", 12, null, Belts.Kyu8, Sex.Male, [CategoryType.Kata]);

    private static List<ValidationResult> Validate(CreateParticipantRequest request)
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(request, new ValidationContext(request), results, true);
        return results;
    }

    [Fact]
    public void AcceptsValidParticipantWithoutClub() => Assert.Empty(Validate(ValidRequest()));

    [Theory]
    [InlineData(null)]
    [InlineData("   ")]
    public void RejectsBlankNames(string? name)
    {
        Assert.Contains(Validate(ValidRequest() with { FirstName = name }),
            error => error.MemberNames.Contains(nameof(CreateParticipantRequest.FirstName)));
        Assert.Contains(Validate(ValidRequest() with { LastName = name }),
            error => error.MemberNames.Contains(nameof(CreateParticipantRequest.LastName)));
    }

    [Theory]
    [InlineData(null)]
    [InlineData(-1)]
    public void RejectsMissingOrNegativeAge(int? age) =>
        Assert.NotEmpty(Validate(ValidRequest() with { Age = age }));

    [Fact]
    public void RejectsMissingBeltAndSex()
    {
        Assert.NotEmpty(Validate(ValidRequest() with { Belt = null }));
        Assert.NotEmpty(Validate(ValidRequest() with { Sex = null }));
    }

    [Fact]
    public void RejectsUnknownEnumValues()
    {
        Assert.NotEmpty(Validate(ValidRequest() with { Belt = (Belts)99 }));
        Assert.NotEmpty(Validate(ValidRequest() with { Sex = (Sex)0 }));
        Assert.NotEmpty(Validate(ValidRequest() with { CategoryTypes = [(CategoryType)99] }));
    }

    [Fact]
    public void RejectsMissingEmptyOrDuplicateCategoryTypes()
    {
        Assert.NotEmpty(Validate(ValidRequest() with { CategoryTypes = null }));
        Assert.NotEmpty(Validate(ValidRequest() with { CategoryTypes = [] }));
        Assert.NotEmpty(Validate(ValidRequest() with { CategoryTypes = [CategoryType.Kata, CategoryType.Kata] }));
    }

    [Fact]
    public void AcceptsZeroValuedEnumsAndMultipleCategoryTypes() =>
        Assert.Empty(Validate(ValidRequest() with
        {
            Belt = Belts.Dan,
            CategoryTypes = [CategoryType.Kata, CategoryType.Kumite]
        }));
}
