using System.IO;
using KarateClassLibrary;
using KarateTournamentApp.Models;
using KarateTournamentApp.Services;
using Xunit;

namespace KarateTournamentApp.Tests;

public class CompetitionResumeTests : IDisposable
{
    private readonly string _file = Path.Combine(Path.GetTempPath(), $"karate-resume-{Guid.NewGuid():N}.json");
    private readonly JsonService _json = new();

    public void Dispose() => File.Delete(_file);

    private static Category CreateCategory(bool shobu, int count)
    {
        Category category = shobu ? new ShobuSanbonCategory() : new Category();
        category.CategoryType = shobu ? CategoryType.Kumite : CategoryType.Kata;
        for (var id = 1; id <= count; id++)
            category.Participants.Add(new Participant(
                $"Zawodnik {id}", "Testowy", 12, Belts.Kyu8, Sex.Male, [category.CategoryType]) { Id = id });
        category.InitializeBracket();
        return category;
    }

    private static void PlayNextMatch(Category category)
    {
        var index = category.BracketMatches.FindLastIndex(match =>
            !match.IsFinished && match.Aka.HasValue && match.Shiro.HasValue);
        Assert.True(index >= 0, "No playable match remains before the final.");
        var match = category.BracketMatches[index];
        match.WinnerId = Math.Min(match.Aka!.Value, match.Shiro!.Value);
        match.AkaScore = (short)(match.WinnerId == match.Aka ? 3 : 1);
        match.ShiroScore = (short)(match.WinnerId == match.Shiro ? 3 : 1);
        match.IsFinished = true;
        category.PromoteWinner(index);
    }

    private static int FinishCompetition(Category category)
    {
        var played = 0;
        while (!category.IsFinished && played < category.Participants.Count)
        {
            PlayNextMatch(category);
            played++;
        }
        Assert.True(category.IsFinished);
        return played;
    }

    private static void AssertSameBracket(Category expected, Category actual)
    {
        Assert.Equal(expected.IsFinished, actual.IsFinished);
        Assert.Equal(expected.Participants.Select(person => person.Id), actual.Participants.Select(person => person.Id));
        Assert.Equal(expected.BracketMatches.Count, actual.BracketMatches.Count);
        for (var index = 0; index < expected.BracketMatches.Count; index++)
        {
            var before = expected.BracketMatches[index];
            var after = actual.BracketMatches[index];
            Assert.Equal((before.Aka, before.Shiro, before.WinnerId, before.IsFinished, before.AkaScore, before.ShiroScore),
                (after.Aka, after.Shiro, after.WinnerId, after.IsFinished, after.AkaScore, after.ShiroScore));
        }
    }

    [Theory]
    [InlineData(false, 5)]
    [InlineData(true, 5)]
    [InlineData(true, 8)]
    public async Task ResumedCompetitionHasSameFinalAsUninterruptedCompetition(bool shobu, int count)
    {
        var original = CreateCategory(shobu, count);
        PlayNextMatch(original);
        PlayNextMatch(original);
        Assert.False(original.IsFinished);

        // Persist the timer and penalties of the next match as well as completed matches.
        var nextIndex = original.BracketMatches.FindLastIndex(match =>
            !match.IsFinished && match.Aka.HasValue && match.Shiro.HasValue);
        Assert.True(nextIndex >= 0);
        if (shobu)
        {
            var next = Assert.IsType<ShobuSanbonMatch>(original.BracketMatches[nextIndex]);
            next.TimeRemaining = 73.5;
            next.AtenaiAka = 1;
            next.ChukokuShiro = 2;
            next.IsRunning = false;
        }

        await _json.SaveTournamentDataAsync(_file, [original]);
        var resumed = Assert.Single(await _json.LoadTournamentDataAsync(_file));
        Assert.NotSame(original, resumed);
        Assert.Equal(original.GetType(), resumed.GetType());
        AssertSameBracket(original, resumed);
        if (shobu)
        {
            var next = Assert.IsType<ShobuSanbonMatch>(resumed.BracketMatches[nextIndex]);
            Assert.Equal(73.5, next.TimeRemaining);
            Assert.Equal(1, next.AtenaiAka);
            Assert.Equal(2, next.ChukokuShiro);
            Assert.False(next.IsRunning);
        }

        var originalRemaining = FinishCompetition(original);
        var resumedRemaining = FinishCompetition(resumed);
        Assert.Equal(count - 1, 2 + resumedRemaining);
        Assert.Equal(originalRemaining, resumedRemaining);
        AssertSameBracket(original, resumed);
        Assert.Equal(1, resumed.BracketMatches[0].WinnerId);
    }

    [Fact]
    public async Task LoadingCompletedCompetitionKeepsFinalWinnerAndFinishedState()
    {
        var original = CreateCategory(true, 5);
        Assert.Equal(4, FinishCompetition(original));

        await _json.SaveTournamentDataAsync(_file, [original]);
        var loaded = Assert.Single(await _json.LoadTournamentDataAsync(_file));

        AssertSameBracket(original, loaded);
        Assert.Equal(1, loaded.BracketMatches[0].WinnerId);
        Assert.Equal(0, FinishCompetition(loaded));
    }
}
