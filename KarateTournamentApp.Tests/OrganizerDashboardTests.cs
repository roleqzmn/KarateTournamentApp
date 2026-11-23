using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using KarateClassLibrary;
using KarateClassLibrary.Contracts;
using KarateTournamentApp.Commands;
using KarateTournamentApp.Services;
using KarateTournamentApp.Services.Api;
using KarateTournamentApp.Services.Dialogs;
using KarateTournamentApp.Services.Navigation;
using KarateTournamentApp.Services.Scheduling;
using KarateTournamentApp.ViewModels;
using OfficeOpenXml;
using Xunit;

namespace KarateTournamentApp.Tests;

public sealed class OrganizerDashboardTests
{
    private static readonly ParticipantDto Participant = new(
        1, "Testowy", "Zawodnik", 12, null, Belts.Kyu8, Sex.Male, [CategoryType.Kata]);

    private static readonly CategoryDto Category = new(
        1, "Kata", null, null, false, Sex.Unisex, CategoryType.Kata, [Belts.Kyu8],
        [Participant.Id], [], [], [], false, null);

    private static readonly CategorySummaryDto Summary = new(
        Category.Id, Category.Name, CategoryType.Kata, Sex.Unisex, null, null, false, 1, 0, false, null);

    [Fact]
    public async Task RefreshesStatusesWithoutTouchingRegistrationAndRetainsLastListOnError()
    {
        var responses = new TestApi();
        using var http = new HttpClient(responses);
        var dashboard = await LoadAsync(http, responses);
        dashboard.SelectedOrganizerCategory = Assert.Single(dashboard.OrganizerCategories);
        dashboard.SelectedApiParticipant = Assert.Single(dashboard.ApiParticipants);
        dashboard.FirstName = "Niezapisany wpis";
        var available = Assert.IsType<OrganizerCompetitionRow>(dashboard.SelectedOrganizerCategory);
        Assert.Equal(OrganizerCompetitionStatus.Available, available.Status);
        Assert.True(dashboard.EditApiCategoryCommand.CanExecute(available));

        responses.EnqueueJson(new[] { Summary with { IsClaimed = true, ClaimedBy = "Tatami 1" } });
        await dashboard.RefreshOrganizerCategoriesAsync();
        var claimed = Assert.IsType<OrganizerCompetitionRow>(dashboard.SelectedOrganizerCategory);
        Assert.Equal(OrganizerCompetitionStatus.InProgress, claimed.Status);
        Assert.Equal("Tatami 1", claimed.StationName);
        Assert.False(dashboard.EditApiCategoryCommand.CanExecute(claimed));
        Assert.Same(dashboard.ApiParticipants[0], dashboard.SelectedApiParticipant);
        Assert.Equal("Niezapisany wpis", dashboard.FirstName);
        dashboard.OrganizerStatusFilter = OrganizerCompetitionStatus.InProgress;
        Assert.Same(claimed, Assert.Single(dashboard.OrganizerCategories));

        var lastRefresh = dashboard.OrganizerCategoryRefreshStatus;
        responses.Enqueue(HttpStatusCode.ServiceUnavailable);
        await dashboard.RefreshOrganizerCategoriesAsync();
        Assert.Same(claimed, Assert.Single(dashboard.OrganizerCategories));
        Assert.Equal($"Odświeżenie nie powiodło się · {lastRefresh}", dashboard.OrganizerCategoryRefreshStatus);

        responses.EnqueueJson(new[] { Summary with { IsFinished = true } });
        await dashboard.RefreshOrganizerCategoriesAsync();
        Assert.Empty(dashboard.OrganizerCategories);
        Assert.Null(dashboard.SelectedOrganizerCategory);
        dashboard.OrganizerStatusFilter = OrganizerCompetitionStatus.Finished;
        var finished = Assert.Single(dashboard.OrganizerCategories);
        Assert.Equal(OrganizerCompetitionStatus.Finished, finished.Status);
        Assert.True(dashboard.ViewApiCategoryResultsCommand.CanExecute(finished));
        Assert.StartsWith("Ostatnia aktualizacja", dashboard.OrganizerCategoryRefreshStatus);
        Assert.Equal(new[] { "/api/tournament", "/api/categories", "/api/categories", "/api/categories" },
            responses.Requests);
    }

    [Fact]
    public async Task OlderPollCannotOverwriteRefreshAfterConflict()
    {
        var responses = new TestApi();
        using var http = new HttpClient(responses);
        var dashboard = await LoadAsync(http, responses);
        var olderResponse = responses.EnqueuePending();
        var olderPoll = dashboard.RefreshOrganizerCategoriesAsync();
        responses.EnqueueJson(new[] { Summary with { IsClaimed = true, ClaimedBy = "Tatami 1" } });
        await dashboard.RefreshOrganizerCategoriesAsync(force: true);

        olderResponse.SetResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(new[] { Summary }) });
        await olderPoll;

        Assert.Equal(OrganizerCompetitionStatus.InProgress, Assert.Single(dashboard.OrganizerCategories).Status);
    }

    [Theory]
    [InlineData(CategoryType.Kata)]
    [InlineData(CategoryType.Grappling)]
    public async Task JudgeCanOpenCompetitionButCannotUseOrganizerCommands(CategoryType discipline)
    {
        var responses = new TestApi();
        using var http = new HttpClient(responses);
        var judge = await LoadAsync(http, responses, judge: true, snapshot:
            new TournamentSnapshotDto([Participant with { CategoryTypes = [discipline] }], [Category with { CategoryType = discipline }]));
        var competition = Assert.Single(judge.Categories);
        judge.FirstName = Participant.FirstName;
        judge.LastName = Participant.LastName;
        judge.Age = Participant.Age;
        judge.SelectedSex = Participant.Sex;
        judge.SelectedBelt = Participant.Belt;
        judge.CategorySelections.Single(item => item.CategoryType == CategoryType.Kata).IsSelected = true;

        Assert.True(judge.StartApiJudgingCommand.CanExecute(competition));
        Assert.False(judge.AddParticipantCommand.CanExecute(null));
        Assert.False(judge.ImportExcelCommand.CanExecute(null));
        Assert.False(judge.CreateExcelTemplateCommand.CanExecute(null));
        Assert.False(judge.CreateApiCategoryCommand.CanExecute(null));
        Assert.False(judge.ViewApiParticipantResultsCommand.CanExecute(null));
        Assert.Empty(judge.ApiParticipants);
    }

    [Fact]
    public async Task ShowsFreshResultHistoryForTheSelectedParticipant()
    {
        var responses = new TestApi();
        using var http = new HttpClient(responses);
        var windows = new HistoryWindow();
        var namesake = Participant with { Id = 42 };
        var organizer = await LoadAsync(http, responses, snapshot: new([Participant, namesake], [Category]), windows: windows);
        var command = Assert.IsType<AsyncRelayCommand>(organizer.ViewApiParticipantResultsCommand);
        Assert.False(command.CanExecute(null));
        organizer.SelectedApiParticipant = namesake;

        responses.EnqueueJson(Array.Empty<ParticipantResultHistoryEntryDto>());
        await command.ExecuteAsync();
        Assert.False(windows.History!.HasResults);

        responses.EnqueueJson(new[] { new ParticipantResultHistoryEntryDto(2, "Drużyny", CategoryType.Team, 1, 25m, 4, "Smoki") });
        await command.ExecuteAsync();
        Assert.Equal(new ParticipantResultHistoryViewModel.Row("Drużyny", "Team Kata", 1, 25m, "Smoki"),
            Assert.Single(windows.History!.Rows));
        Assert.Equal(new[] { "/api/tournament", "/api/participants/42/results", "/api/participants/42/results" }, responses.Requests);
    }

    [Fact]
    public async Task ScheduleEditorSavesReorderedSlotsWithVenueLocalTimes()
    {
        var responses = new TestApi();
        using var http = new HttpClient(responses);
        var api = new TournamentApiClient(http, "https://schedule.test");
        var other = Summary with { Id = 2, Name = "Team Kata", CategoryType = CategoryType.Team };
        var editor = new ApiScheduleEditorViewModel(new(7, []), [Summary, other], api, new WpfDialogService());
        editor.MatCountText = "2";
        editor.ApplyMatCountCommand.Execute(null);
        editor.SelectedCategory = Summary;
        editor.AddSlotCommand.Execute(null);
        editor.SelectedSlot!.StartsAt = "2026-11-23 10:30";
        editor.SelectedCategory = other;
        editor.AddSlotCommand.Execute(null);
        editor.MoveUpCommand.Execute(null);
        responses.Enqueue(HttpStatusCode.NoContent);
        responses.EnqueueJson(new TournamentScheduleDto(8, []));

        await Assert.IsType<AsyncRelayCommand>(editor.SaveCommand).ExecuteAsync();

        var request = JsonSerializer.Deserialize<SaveTournamentScheduleRequest>(responses.Bodies[0]!, JsonSerializerOptions.Web)!;
        Assert.Equal(7, request.Version);
        Assert.Equal(2, request.MatCount);
        Assert.Equal(new[] { other.Id, Summary.Id }, request.Slots!.Select(slot => slot!.CategoryId));
        Assert.Equal(new DateTime(2026, 11, 23, 10, 30, 0), request.Slots![1]!.StartsAt);
        Assert.Equal(DateTimeKind.Unspecified, request.Slots[1]!.StartsAt!.Value.Kind);
    }

    [Fact]
    public void PrintScheduleUsesMatOrderAndEscapesCompetitionNames()
    {
        var date = new DateTime(2026, 11, 23, 9, 30, 0);
        var html = SchedulePrintExport.CreateHtml(new TournamentScheduleDto(1,
            [new(1, Guid.NewGuid(), null,
                [new(1, "Kata <A> & B", CategoryType.Kata, false, null, date, null),
                 new(2, "Drużyny", CategoryType.Team, false, null, null, null)])]));

        Assert.Contains("Mata 1", html);
        Assert.Contains("<td>1</td><td>Kata &lt;A&gt; &amp; B</td>", html);
        Assert.Contains("<td>2</td><td>Drużyny</td><td>Team Kata</td>", html);
        Assert.Contains("23.11.2026 09:30", html);
        Assert.DoesNotContain("<A>", html);
    }

    [Fact]
    public async Task JudgeRestoresLocalMatClaimAndStartsTheFirstUnfinishedSlot()
    {
        var responses = new TestApi();
        using var http = new HttpClient(responses);
        var folder = Path.Combine(Path.GetTempPath(), $"karate-schedule-{Guid.NewGuid():N}");
        var store = new JudgingSessionStore("schedule-test", folder);
        var windows = new QueueWindows();
        var dialogs = new ImportDialogs("", "Tatami A");
        var done = Category with { Id = 4, IsFinished = true };
        var snapshot = new TournamentSnapshotDto([Participant], [done, Category]);
        var free = new ScheduleMatDto(1, Guid.NewGuid(), null,
            [new(done.Id, done.Name, CategoryType.Kata, true, null, null, null),
             new(Category.Id, Category.Name, CategoryType.Kata, false, null, null, null)]);
        var taken = free with { Revision = Guid.NewGuid(), ClaimedBy = "Tatami A" };

        MainViewModel? judge = null;
        try
        {
            judge = await LoadAsync(http, responses, true, dialogs, store, snapshot, windows);
            responses.EnqueueJson(snapshot);
            responses.EnqueueJson(new TournamentScheduleDto(1, [free]));
            await Assert.IsType<AsyncRelayCommand>(judge.RefreshJudgeScheduleCommand).ExecuteAsync();
            Assert.Empty(judge.UnscheduledJudgeCategories);
            Assert.False(judge.StartApiJudgingCommand.CanExecute(judge.Categories.Single(item => item.Category.Id == Category.Id)));

            responses.Enqueue(HttpStatusCode.NoContent);
            responses.EnqueueJson(new TournamentScheduleDto(1, [taken]));
            await Assert.IsType<AsyncRelayCommand>(judge.ClaimJudgeMatCommand).ExecuteAsync();
            Assert.Throws<IOException>(() => store.LockMat(1));
            var reserved = store.LoadMat(1)!.Request;
            judge.CloseJudgeSchedule();

            judge = await LoadAsync(http, responses, true, dialogs, store, snapshot, windows);
            responses.EnqueueJson(snapshot);
            responses.EnqueueJson(new TournamentScheduleDto(1, [taken]));
            await Assert.IsType<AsyncRelayCommand>(judge.RefreshJudgeScheduleCommand).ExecuteAsync();
            responses.Enqueue(HttpStatusCode.NoContent);
            responses.EnqueueJson(new TournamentScheduleDto(1, [taken]));
            await Assert.IsType<AsyncRelayCommand>(judge.ClaimJudgeMatCommand).ExecuteAsync();
            Assert.Equal(reserved, store.LoadMat(1)!.Request);

            responses.EnqueueJson(snapshot);
            responses.EnqueueJson(new TournamentScheduleDto(1, [taken]));
            responses.Enqueue(HttpStatusCode.NoContent);
            responses.Enqueue(HttpStatusCode.NoContent);
            responses.EnqueueJson(snapshot with { Categories = [done, Category with { IsClaimed = true, ClaimedBy = taken.ClaimedBy }] });
            responses.EnqueueJson(new JudgingCheckpointDto(null));
            await Assert.IsType<AsyncRelayCommand>(judge.StartNextScheduledCategoryCommand).ExecuteAsync();

            var claimIndex = responses.Requests.IndexOf($"/api/categories/{Category.Id}/claim");
            Assert.True(claimIndex >= 0);
            var claim = JsonSerializer.Deserialize<ClaimCategoryRequest>(responses.Bodies[claimIndex]!, JsonSerializerOptions.Web)!;
            Assert.Equal(reserved.SessionToken, claim.MatClaimToken);
            Assert.Equal(claim.SessionToken, judge.JudgeMatSession!.Token);
            Assert.False(judge.StartNextScheduledCategoryCommand.CanExecute(null));
        }
        finally
        {
            windows.Judge?.Close();
            judge?.CloseJudgeSchedule();
            if (Directory.Exists(folder))
                Directory.Delete(folder, recursive: true);
        }
    }

    [Fact]
    public async Task RegistrationRequestsSelectedGroupingAndImmediatelyRefreshesRosterSizes()
    {
        var responses = new TestApi();
        using var http = new HttpClient(responses);
        var organizer = await LoadAsync(http, responses);
        Assert.Equal(CategoryGrouping.AgeAndBelt, organizer.SelectedCategoryGrouping);
        organizer.SelectedCategoryGrouping = CategoryGrouping.Age;
        organizer.FirstName = "Nowy";
        organizer.LastName = Participant.LastName;
        organizer.Age = Participant.Age;
        organizer.SelectedSex = Participant.Sex;
        organizer.SelectedBelt = Participant.Belt;
        organizer.CategorySelections.Single(item => item.CategoryType == CategoryType.Kata).IsSelected = true;
        responses.EnqueueJson(Participant with { Id = 2, FirstName = "Nowy" }, HttpStatusCode.Created);
        responses.EnqueueJson(new[] { Summary with { ParticipantCount = 2 } });

        await Assert.IsType<AsyncRelayCommand>(organizer.AddParticipantCommand).ExecuteAsync();

        Assert.Equal((HttpMethod.Post, "/api/participants?groupBy=Age"), responses.Operations[1]);
        Assert.Equal((HttpMethod.Get, "/api/categories"), responses.Operations[2]);
        Assert.Equal(2, organizer.ApiParticipants.Count);
        Assert.Equal(2, Assert.Single(organizer.OrganizerCategories).Summary.ParticipantCount);
    }

    [Fact]
    public async Task ExcelImportUsesSelectedGroupingAndShowsNewCompetitionAfterSave()
    {
        var path = Path.Combine(Path.GetTempPath(), $"karate-organizer-import-{Guid.NewGuid():N}.xlsx");
        try
        {
            await new ExcelImportService().CreateSampleExcelFileAsync(path);
            using (var workbook = new ExcelPackage(new FileInfo(path)))
            {
                workbook.Workbook.Worksheets["Participants"].Cells["A2:G2"].LoadFromArrays(
                    [new object[] { "Nowy", "Zawodnik", 12, "", "Kyu8", "Male", "Kumite" }]);
                workbook.Save();
            }

            var responses = new TestApi();
            using var http = new HttpClient(responses);
            var organizer = await LoadAsync(http, responses, dialogs: new ImportDialogs(path));
            organizer.SelectedCategoryGrouping = CategoryGrouping.Belt;
            responses.EnqueueJson(new[] { Participant with { Id = 2, CategoryTypes = [CategoryType.Kumite] } },
                HttpStatusCode.Created);
            responses.EnqueueJson(new[] { Summary, Summary with { Id = 2, Name = "Kumite", CategoryType = CategoryType.Kumite } });

            await Assert.IsType<AsyncRelayCommand>(organizer.ImportExcelCommand).ExecuteAsync();

            Assert.Equal((HttpMethod.Post, "/api/participants/batch?groupBy=Belt"), responses.Operations[1]);
            Assert.Equal((HttpMethod.Get, "/api/categories"), responses.Operations[2]);
            Assert.Contains(organizer.OrganizerCategories, row => row.Summary.Name == "Kumite");
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public async Task OrganizerRequestsHandoffAndShowsPendingStatusUntilJudgeSaves()
    {
        var responses = new TestApi();
        using var http = new HttpClient(responses);
        var organizer = await LoadAsync(http, responses, dialogs: new ImportDialogs(string.Empty));
        var claimed = Summary with { IsClaimed = true, ClaimedBy = "Tatami 1" };
        responses.EnqueueJson(new[] { claimed });
        await organizer.RefreshOrganizerCategoriesAsync();
        var row = Assert.Single(organizer.OrganizerCategories);
        Assert.True(organizer.RequestJudgingHandoffCommand.CanExecute(row));
        responses.EnqueueJson(new ClaimRecoveryDto(new string('A', 64), "Tatami 1"));
        responses.Enqueue(HttpStatusCode.NoContent);
        responses.EnqueueJson(new[] { claimed with { HandoffRequested = true } });

        await Assert.IsType<AsyncRelayCommand<OrganizerCompetitionRow>>(organizer.RequestJudgingHandoffCommand).ExecuteAsync(row);

        Assert.Equal((HttpMethod.Get, "/api/categories/1/handoff"), responses.Operations[2]);
        Assert.Equal((HttpMethod.Post, "/api/categories/1/handoff"), responses.Operations[3]);
        var pending = Assert.Single(organizer.OrganizerCategories);
        Assert.Equal("Oczekuje na zwolnienie", pending.StatusText);
        Assert.False(organizer.RequestJudgingHandoffCommand.CanExecute(pending));
        Assert.False(organizer.CanManageApi && organizer.RecoverApiCategoryCommand.CanExecute(pending));
    }

    [Fact]
    public async Task JudgeKeepsCheckpointUntilHandoffIsConfirmedAndCanThenReclaimIt()
    {
        var folder = Path.Combine(Path.GetTempPath(), $"karate-handoff-{Guid.NewGuid():N}");
        var snapshot = new TournamentSnapshotDto([Participant], [Category]);
        var draft = new IndividualJudgeDraft(0, [8.5m], []);
        var session = new JudgingSession(Category.Id, Category.Name, Guid.NewGuid(), "Tatami 1")
        {
            Category = TournamentSnapshotMapper.MapCategories(snapshot).Single().Category,
            Draft = draft
        };
        var store = new JudgingSessionStore("test", folder);
        try
        {
            using (store.Lock(session.CategoryId))
                store.Save(session);
            var responses = new TestApi();
            using var http = new HttpClient(responses);
            var judge = await LoadAsync(http, responses, judge: true, sessionStore: store);
            responses.EnqueueJson(new JudgingHandoffState(true, false));
            responses.Enqueue(HttpStatusCode.ServiceUnavailable);
            await judge.CheckJudgingHandoffsAsync(CancellationToken.None);

            var pending = Assert.Single(judge.JudgingSessions);
            Assert.True(pending.AwaitingHandoff);
            Assert.Equal(draft.CurrentScores, pending.Draft!.CurrentScores);
            Assert.False(judge.ResumeApiJudgingCommand.CanExecute(pending));
            Assert.Equal((HttpMethod.Post, "/api/categories/1/handoff/save"), responses.Operations[2]);

            responses.EnqueueJson(new JudgingHandoffState(false, true));
            responses.EnqueueJson(new TournamentSnapshotDto([Participant], [Category with { HasJudgingCheckpoint = true }]));
            await judge.CheckJudgingHandoffsAsync(CancellationToken.None);

            Assert.Empty(judge.JudgingSessions);
            Assert.Null(store.Load(session.CategoryId));
            var category = Assert.Single(judge.Categories);
            Assert.Equal("Do wznowienia", category.ApiStatus);
            Assert.True(judge.StartApiJudgingCommand.CanExecute(category));
        }
        finally
        {
            if (Directory.Exists(folder))
                Directory.Delete(folder, recursive: true);
        }
    }

    [Fact]
    public async Task JudgePublishesTeamIdsAndReloadsTheTeamRanking()
    {
        const int teamId = 73;
        var dto = Category with
        {
            CategoryType = CategoryType.Team,
            ParticipantIds = [],
            Teams = [new TeamDto(teamId, "Team Kata A", Sex.Unisex, [Participant.Id])]
        };
        var snapshot = new TournamentSnapshotDto([Participant], [dto]);
        var category = TournamentSnapshotMapper.MapCategories(snapshot).Single().Category;
        category.IsFinished = true;
        var session = new JudgingSession(category.Id, category.Name, Guid.NewGuid(), "Tatami 1")
        {
            Category = category,
            TeamDraft = new TeamJudgeDraft(0, [], [new(teamId, [8m, 8m, 9m])])
        };
        var folder = Path.Combine(Path.GetTempPath(), $"karate-team-result-{Guid.NewGuid():N}");
        var store = new JudgingSessionStore("test", folder);
        try
        {
            using (store.Lock(session.CategoryId))
                store.Save(session);
            var responses = new TestApi();
            using var http = new HttpClient(responses);
            var judge = await LoadAsync(http, responses, judge: true, dialogs: new ImportDialogs(string.Empty),
                sessionStore: store, snapshot: snapshot);
            responses.Enqueue(HttpStatusCode.NoContent);
            responses.EnqueueJson(snapshot with
            {
                Categories = [dto with { IsFinished = true, TeamResults = [new(teamId, 1, 25m, [])] }]
            });

            await Assert.IsType<AsyncRelayCommand<JudgingSession>>(judge.SubmitApiJudgingCommand)
                .ExecuteAsync(Assert.Single(judge.JudgingSessions));

            Assert.Equal((HttpMethod.Post, "/api/categories/1/team-results"), responses.Operations[1]);
            var request = JsonSerializer.Deserialize<CompleteTeamCategoryRequest>(responses.Bodies[1]!, JsonSerializerOptions.Web)!;
            Assert.Equal(session.Token, request.SessionToken);
            Assert.Equal(teamId, Assert.Single(request.Results!).TeamId);
            Assert.Empty(judge.JudgingSessions);
            var published = Assert.IsType<KarateTournamentApp.Models.TeamCategory>(Assert.Single(judge.Categories).Category);
            var ranking = Assert.Single(published.TeamResults);
            Assert.Equal(25m, ranking.Score);
            Assert.Equal(Participant.Id, Assert.Single(ranking.Team.Members).Id);
        }
        finally
        {
            if (Directory.Exists(folder))
                Directory.Delete(folder, recursive: true);
        }
    }

    [Fact]
    public async Task GrapplingUsesTheCombatPanelAndPublishesItsOwnBracket()
    {
        var second = Participant with { Id = 2, FirstName = "Drugi" };
        var dto = Category with { CategoryType = CategoryType.Grappling, Name = "Grappling",
            ParticipantIds = [Participant.Id, second.Id] };
        var snapshot = new TournamentSnapshotDto([Participant, second], [dto]);
        var category = Assert.IsType<KarateTournamentApp.Models.ShobuSanbonCategory>(
            TournamentSnapshotMapper.MapCategories(snapshot).Single().Category);
        var dialogs = new ImportDialogs(string.Empty);
        var manager = new CompetitionManagerViewModel(category, dialogs, preserveBracket: true);
        var scoreboard = new ScoreboardViewModel(manager);
        var judgePanel = new ScoreboardJudgeViewModel(manager, dialogs, scoreboard);
        Assert.True(scoreboard.UsesMatchBracket);
        Assert.Equal("Kontrola Grapplingu", judgePanel.BracketControlsTitle);
        var match = Assert.IsType<KarateTournamentApp.Models.ShobuSanbonMatch>(manager.CurrentMatch);
        scoreboard.StartTimer();
        scoreboard.StopTimer();
        Assert.False(match.IsRunning);
        manager.UpdateMatchScore(true, 4);
        manager.FinishMatchCommand.Execute(null);
        Assert.True(category.IsFinished);
        Assert.Equal(Participant.Id, category.FinalResults[0].Participant.Id);
        var managerForResults = new IndividualCompetitionManagerViewModel(category, new WpfWindowService(), new WpfUiScheduler());
        Assert.True(new ResultsViewModel(managerForResults).HasBracketWinner);

        var folder = Path.Combine(Path.GetTempPath(), $"karate-grappling-{Guid.NewGuid():N}");
        var store = new JudgingSessionStore("test", folder);
        var session = new JudgingSession(category.Id, category.Name, Guid.NewGuid(), "Tatami")
        {
            Category = category
        };
        try
        {
            using (store.Lock(session.CategoryId))
                store.Save(session);
            var responses = new TestApi();
            using var http = new HttpClient(responses);
            var judge = await LoadAsync(http, responses, judge: true, dialogs: dialogs, sessionStore: store, snapshot: snapshot);
            responses.Enqueue(HttpStatusCode.NoContent);
            responses.EnqueueJson(snapshot with { Categories = [dto with { IsFinished = true }] });

            await Assert.IsType<AsyncRelayCommand<JudgingSession>>(judge.SubmitApiJudgingCommand)
                .ExecuteAsync(Assert.Single(judge.JudgingSessions));

            Assert.Equal((HttpMethod.Post, "/api/categories/1/grappling-results"), responses.Operations[1]);
            var request = JsonSerializer.Deserialize<CompleteKumiteCategoryRequest>(responses.Bodies[1]!, JsonSerializerOptions.Web)!;
            Assert.Equal(4, Assert.Single(request.Matches!).AkaScore);
            Assert.Equal(new[] { Participant.Id, second.Id }, request.RankedParticipantIds);
            Assert.Empty(judge.JudgingSessions);
        }
        finally
        {
            if (Directory.Exists(folder))
                Directory.Delete(folder, recursive: true);
        }
    }

    private static async Task<MainViewModel> LoadAsync(
        HttpClient http, TestApi responses, bool judge = false, IDialogService? dialogs = null,
        JudgingSessionStore? sessionStore = null, TournamentSnapshotDto? snapshot = null, IWindowService? windows = null)
    {
        responses.EnqueueJson(snapshot ?? new TournamentSnapshotDto([Participant], [Category]));
        var api = new TournamentApiClient(http, $"https://{Guid.NewGuid():N}.test");
        var viewModel = new MainViewModel(new CategoryManager(), dialogs ?? new WpfDialogService(),
            windows ?? new WpfWindowService(), new WpfUiScheduler(), api, isJudgeStation: judge, sessionStore: sessionStore);
        await Assert.IsType<AsyncRelayCommand>(viewModel.LoadApiTournamentCommand).ExecuteAsync();
        return viewModel;
    }

    private sealed class HistoryWindow : IWindowService
    {
        public ParticipantResultHistoryViewModel? History { get; private set; }

        public bool? ShowDialog(object viewModel, WindowOptions? options = null, Action<IWindowHandle>? onOpened = null)
        {
            History = Assert.IsType<ParticipantResultHistoryViewModel>(viewModel);
            return null;
        }

        public void Register<TViewModel, TView>() where TView : System.Windows.FrameworkElement, new() =>
            throw new NotSupportedException();
        public IWindowHandle Show(object viewModel, WindowOptions? options = null) => throw new NotSupportedException();
        public IWindowHandle ShowOrUpdate(string key, object viewModel, WindowOptions? options = null,
            EventHandler? onClosed = null) => throw new NotSupportedException();
    }

    private sealed class QueueWindows : IWindowService
    {
        public IWindowHandle? Judge { get; private set; }

        public IWindowHandle Show(object viewModel, WindowOptions? options = null) => Judge = new Handle();
        public IWindowHandle ShowOrUpdate(string key, object viewModel, WindowOptions? options = null,
            EventHandler? onClosed = null) => new Handle();
        public void Register<TViewModel, TView>() where TView : System.Windows.FrameworkElement, new() =>
            throw new NotSupportedException();
        public bool? ShowDialog(object viewModel, WindowOptions? options = null, Action<IWindowHandle>? onOpened = null) =>
            throw new NotSupportedException();

        private sealed class Handle : IWindowHandle
        {
            public event EventHandler? Closed;
            public void Close() => Closed?.Invoke(this, EventArgs.Empty);
        }
    }

    private sealed class ImportDialogs(string path, string? station = null) : IDialogService
    {
        public DialogResult ShowMessage(string message, string title, DialogButtons buttons, DialogIcon icon) =>
            buttons == DialogButtons.YesNo ? DialogResult.Yes : DialogResult.Ok;

        public string ShowOpenFileDialog(string title, string filter) => path;
        public string? ShowTextInput(string title, string prompt, string defaultValue) => station ?? throw new NotSupportedException();
        public string? ShowSaveFileDialog(string title, string filter, string defaultFileName) => throw new NotSupportedException();
    }

    private sealed class TestApi : HttpMessageHandler
    {
        private readonly Queue<Task<HttpResponseMessage>> _responses = new();

        public List<string> Requests { get; } = [];
        public List<(HttpMethod Method, string Path)> Operations { get; } = [];
        public List<string?> Bodies { get; } = [];

        public void EnqueueJson<T>(T value, HttpStatusCode status = HttpStatusCode.OK) => _responses.Enqueue(Task.FromResult(
            new HttpResponseMessage(status) { Content = JsonContent.Create(value) }));

        public void Enqueue(HttpStatusCode status) => _responses.Enqueue(Task.FromResult(new HttpResponseMessage(status)));

        public TaskCompletionSource<HttpResponseMessage> EnqueuePending()
        {
            var response = new TaskCompletionSource<HttpResponseMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
            _responses.Enqueue(response.Task);
            return response;
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request.RequestUri!.AbsolutePath);
            Operations.Add((request.Method, request.RequestUri.PathAndQuery));
            var response = _responses.Dequeue();
            Bodies.Add(request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken));
            return await response.WaitAsync(cancellationToken);
        }
    }
}
