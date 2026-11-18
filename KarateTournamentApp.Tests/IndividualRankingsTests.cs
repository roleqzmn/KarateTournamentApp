using System.Windows;
using KarateClassLibrary;
using KarateTournamentApp.Models;
using KarateTournamentApp.Services.Navigation;
using KarateTournamentApp.Services.Scheduling;
using KarateTournamentApp.ViewModels;
using Xunit;

namespace KarateTournamentApp.Tests;

public class IndividualRankingsTests
{
    private static ParticipantResult Result(int id, decimal score, params decimal[] notes) => new()
    {
        Participant = new Participant($"Zawodnik {id}", "Testowy", 12,
            Belts.Kyu8, Sex.Male, [CategoryType.Kata]) { Id = id },
        Score = score,
        JudgeScores = notes.ToList()
    };

    private static IndividualCompetitionManagerViewModel Manager(Category category, TestWindows? windows = null) =>
        new(category, windows ?? new TestWindows(), new TestScheduler());

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public void CompletedSmallCompetitionPersistsFinalRankings(int count)
    {
        var category = new Category { IsFinished = true };
        var manager = Manager(category);
        for (var id = 1; id <= count; id++) manager.Results.Add(Result(id, 30 - id));

        var ranking = manager.GetFinalRankings();

        Assert.Equal(count, ranking.Count);
        Assert.Equal(ranking.ToArray(), category.FinalResults.ToArray());
    }

    [Fact]
    public void RankingsSortByDescendingScoreAndDoNotFinalizeRunningCompetition()
    {
        var category = new Category();
        var manager = Manager(category);
        manager.Results.Add(Result(1, 21));
        manager.Results.Add(Result(2, 27));
        manager.Results.Add(Result(3, 24));

        var ranking = manager.GetFinalRankings();

        Assert.Equal(new[] { 2, 3, 1 }, ranking.Select(result => result.Participant.Id));
        Assert.Empty(category.FinalResults);
        Assert.False(category.IsFinished);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void EqualTotalsUseHighestThenLowestNoteBeforeRequestingManualDecision(bool sameHighest)
    {
        var manager = Manager(new Category { IsFinished = true });
        // Both pairs total 24, with the second participant winning the existing tie rules.
        manager.Results.Add(sameHighest ? Result(1, 24, 6, 8, 10) : Result(1, 24, 7, 8, 9));
        manager.Results.Add(sameHighest ? Result(2, 24, 7, 7, 10) : Result(2, 24, 6, 8, 10));
        manager.Results.Add(Result(3, 21, 7, 7, 7));

        var ranking = manager.GetFinalRankings();

        Assert.Equal(new[] { 2, 1, 3 }, ranking.Select(result => result.Participant.Id));
    }

    [Fact]
    public void UnresolvedTieUsesConfirmedJudgeDecision()
    {
        var windows = new TestWindows { SelectShiro = true };
        var category = new Category { IsFinished = true };
        var manager = Manager(category, windows);
        manager.Results.Add(Result(1, 24, 7, 8, 9));
        manager.Results.Add(Result(2, 24, 7, 8, 9));
        manager.Results.Add(Result(3, 21, 7, 7, 7));

        var ranking = manager.GetFinalRankings();

        Assert.Equal(1, windows.DialogCount);
        Assert.Equal(new[] { 2, 1, 3 }, ranking.Select(result => result.Participant.Id));
        Assert.Equal(ranking.ToArray(), category.FinalResults.ToArray());
    }

    [Fact]
    public void SavedFinalRankingsAreAuthoritativeAndNotReorderedOrResolvedAgain()
    {
        var winner = Result(2, 24, 7, 8, 9);
        var runnerUp = Result(1, 24, 7, 8, 9);
        var category = new Category { IsFinished = true, FinalResults = [winner, runnerUp] };
        var manager = Manager(category);
        manager.Results.Add(Result(3, 30, 10, 10, 10));

        var ranking = manager.GetFinalRankings();

        Assert.Equal(new[] { winner, runnerUp }, ranking);
        ranking.Clear();
        Assert.Equal(new[] { winner, runnerUp }, category.FinalResults);
    }

    [Fact]
    public void FinishingParticipantsKeepsTheirNotesAndAllowsFinalRankingToBeSaved()
    {
        var first = Result(1, 0).Participant;
        var second = Result(2, 0).Participant;
        var category = new Category { Participants = [first, second] };
        var manager = Manager(category);
        manager.JudgeScores.Add(8m);
        manager.JudgeScores.Add(8m);
        Assert.False(manager.FinishParticipantCommand.CanExecute(null));
        manager.JudgeScores.Add(8m);
        Assert.True(manager.FinishParticipantCommand.CanExecute(null));
        manager.FinishParticipantCommand.Execute(null);
        Assert.Same(second, manager.CurrentParticipant);
        Assert.Empty(manager.JudgeScores);
        Assert.False(category.IsFinished);

        foreach (var note in new[] { 9m, 9m, 9m }) manager.JudgeScores.Add(note);
        manager.FinishParticipantCommand.Execute(null);

        Assert.True(category.IsFinished);
        Assert.Null(manager.CurrentParticipant);
        Assert.False(manager.FinishParticipantCommand.CanExecute(null));
        Assert.Equal(new decimal[] { 8, 8, 8 }, manager.Results[0].JudgeScores);
        Assert.Equal(new decimal[] { 9, 9, 9 }, manager.Results[1].JudgeScores);
        Assert.Equal(new[] { 1, 2 }, category.JudgingScores.Select(result => result.ParticipantId));
        Assert.Equal(new[] { 2, 1 }, manager.GetFinalRankings().Select(result => result.Participant.Id));
        Assert.Equal(2, category.FinalResults.Count);
    }

    private sealed class TestScheduler : IUiScheduler
    {
        // Window lifecycle delays are outside these ranking tests.
        public void Schedule(TimeSpan delay, Action action) { }
    }

    private sealed class TestWindows : IWindowService
    {
        public bool SelectShiro { get; init; }
        public int DialogCount { get; private set; }
        public void Register<TViewModel, TView>() where TView : FrameworkElement, new() =>
            throw new NotSupportedException();
        public IWindowHandle Show(object viewModel, WindowOptions? options = null)
        {
            Assert.True(SelectShiro, "A numeric or saved ranking unexpectedly requested a draw window.");
            return new TestHandle();
        }
        public IWindowHandle ShowOrUpdate(string key, object viewModel, WindowOptions? options = null,
            EventHandler? onClosed = null) => throw new NotSupportedException();
        public bool? ShowDialog(object viewModel, WindowOptions? options = null,
            Action<IWindowHandle>? onOpened = null)
        {
            Assert.True(SelectShiro);
            DialogCount++;
            onOpened?.Invoke(new TestHandle());
            var resolver = Assert.IsType<DrawResolverViewModel>(viewModel);
            resolver.SelectShiroCommand.Execute(null);
            resolver.ConfirmCommand.Execute(null);
            return true;
        }
    }

    private sealed class TestHandle : IWindowHandle
    {
        public event EventHandler? Closed { add { } remove { } }
        public void Close() { }
    }
}
