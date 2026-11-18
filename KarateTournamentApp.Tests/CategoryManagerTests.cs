using KarateClassLibrary;
using KarateTournamentApp.Models;
using KarateTournamentApp.Services;
using Xunit;

namespace KarateTournamentApp.Tests;

public class CategoryManagerTests
{
    private static Participant Person(int age = 12, Belts belt = Belts.Kyu8, Sex sex = Sex.Male) =>
        new("Jan", "Kowalski", age, belt, sex, [CategoryType.Kata]);

    [Theory]
    [InlineData(true, true, 2)]
    [InlineData(true, false, 1)]
    [InlineData(false, true, 2)]
    public void AgeDifferenceSplitsCategoriesOnlyWhenAgeDivisionEnabled(
        bool byBelt, bool byAge, int expectedCount)
    {
        var manager = new CategoryManager();
        manager.AssignParticipant(Person(12), byBelt, byAge);
        manager.AssignParticipant(Person(13), byBelt, byAge);
        Assert.Equal(expectedCount, manager.DefinedCategories.Count);
        Assert.Equal(2, manager.DefinedCategories.Sum(category => category.Participants.Count));
    }

    [Theory]
    [InlineData(true, true, 2)]
    [InlineData(true, false, 2)]
    [InlineData(false, true, 1)]
    public void BeltDifferenceSplitsCategoriesOnlyWhenBeltDivisionEnabled(
        bool byBelt, bool byAge, int expectedCount)
    {
        var manager = new CategoryManager();
        manager.AssignParticipant(Person(belt: Belts.Kyu8), byBelt, byAge);
        manager.AssignParticipant(Person(belt: Belts.Kyu7), byBelt, byAge);
        Assert.Equal(expectedCount, manager.DefinedCategories.Count);
        Assert.Equal(2, manager.DefinedCategories.Sum(category => category.Participants.Count));
    }

    [Fact]
    public void MatchingParticipantsShareOneCategory()
    {
        var manager = new CategoryManager();
        var first = Person();
        var second = Person();
        manager.AssignParticipant(first, true, true);
        manager.AssignParticipant(second, true, true);
        Assert.Equal(new[] { first, second }, Assert.Single(manager.DefinedCategories).Participants);
    }

    [Fact]
    public void EighteenthBirthdaySeparatesSeniorFromJunior()
    {
        var manager = new CategoryManager();
        manager.AssignParticipant(Person(17), true, true);
        var adult = Person(18);
        manager.AssignParticipant(adult, true, true);
        Assert.Equal(2, manager.DefinedCategories.Count);
        var senior = Assert.Single(manager.DefinedCategories, category => category.MinAge == 18);
        Assert.Same(adult, Assert.Single(senior.Participants));
    }

    [Fact]
    public void SeniorsShareCategoryRegardlessOfAgeAndBeltDivisionSettings()
    {
        var manager = new CategoryManager();
        manager.AssignParticipant(Person(18, Belts.Kyu8), false, false);
        manager.AssignParticipant(Person(35, Belts.Dan), true, true);
        Assert.Equal(2, Assert.Single(manager.DefinedCategories).Participants.Count);
    }

    [Fact]
    public void EachDisciplineGetsSeparateCategoryAndKumiteUsesShobuRules()
    {
        var manager = new CategoryManager();
        var participant = Person();
        participant.Categories = [CategoryType.Kata, CategoryType.Kumite];
        manager.AssignParticipant(participant, true, true);
        Assert.Equal(2, manager.DefinedCategories.Count);
        var kumite = Assert.Single(manager.DefinedCategories, c => c.CategoryType == CategoryType.Kumite);
        Assert.IsType<ShobuSanbonCategory>(kumite);
        Assert.All(manager.DefinedCategories, category => Assert.Same(participant, Assert.Single(category.Participants)));
    }

    [Fact]
    public void MaleAndFemaleParticipantsDoNotShareSexSpecificCategory()
    {
        var manager = new CategoryManager();
        manager.AssignParticipant(Person(sex: Sex.Male), true, true);
        manager.AssignParticipant(Person(sex: Sex.Female), true, true);
        Assert.Equal(2, manager.DefinedCategories.Count);
        Assert.All(manager.DefinedCategories, category => Assert.Single(category.Participants));
    }

    [Fact]
    public void ExistingUnisexCategoryAcceptsBothSexes()
    {
        var category = new Category(Belts.Kyu8, CategoryType.Kata, Sex.Unisex, 10, 14);
        var manager = new CategoryManager { DefinedCategories = [category] };
        manager.AssignParticipant(Person(sex: Sex.Male), true, true);
        manager.AssignParticipant(Person(sex: Sex.Female), true, true);
        Assert.Same(category, Assert.Single(manager.DefinedCategories));
        Assert.Equal(2, category.Participants.Count);
    }
}
