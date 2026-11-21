namespace KarateClassLibrary;

public enum CategoryGrouping
{
    Age = 1,
    Belt = 2,
    AgeAndBelt = 3
}

public static class ParticipantCategoryRules
{
    public const int SeniorAge = 18;

    public static (int MinAge, int MaxAge, Belts? SingleBelt) GetSettings(
        int age, Belts belt, CategoryGrouping grouping, bool senior)
    {
        if (senior)
            return (SeniorAge, Math.Max(99, age), null);

        return grouping switch
        {
            CategoryGrouping.Age => (age, age, null),
            CategoryGrouping.Belt => (Math.Min(age, 1), SeniorAge - 1, belt),
            CategoryGrouping.AgeAndBelt => (age, age, belt),
            _ => throw new ArgumentOutOfRangeException(nameof(grouping))
        };
    }

    public static bool Matches(
        int age, Sex sex, bool senior, CategoryGrouping grouping,
        int? minAge, int? maxAge, Sex categorySex, bool allowsBelt)
    {
        if (!allowsBelt || (sex != categorySex && categorySex != Sex.Unisex)
            || (minAge is int minimum && age < minimum) || (maxAge is int maximum && age > maximum))
            return false;

        if (senior)
            return minAge >= SeniorAge;

        return grouping == CategoryGrouping.Belt ? maxAge < SeniorAge : minAge.HasValue && maxAge.HasValue;
    }
}
