using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Windows.Input;
using KarateClassLibrary.Contracts;

namespace KarateTournamentApp.ViewModels;

public partial class MainViewModel
{
    private OrganizerCompetitionRow[] _organizerCategories = [];
    private OrganizerCompetitionRow? _selectedOrganizerCategory;
    private OrganizerCompetitionStatus _organizerStatusFilter;
    private int _categoryVersion;
    private int _categoryRequestsInFlight;
    private DateTimeOffset? _lastCategoryRefresh;
    private bool _categoryRefreshFailed;

    public ICommand RefreshOrganizerCategoriesCommand { get; }
    public ICommand ViewApiCategoryResultsCommand { get; }

    public IReadOnlyDictionary<string, OrganizerCompetitionStatus> OrganizerStatusFilters { get; } =
        Enum.GetValues<OrganizerCompetitionStatus>().ToDictionary(OrganizerCompetitionRow.GetStatusText);

    public IEnumerable<OrganizerCompetitionRow> OrganizerCategories =>
        _organizerCategories.Where(row => OrganizerStatusFilter == OrganizerCompetitionStatus.All
            || row.Status == OrganizerStatusFilter);

    public OrganizerCompetitionRow? SelectedOrganizerCategory
    {
        get => _selectedOrganizerCategory;
        set
        {
            if (_selectedOrganizerCategory == value)
                return;
            _selectedOrganizerCategory = value;
            OnPropertyChanged();
            CommandManager.InvalidateRequerySuggested();
        }
    }

    public OrganizerCompetitionStatus OrganizerStatusFilter
    {
        get => _organizerStatusFilter;
        set
        {
            if (_organizerStatusFilter == value)
                return;
            _organizerStatusFilter = value;
            OnPropertyChanged();
            ApplyCategoryFilter();
        }
    }

    public string OrganizerCategoryRefreshStatus
    {
        get
        {
            var lastRefresh = _lastCategoryRefresh is { } time
                ? $"Ostatnia aktualizacja {time:HH:mm:ss}"
                : "Oczekiwanie na dane z API";
            return _categoryRefreshFailed ? $"Odświeżenie nie powiodło się · {lastRefresh}" : lastRefresh;
        }
    }

    internal async Task RefreshOrganizerCategoriesAsync(bool force = false, CancellationToken cancellationToken = default)
    {
        if (!CanManageApi || (!force && (IsLoading || _categoryRequestsInFlight > 0)))
            return;

        var version = ++_categoryVersion;
        _categoryRequestsInFlight++;
        try
        {
            var categories = await _api!.GetCategorySummariesAsync(cancellationToken);
            if (version == _categoryVersion && (force || !IsLoading) && !cancellationToken.IsCancellationRequested)
                ShowRefreshedCategories(categories);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return;
        }
        catch (Exception error) when (error is HttpRequestException or IOException or JsonException
            or OperationCanceledException or InvalidOperationException)
        {
            if (version == _categoryVersion)
            {
                _categoryRefreshFailed = true;
                OnPropertyChanged(nameof(OrganizerCategoryRefreshStatus));
            }
        }
        finally
        {
            _categoryRequestsInFlight--;
        }
    }

    private void ShowRefreshedCategories(IEnumerable<CategorySummaryDto> categories)
    {
        ReplaceOrganizerCategories(categories.Select(category => new OrganizerCompetitionRow(category)));
        _lastCategoryRefresh = DateTimeOffset.Now;
        _categoryRefreshFailed = false;
        OnPropertyChanged(nameof(OrganizerCategoryRefreshStatus));
    }

    private void ReplaceOrganizerCategories(IEnumerable<OrganizerCompetitionRow> rows)
    {
        var ordered = rows.OrderBy(row => row.Summary.Id).ToArray();
        _categoryVersion++;
        if (ordered.SequenceEqual(_organizerCategories))
            return;
        _organizerCategories = ordered;
        ApplyCategoryFilter();
    }

    private void ApplyCategoryFilter()
    {
        var selectedId = SelectedOrganizerCategory?.Summary.Id;
        OnPropertyChanged(nameof(OrganizerCategories));
        SelectedOrganizerCategory = OrganizerCategories.FirstOrDefault(row => row.Summary.Id == selectedId);
    }

    private static CategorySummaryDto SummarizeCategory(CategoryDto category) => new(
        category.Id, category.Name, category.CategoryType, category.Sex, category.MinAge, category.MaxAge,
        category.IsFinished, category.ParticipantIds.Count, category.Teams.Count, category.IsClaimed, category.ClaimedBy);
}
