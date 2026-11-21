using System.ComponentModel.DataAnnotations;
using System.Windows.Input;
using KarateClassLibrary.Contracts;
using KarateTournamentApp.Commands;

namespace KarateTournamentApp.ViewModels;

public sealed class ApiCategoryEditorViewModel : ViewModelBase
{
    private string _status = string.Empty;
    private CategoryType _discipline;

    public ApiCategoryEditorViewModel(
        CategoryDto? current, IReadOnlyList<ParticipantDto> participants, Func<CreateCategoryRequest, Task> save)
    {
        Name = current?.Name ?? string.Empty;
        MinAge = current?.MinAge?.ToString() ?? string.Empty;
        MaxAge = current?.MaxAge?.ToString() ?? string.Empty;
        Sex = current?.Sex ?? Sex.Unisex;
        Discipline = current?.CategoryType ?? CategoryType.Kata;
        var selectedBelts = current?.AllowedBelts ?? [];
        BeltOptions = selectedBelts.Concat(Enum.GetValues<Belts>()).Distinct()
            .Select(belt => new Choice<Belts>(belt, belt.ToString(), selectedBelts.Contains(belt))).ToArray();
        Roster = new ParticipantRosterEditor(participants, current?.ParticipantIds ?? []);
        SaveCommand = new AsyncRelayCommand(async () =>
        {
            Status = string.Empty;
            if (!TryAge(MinAge, out var minimum) || !TryAge(MaxAge, out var maximum))
            {
                Status = "Wpisz wiek jako liczbę całkowitą albo pozostaw pole puste.";
                return;
            }
            var memberIds = IsTeamKata
                ? current is { CategoryType: CategoryType.Team } ? current.ParticipantIds.ToArray() : []
                : Roster.GetSelectedIds();
            var request = new CreateCategoryRequest(Name, minimum, maximum, Sex, Discipline,
                BeltOptions.Where(option => option.IsSelected).Select(option => option.Value).ToArray(), memberIds);
            var errors = new List<ValidationResult>();
            if (!Validator.TryValidateObject(request, new ValidationContext(request), errors, validateAllProperties: true))
            {
                Status = string.Join(Environment.NewLine, errors.Select(error => error.ErrorMessage));
                return;
            }
            await save(request);
        }, error => Status = error.Message);
    }

    public string Name { get; set; }
    public string MinAge { get; set; }
    public string MaxAge { get; set; }
    public Sex Sex { get; set; }
    public CategoryType Discipline
    {
        get => _discipline;
        set
        {
            _discipline = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsTeamKata));
        }
    }

    public bool IsTeamKata => Discipline == CategoryType.Team;
    public Sex[] SexOptions { get; } = Enum.GetValues<Sex>();
    public IReadOnlyDictionary<string, CategoryType> Disciplines { get; } = Enum.GetValues<CategoryType>()
        .ToDictionary(type => type == CategoryType.Team ? "Team Kata" : type.ToString());
    public IReadOnlyList<Choice<Belts>> BeltOptions { get; }
    public ParticipantRosterEditor Roster { get; }
    public ICommand SaveCommand { get; }
    public string Status
    {
        get => _status;
        private set { _status = value; OnPropertyChanged(); }
    }

    private static bool TryAge(string text, out int? value)
    {
        value = null;
        if (string.IsNullOrWhiteSpace(text))
            return true;
        if (!int.TryParse(text, out var age))
            return false;
        value = age;
        return true;
    }

    public sealed class Choice<T>(T value, string label, bool isSelected)
    {
        public T Value { get; } = value;
        public string Label { get; } = label;
        public bool IsSelected { get; set; } = isSelected;
    }
}
