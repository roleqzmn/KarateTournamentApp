using KarateTournamentApp.Commands;
using Xunit;

namespace KarateTournamentApp.Tests;

public class AsyncRelayCommandTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RunningOperationRejectsAnotherExecutionAndUnlocksAfterCompletion(bool generic)
    {
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var calls = 0;
        Task Operation() { calls++; return completion.Task; }
        var plain = new AsyncRelayCommand(Operation, error => Assert.Fail(error.ToString()));
        var typed = new AsyncRelayCommand<int>(_ => Operation(), error => Assert.Fail(error.ToString()));
        Task Execute() => generic ? typed.ExecuteAsync(42) : plain.ExecuteAsync();
        bool CanExecute() => generic ? typed.CanExecute(42) : plain.CanExecute(null);

        var firstExecution = Execute();
        try
        {
            Assert.False(CanExecute());
            await Execute();
            Assert.Equal(1, calls);
        }
        finally
        {
            completion.TrySetResult();
            await firstExecution.WaitAsync(TimeSpan.FromSeconds(5));
        }
        Assert.True(CanExecute());
        await Execute();
        Assert.Equal(2, calls);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DisabledCommandDoesNotInvokeOperation(bool generic)
    {
        var calls = 0;
        Task Operation() { calls++; return Task.CompletedTask; }
        var plain = new AsyncRelayCommand(Operation, _ => { }, () => false);
        var typed = new AsyncRelayCommand<int>(_ => Operation(), _ => { }, _ => false);
        if (generic) await typed.ExecuteAsync(42);
        else await plain.ExecuteAsync();
        Assert.Equal(0, calls);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AwaitedExecutionPropagatesExceptionAndUnlocksCommand(bool generic)
    {
        var expected = new InvalidOperationException("Save failed");
        var handledErrors = new List<Exception>();
        var plain = new AsyncRelayCommand(() => Task.FromException(expected), handledErrors.Add);
        var typed = new AsyncRelayCommand<int>(_ => Task.FromException(expected), handledErrors.Add);
        var actual = await Assert.ThrowsAsync<InvalidOperationException>(
            () => generic ? typed.ExecuteAsync(42) : plain.ExecuteAsync());
        Assert.Same(expected, actual);
        Assert.Empty(handledErrors);
        Assert.True(generic ? typed.CanExecute(42) : plain.CanExecute(null));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ICommandExecutionReportsAsyncFailureToErrorHandler(bool generic)
    {
        var operation = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var reported = new TaskCompletionSource<Exception>(TaskCreationOptions.RunContinuationsAsynchronously);
        var expected = new InvalidOperationException("Save failed");
        var plain = new AsyncRelayCommand(() => operation.Task, error => reported.TrySetResult(error));
        var typed = new AsyncRelayCommand<int>(_ => operation.Task, error => reported.TrySetResult(error));
        if (generic) typed.Execute(42);
        else plain.Execute(null);
        operation.SetException(expected);
        Assert.Same(expected, await reported.Task.WaitAsync(TimeSpan.FromSeconds(5)));
        Assert.True(generic ? typed.CanExecute(42) : plain.CanExecute(null));
    }

}
