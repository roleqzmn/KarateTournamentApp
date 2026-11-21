using System.Windows;
using KarateTournamentApp.ViewModels;

namespace KarateTournamentApp.Views;

public partial class OrganizerWindow : Window
{
    public OrganizerWindow()
    {
        InitializeComponent();
        Loaded += OnFirstLoad;
    }

    private void OnFirstLoad(object sender, RoutedEventArgs e)
    {
        Loaded -= OnFirstLoad;
        if (DataContext is MainViewModel viewModel && viewModel.LoadApiTournamentCommand.CanExecute(null))
            viewModel.LoadApiTournamentCommand.Execute(null);
    }
}
