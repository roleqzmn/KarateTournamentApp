using System.Windows;
using System.Windows.Threading;
using KarateTournamentApp.ViewModels;

namespace KarateTournamentApp.Views;

public partial class OrganizerWindow : Window
{
    private readonly DispatcherTimer _categoryRefreshTimer = new() { Interval = TimeSpan.FromSeconds(10) };
    private readonly CancellationTokenSource _refreshLifetime = new();

    public OrganizerWindow()
    {
        InitializeComponent();
        Loaded += OnFirstLoad;
        Closed += (_, _) =>
        {
            _categoryRefreshTimer.Stop();
            _refreshLifetime.Cancel();
            _refreshLifetime.Dispose();
        };
    }

    private void OnFirstLoad(object sender, RoutedEventArgs e)
    {
        Loaded -= OnFirstLoad;
        if (DataContext is not MainViewModel viewModel)
            return;
        if (viewModel.LoadApiTournamentCommand.CanExecute(null))
            viewModel.LoadApiTournamentCommand.Execute(null);

        var cancellationToken = _refreshLifetime.Token;
        _categoryRefreshTimer.Tick += async (_, _) =>
            await viewModel.RefreshOrganizerCategoriesAsync(cancellationToken: cancellationToken);
        _categoryRefreshTimer.Start();
    }
}
