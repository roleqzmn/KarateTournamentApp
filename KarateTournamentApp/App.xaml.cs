using System.Configuration;
using System.Data;
using System.Net.Http;
using System.Windows;
using KarateTournamentApp.Services;
using KarateTournamentApp.Services.Api;
using KarateTournamentApp.Services.Dialogs;
using KarateTournamentApp.Services.Navigation;
using KarateTournamentApp.Services.Scheduling;
using KarateTournamentApp.ViewModels;
using KarateTournamentApp.Views;

namespace KarateTournamentApp
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        private HttpClient? _tournamentHttpClient;

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            var categoryManager = new CategoryManager();
            var dialogService = new WpfDialogService();
            var windowService = new WpfWindowService();
            var uiScheduler = new WpfUiScheduler();
            RegisterViews(windowService);

            _tournamentHttpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
            var api = new TournamentApiClient(_tournamentHttpClient,
                Environment.GetEnvironmentVariable("KARATE_TOURNAMENT_API_URL"),
                Environment.GetEnvironmentVariable("KARATE_TOURNAMENT_RECOVERY_KEY"));
            var mainViewModel = new MainViewModel(categoryManager, dialogService, windowService, uiScheduler, api);

            var mainWindow = new MainWindow
            {
                DataContext = mainViewModel
            };

            mainWindow.Show();
        }

        protected override void OnExit(ExitEventArgs e)
        {
            _tournamentHttpClient?.Dispose();
            base.OnExit(e);
        }

        private static void RegisterViews(IWindowService windowService)
        {
            windowService.Register<ScoreboardViewModel, ScoreboardView>();
            windowService.Register<ScoreboardJudgeViewModel, ScoreboardJudge>();
            windowService.Register<IndividualScoreboardViewModel, IndividualScoreboardView>();
            windowService.Register<IndividualJudgeViewModel, IndividualJudgeView>();
            windowService.Register<ResultsViewModel, ResultsView>();
            windowService.Register<DrawResolverViewModel, DrawResolverView>();
            windowService.Register<DrawResolverScoreboardViewModel, DrawResolverScoreboardView>();
            windowService.Register<ApiCategoryEditorViewModel, ApiCategoryEditorView>();
        }
    }

}
