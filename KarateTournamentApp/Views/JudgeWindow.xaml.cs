using System.Windows;
using System.Windows.Threading;
using KarateTournamentApp.ViewModels;

namespace KarateTournamentApp.Views;

public partial class JudgeWindow : Window
{
    private readonly DispatcherTimer _handoffTimer = new() { Interval = TimeSpan.FromSeconds(2) };
    private readonly CancellationTokenSource _lifetime = new();

    public JudgeWindow()
    {
        InitializeComponent();
        var cancellationToken = _lifetime.Token;
        _handoffTimer.Tick += async (_, _) =>
        {
            if (DataContext is MainViewModel judge)
                await judge.CheckJudgingHandoffsAsync(cancellationToken);
        };
        Loaded += (_, _) => _handoffTimer.Start();
        Closed += (_, _) =>
        {
            _handoffTimer.Stop();
            _lifetime.Cancel();
            _lifetime.Dispose();
        };
    }
}
