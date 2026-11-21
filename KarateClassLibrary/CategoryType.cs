namespace KarateClassLibrary;

public enum CategoryType
{
    Kata = 0,
    Kumite = 1,
    Kihon = 2,
    KobudoShort = 3,
    KobudoLong = 4,
    Grappling = 5,
    Team = 6
}

public static class CategoryTypeExtensions
{
    public static bool UsesMatchBracket(this CategoryType discipline) =>
        discipline is CategoryType.Kumite or CategoryType.Grappling;
}
