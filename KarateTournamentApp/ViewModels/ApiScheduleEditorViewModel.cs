using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Windows.Input;
using KarateClassLibrary.Contracts;
using KarateTournamentApp.Commands;
using KarateTournamentApp.Services;
using KarateTournamentApp.Services.Api;
using KarateTournamentApp.Services.Dialogs;

namespace KarateTournamentApp.ViewModels;

public sealed class ApiScheduleEditorViewModel : ViewModelBase
{
    private readonly TournamentApiClient _api;
    private readonly IDialogService _dialogs;
    private CategorySummaryDto[] _categories;
    private long _version;
    private Mat? _selectedMat;
    private bool _busy;
    private string? _status;

    internal ApiScheduleEditorViewModel(TournamentScheduleDto schedule, CategorySummaryDto[] categories,
        TournamentApiClient api, IDialogService dialogs)
    {
        _api = api;
        _dialogs = dialogs;
        _categories = categories;
        ApplyMatCountCommand = new RelayCommand(_ => ResizeMats(), _ => CanEdit);
        AddSlotCommand = new RelayCommand(_ => AddSlot(), _ => CanEdit && SelectedMat is not null && SelectedCategory is not null);
        RemoveSlotCommand = new RelayCommand(_ => RemoveSlot(), _ => CanEdit && SelectedSlot is not null);
        MoveUpCommand = new RelayCommand(_ => MoveWithinMat(-1), _ => CanMove(-1));
        MoveDownCommand = new RelayCommand(_ => MoveWithinMat(1), _ => CanMove(1));
        MoveToMatCommand = new RelayCommand(_ => MoveToMat(),
            _ => CanEdit && SelectedSlot is not null && DestinationMat is not null && DestinationMat != SelectedMat);
        SaveCommand = new AsyncRelayCommand(SaveAsync, ShowError, () => CanEdit);
        ReloadCommand = new AsyncRelayCommand(() => RunBusyAsync(ReloadAsync), ShowError, () => CanEdit);
        ExportCommand = new AsyncRelayCommand(ExportAsync, ShowError, () => CanEdit);
        RecoverMatCommand = new AsyncRelayCommand(RecoverMatAsync, ShowError,
            () => CanEdit && SelectedMat?.Reservation is { ClaimedBy: not null });
        Load(schedule);
    }

    public ObservableCollection<Mat> Mats { get; } = [];
    public string MatCountText { get; set; } = "0";
    public Mat? DestinationMat { get; set; }
    public Slot? SelectedSlot { get; set; }
    public CategorySummaryDto? SelectedCategory { get; set; }
    public Mat? SelectedMat
    {
        get => _selectedMat;
        set
        {
            if (_selectedMat == value)
                return;
            _selectedMat = value;
            SelectedSlot = null;
            OnPropertyChanged(nameof(SelectedSlot));
            OnPropertyChanged();
        }
    }

    public IEnumerable<CategorySummaryDto> AvailableCategories
    {
        get
        {
            var scheduled = Mats.SelectMany(mat => mat.Slots).Select(slot => slot.CategoryId).ToHashSet();
            return _categories.Where(category => !category.IsClaimed && !scheduled.Contains(category.Id))
                .OrderBy(category => category.Name).ThenBy(category => category.Id);
        }
    }

    public bool CanEdit => !_busy;
    public string? Status
    {
        get => _status;
        private set { _status = value; OnPropertyChanged(); }
    }

    public ICommand ApplyMatCountCommand { get; }
    public ICommand AddSlotCommand { get; }
    public ICommand RemoveSlotCommand { get; }
    public ICommand MoveUpCommand { get; }
    public ICommand MoveDownCommand { get; }
    public ICommand MoveToMatCommand { get; }
    public ICommand SaveCommand { get; }
    public ICommand ReloadCommand { get; }
    public ICommand ExportCommand { get; }
    public ICommand RecoverMatCommand { get; }

    private void Load(TournamentScheduleDto schedule)
    {
        var selectedNumber = SelectedMat?.Number;
        _version = schedule.Version;
        Mats.Clear();
        foreach (var source in schedule.Mats)
        {
            var mat = new Mat(source.Number) { Reservation = source };
            foreach (var slot in source.Slots)
                mat.Slots.Add(new Slot(slot.CategoryId, slot.CategoryName, slot.StartsAt, slot.EndsAt));
            Mats.Add(mat);
        }
        MatCountText = Mats.Count.ToString(CultureInfo.InvariantCulture);
        OnPropertyChanged(nameof(MatCountText));
        OnPropertyChanged(nameof(AvailableCategories));
        SelectedMat = Mats.FirstOrDefault(mat => mat.Number == selectedNumber) ?? Mats.FirstOrDefault();
    }

    private bool ResizeMats()
    {
        if (!int.TryParse(MatCountText, out var count) || count is < 0 or > 100)
        {
            Status = "Podaj liczbę mat od 0 do 100.";
            return false;
        }
        if (Mats.Skip(count).Any(mat => mat.Slots.Count > 0 || mat.Reservation?.ClaimedBy is not null))
        {
            Status = "Przed usunięciem maty przenieś z niej konkurencje i zwolnij stanowisko.";
            return false;
        }
        while (Mats.Count > count)
            Mats.RemoveAt(Mats.Count - 1);
        while (Mats.Count < count)
            Mats.Add(new Mat(Mats.Count + 1));
        if (!Mats.Contains(SelectedMat!))
            SelectedMat = Mats.FirstOrDefault();
        Status = null;
        return true;
    }

    private void AddSlot()
    {
        var category = SelectedCategory!;
        var slot = new Slot(category.Id, category.Name, null, null);
        SelectedMat!.Slots.Add(slot);
        SelectedSlot = slot;
        SelectedCategory = null;
        OnPropertyChanged(nameof(SelectedSlot));
        OnPropertyChanged(nameof(AvailableCategories));
        OnPropertyChanged(nameof(SelectedCategory));
    }

    private void RemoveSlot()
    {
        SelectedMat!.Slots.Remove(SelectedSlot!);
        SelectedSlot = null;
        OnPropertyChanged(nameof(SelectedSlot));
        OnPropertyChanged(nameof(AvailableCategories));
    }

    private bool CanMove(int offset)
    {
        if (!CanEdit || SelectedMat is null || SelectedSlot is null)
            return false;
        var index = SelectedMat.Slots.IndexOf(SelectedSlot);
        return index >= 0 && index + offset >= 0 && index + offset < SelectedMat.Slots.Count;
    }

    private void MoveWithinMat(int offset)
    {
        var slots = SelectedMat!.Slots;
        var index = slots.IndexOf(SelectedSlot!);
        slots.Move(index, index + offset);
    }

    private void MoveToMat()
    {
        var slot = SelectedSlot!;
        var destination = DestinationMat!;
        SelectedMat!.Slots.Remove(slot);
        destination.Slots.Add(slot);
        SelectedMat = destination;
        SelectedSlot = slot;
        OnPropertyChanged(nameof(SelectedSlot));
    }

    private Task SaveAsync() => RunBusyAsync(async () =>
    {
        if (!ResizeMats())
            return;
        var slots = Mats.SelectMany(mat => mat.Slots.Select(slot => slot.ToRequest(mat.Number))).ToArray();
        await _api.SaveScheduleAsync(new(_version, Mats.Count, slots));
        try
        {
            Load(await _api.GetScheduleAsync());
            Status = "Zapisano harmonogram.";
        }
        catch (Exception error) when (error is InvalidOperationException or IOException)
        {
            Status = $"Zapisano harmonogram. Odśwież zapisany plan przed kolejną edycją. {error.Message}";
        }
    });

    private async Task ReloadAsync()
    {
        var schedule = await _api.GetScheduleAsync();
        _categories = await _api.GetCategorySummariesAsync(CancellationToken.None);
        Load(schedule);
        Status = "Wczytano zapisany harmonogram.";
    }

    private Task ExportAsync() => RunBusyAsync(async () =>
    {
        var path = _dialogs.ShowSaveFileDialog("Eksportuj harmonogram", "Harmonogram do wydruku (*.html)|*.html",
            "harmonogram.html");
        if (path is null)
            return;
        var schedule = await _api.GetScheduleAsync();
        await File.WriteAllTextAsync(path, SchedulePrintExport.CreateHtml(schedule));
        Status = "Zapisano plik. Otwórz go w przeglądarce i wybierz Drukuj (Ctrl+P).";
    });

    private Task RecoverMatAsync() => RunBusyAsync(async () =>
    {
        var mat = SelectedMat!;
        var reservation = mat.Reservation!;
        if (_dialogs.ShowMessage($"Zwolnić matę {mat.Number} przydzieloną do „{reservation.ClaimedBy}”?",
                "Zwolnij matę", DialogButtons.YesNo, DialogIcon.Question) != DialogResult.Yes)
            return;
        await _api.RecoverMatAsync(mat.Number, new(reservation.Revision));
        mat.Reservation = (await _api.GetScheduleAsync()).Mats.SingleOrDefault(item => item.Number == mat.Number);
        Status = "Zwolniono matę.";
    });

    private async Task RunBusyAsync(Func<Task> action)
    {
        _busy = true;
        OnPropertyChanged(nameof(CanEdit));
        try { await action(); }
        finally
        {
            _busy = false;
            OnPropertyChanged(nameof(CanEdit));
            CommandManager.InvalidateRequerySuggested();
        }
    }

    private void ShowError(Exception error) => Status = error.Message;

    public sealed class Mat(int number) : ViewModelBase
    {
        private ScheduleMatDto? _reservation;
        public int Number { get; } = number;
        public string Label => $"Mata {Number}";
        public ObservableCollection<Slot> Slots { get; } = [];
        public string Station => Reservation?.ClaimedBy is { } station ? $"Stanowisko {station}" : "Wolna";
        internal ScheduleMatDto? Reservation
        {
            get => _reservation;
            set { _reservation = value; OnPropertyChanged(nameof(Station)); }
        }
    }

    public sealed class Slot(int categoryId, string name, DateTime? start, DateTime? end) : ViewModelBase
    {
        private const string TimeFormat = "yyyy-MM-dd HH:mm";
        private string? _startsAt = start?.ToString(TimeFormat, CultureInfo.InvariantCulture);
        private string? _endsAt = end?.ToString(TimeFormat, CultureInfo.InvariantCulture);

        public int CategoryId { get; } = categoryId;
        public string Name { get; } = name;
        public string? StartsAt { get => _startsAt; set { _startsAt = value; OnPropertyChanged(); } }
        public string? EndsAt { get => _endsAt; set { _endsAt = value; OnPropertyChanged(); } }

        internal ScheduleSlotRequest ToRequest(int matNumber)
        {
            var start = ParseTime(StartsAt, "początek");
            var end = ParseTime(EndsAt, "koniec");
            if (start >= end)
                throw new InvalidOperationException($"„{Name}”: planowany koniec musi być po rozpoczęciu.");
            return new ScheduleSlotRequest(CategoryId, matNumber, start, end);
        }

        private DateTime? ParseTime(string? input, string label)
        {
            if (string.IsNullOrWhiteSpace(input))
                return null;
            if (DateTime.TryParseExact(input.Trim(), TimeFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out var time))
                return time;
            throw new InvalidOperationException($"„{Name}”: wpisz {label} w formacie RRRR-MM-DD GG:MM.");
        }
    }
}
