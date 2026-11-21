using System.Net.Http;
using System.Net.Http.Headers;
using System.Windows;
using KarateTournamentApp.Models;
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

            var role = StationRoleSettings.Parse(Environment.GetEnvironmentVariable("KARATE_TOURNAMENT_ROLE"));
            if (role == StationRole.Local)
            {
                MainWindow = new MainWindow
                {
                    DataContext = new MainViewModel(categoryManager, dialogService, windowService, uiScheduler)
                };
                MainWindow.Show();
                return;
            }

            _tournamentHttpClient = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false })
            {
                Timeout = TimeSpan.FromSeconds(10)
            };
            var accessKey = Environment.GetEnvironmentVariable("KARATE_TOURNAMENT_ACCESS_KEY")?.Trim();
            if (!string.IsNullOrEmpty(accessKey))
                _tournamentHttpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessKey);
            var api = new TournamentApiClient(_tournamentHttpClient,
                Environment.GetEnvironmentVariable("KARATE_TOURNAMENT_API_URL"),
                Environment.GetEnvironmentVariable("KARATE_TOURNAMENT_RECOVERY_KEY"));
            var judgeStation = role == StationRole.Judge;
            MainWindow = judgeStation ? new JudgeWindow() : new OrganizerWindow();
            MainWindow.DataContext = new MainViewModel(categoryManager, dialogService, windowService, uiScheduler, api, judgeStation);
            MainWindow.Show();
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
