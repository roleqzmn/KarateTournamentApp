using KarateClassLibrary;
using KarateTournamentApp.Services;
using Xunit;

namespace KarateTournamentApp.Tests;

public class ParticipantImportParserTests
{
    [Fact]
    public void ParsesCaseInsensitiveEnumNamesAndPreservesClub()
    {
        var participant = ParticipantImportParser.TryCreateParticipant(
            "Jan", "Kowalski", "12", "kyu8", "male", ["kata", "KUMITE"], "Klub");
        Assert.NotNull(participant);
        Assert.Equal("Jan", participant.FirstName);
        Assert.Equal("Kowalski", participant.LastName);
        Assert.Equal(12, participant.Age);
        Assert.Equal(Belts.Kyu8, participant.Belt);
        Assert.Equal(Sex.Male, participant.Sex);
        Assert.Equal(new[] { CategoryType.Kata, CategoryType.Kumite }, participant.Categories);
        Assert.Equal("Klub", participant.Club);
    }

    [Theory]
    [InlineData(null, "Kowalski", "12", "Kyu8", "Male")]
    [InlineData(" ", "Kowalski", "12", "Kyu8", "Male")]
    public void RejectsMissingRequiredValues(
        string? firstName, string? lastName, string? age, string? belt, string? sex) =>
        Assert.Null(ParticipantImportParser.TryCreateParticipant(
            firstName, lastName, age, belt, sex, ["Kata"], null));

    [Theory]
    [InlineData("0")]
    [InlineData("12.5")]
    [InlineData("2147483648")]
    public void RejectsNonPositiveNonIntegerOrOverflowingAge(string age) =>
        Assert.Null(ParticipantImportParser.TryCreateParticipant(
            "Jan", "Kowalski", age, "Kyu8", "Male", ["Kata"], null));

    [Theory]
    [InlineData("99", "Male")]
    [InlineData("Kyu8", "0")]
    [InlineData("Kyu8", "99")]
    public void RejectsUndefinedBeltOrSex(string belt, string sex) =>
        Assert.Null(ParticipantImportParser.TryCreateParticipant(
            "Jan", "Kowalski", "12", belt, sex, ["Kata"], null));

    [Fact]
    public void IgnoresInvalidDisciplinesWhenAtLeastOneValidDisciplineRemains()
    {
        var participant = ParticipantImportParser.TryCreateParticipant(
            "Jan", "Kowalski", "12", "Kyu8", "Male", [null, "unknown", "99", "Kata"], null);
        Assert.NotNull(participant);
        Assert.Equal(new[] { CategoryType.Kata }, participant.Categories);
    }

    [Fact]
    public void RejectsParticipantWithoutAnySupportedDiscipline() =>
        Assert.Null(ParticipantImportParser.TryCreateParticipant(
            "Jan", "Kowalski", "12", "Kyu8", "Male", ["99"], null));

    [Fact]
    public void AcceptsDefinedNumericEnumsIncludingZeroValuedDisciplineAndBelt()
    {
        var participant = ParticipantImportParser.TryCreateParticipant(
            "Jan", "Kowalski", "12", "0", "1", ["0"], null);
        Assert.NotNull(participant);
        Assert.Equal(Belts.Dan, participant.Belt);
        Assert.Equal(CategoryType.Kata, Assert.Single(participant.Categories));
    }
}
