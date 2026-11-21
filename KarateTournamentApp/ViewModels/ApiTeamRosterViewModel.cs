using System.Collections.ObjectModel;
using System.Windows.Input;
using KarateClassLibrary.Contracts;
using KarateTournamentApp.Commands;
using KarateTournamentApp.Services.Dialogs;

namespace KarateTournamentApp.ViewModels;

public sealed class ApiTeamRosterViewModel : ViewModelBase
{
    private readonly IReadOnlyList<ParticipantDto> _participants;
    private readonly Func<int?, SaveTeamRequest, Task<TeamDto>> _save;
    private readonly Func<int, Task> _delete;
    private readonly IDialogService _dialogs;
    private readonly Sex _defaultSex;
    private TeamDto? _selectedTeam;
    private bool _isBusy;
    private string _status = string.Empty;

    public ApiTeamRosterViewModel(CategoryDto category, IReadOnlyList<ParticipantDto> participants,
        Func<int?, SaveTeamRequest, Task<TeamDto>> save, Func<int, Task> delete, IDialogService dialogs)
    {
        _participants = participants.OrderBy(person => person.LastName).ThenBy(person => person.FirstName).ToArray();
        _save = save;
        _delete = delete;
        _dialogs = dialogs;
        _defaultSex = category.Sex;
        Teams = new(category.Teams);
        Roster = new ParticipantRosterEditor(_participants, []);
        Sex = _defaultSex;

        NewTeamCommand = new RelayCommand(_ => LoadTeam(null), _ => !IsBusy);
        SaveCommand = new AsyncRelayCommand(SaveAsync, error => Status = error.Message, () => !IsBusy);
        DeleteCommand = new AsyncRelayCommand(DeleteAsync, error => Status = error.Message,
            () => !IsBusy && SelectedTeam is not null);
    }

    public ObservableCollection<TeamDto> Teams { get; }
    public TeamDto? SelectedTeam
    {
        get => _selectedTeam;
        set
        {
            if (_selectedTeam != value)
                LoadTeam(value);
        }
    }

    public ParticipantRosterEditor Roster { get; private set; }
    public string Name { get; set; } = string.Empty;
    public Sex Sex { get; set; }
    public Sex[] SexOptions { get; } = Enum.GetValues<Sex>();
    public ICommand NewTeamCommand { get; }
    public ICommand SaveCommand { get; }
    public ICommand DeleteCommand { get; }

    public bool IsBusy
    {
        get => _isBusy;
        private set { _isBusy = value; OnPropertyChanged(); CommandManager.InvalidateRequerySuggested(); }
    }

    public string Status
    {
        get => _status;
        private set { _status = value; OnPropertyChanged(); }
    }

    private void LoadTeam(TeamDto? team)
    {
        _selectedTeam = team;
        Name = team?.Name ?? string.Empty;
        Sex = team?.Sex ?? _defaultSex;
        Roster = new ParticipantRosterEditor(_participants, team?.MemberParticipantIds ?? []);
        Status = string.Empty;
        OnPropertyChanged(nameof(SelectedTeam));
        OnPropertyChanged(nameof(Name));
        OnPropertyChanged(nameof(Sex));
        OnPropertyChanged(nameof(Roster));
        CommandManager.InvalidateRequerySuggested();
    }

    private async Task SaveAsync()
    {
        var selectedIds = Roster.GetSelectedIds();
        if (string.IsNullOrWhiteSpace(Name) || selectedIds.Length == 0)
        {
            Status = "Podaj nazwę drużyny i wybierz zawodników z rejestru.";
            return;
        }

        IsBusy = true;
        try
        {
            var existing = SelectedTeam;
            var saved = await _save(existing?.Id, new SaveTeamRequest(Name.Trim(), Sex, selectedIds));
            if (existing is null)
                Teams.Add(saved);
            else
                Teams[Teams.IndexOf(existing)] = saved;
            LoadTeam(saved);
            Status = "Zapisano drużynę.";
        }
        finally { IsBusy = false; }
    }

    private async Task DeleteAsync()
    {
        var team = SelectedTeam!;
        if (_dialogs.ShowMessage($"Usunąć drużynę „{team.Name}”? Jej członkowie pozostaną w rejestrze.",
            "Usuń drużynę", DialogButtons.YesNo, DialogIcon.Warning) != DialogResult.Yes)
            return;

        IsBusy = true;
        try
        {
            await _delete(team.Id);
            Teams.Remove(team);
            LoadTeam(null);
            Status = "Usunięto drużynę.";
        }
        finally { IsBusy = false; }
    }
}
