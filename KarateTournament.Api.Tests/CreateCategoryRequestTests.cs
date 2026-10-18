using System.ComponentModel.DataAnnotations;
using KarateClassLibrary;
using KarateTournament.Api.Contracts;
using Xunit;

namespace KarateTournament.Api.Tests;

public class CreateCategoryRequestTests
{
    private static CreateCategoryRequest ValidRequest() =>
        new("Kata 10-12", 10, 12, Sex.Unisex, CategoryType.Kata, [Belts.Kyu8], [1, 2]);

    private static List<ValidationResult> Validate(CreateCategoryRequest request)
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(request, new ValidationContext(request), results, true);
        return results;
    }

    [Fact]
    public void AcceptsValidCategory() => Assert.Empty(Validate(ValidRequest()));

    [Fact]
    public void AcceptsEmptyCategoryAndOptionalAgeBounds() =>
        Assert.Empty(Validate(ValidRequest() with { ParticipantIds = [], MinAge = null, MaxAge = null }));

    [Theory]
    [InlineData(null)]
    [InlineData("   ")]
    public void RejectsBlankName(string? name) =>
        Assert.Contains(Validate(ValidRequest() with { Name = name }),
            error => error.MemberNames.Contains(nameof(CreateCategoryRequest.Name)));

    [Fact]
    public void RejectsInvalidAgeBounds()
    {
        Assert.NotEmpty(Validate(ValidRequest() with { MinAge = -1 }));
        Assert.NotEmpty(Validate(ValidRequest() with { MaxAge = -1 }));
        var errors = Validate(ValidRequest() with { MinAge = 13, MaxAge = 12 });
        Assert.Contains(errors, error => error.MemberNames.Contains(nameof(CreateCategoryRequest.MinAge)));
        Assert.Contains(errors, error => error.MemberNames.Contains(nameof(CreateCategoryRequest.MaxAge)));
    }

    [Fact]
    public void AcceptsEqualAgeBounds() =>
        Assert.Empty(Validate(ValidRequest() with { MinAge = 12, MaxAge = 12 }));

    [Fact]
    public void RejectsMissingOrUnknownEnums()
    {
        Assert.NotEmpty(Validate(ValidRequest() with { Sex = null }));
        Assert.NotEmpty(Validate(ValidRequest() with { CategoryType = null }));
        Assert.NotEmpty(Validate(ValidRequest() with { Sex = (Sex)0 }));
        Assert.NotEmpty(Validate(ValidRequest() with { CategoryType = (CategoryType)99 }));
    }

    [Fact]
    public void RejectsMissingEmptyUnknownOrDuplicateBelts()
    {
        Assert.NotEmpty(Validate(ValidRequest() with { AllowedBelts = null }));
        Assert.NotEmpty(Validate(ValidRequest() with { AllowedBelts = [] }));
        Assert.NotEmpty(Validate(ValidRequest() with { AllowedBelts = [(Belts)99] }));
        Assert.NotEmpty(Validate(ValidRequest() with { AllowedBelts = [Belts.Dan, Belts.Dan] }));
    }

    [Fact]
    public void RejectsMissingNonPositiveOrDuplicateParticipantIds()
    {
        Assert.NotEmpty(Validate(ValidRequest() with { ParticipantIds = null }));
        Assert.NotEmpty(Validate(ValidRequest() with { ParticipantIds = [0] }));
        Assert.NotEmpty(Validate(ValidRequest() with { ParticipantIds = [-1] }));
        Assert.NotEmpty(Validate(ValidRequest() with { ParticipantIds = [1, 1] }));
    }
}
