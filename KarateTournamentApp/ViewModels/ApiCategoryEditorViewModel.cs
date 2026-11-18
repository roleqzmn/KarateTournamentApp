using System.Collections.ObjectModel;
using System.ComponentModel.DataAnnotations;
using System.Windows.Input;
using KarateClassLibrary.Contracts;
using KarateTournamentApp.Commands;

namespace KarateTournamentApp.ViewModels;

public sealed class ApiCategoryEditorViewModel : ViewModelBase
{
    private string _status = string.Empty;

    public ApiCategoryEditorViewModel(
        CategoryDto? current, IReadOnlyList<ParticipantDto> participants, Func<CreateCategoryRequest, Task> save)
    {
        Name = current?.Name ?? string.Empty;
        MinAge = current?.MinAge?.ToString() ?? string.Empty;
        MaxAge = current?.MaxAge?.ToString() ?? string.Empty;
        Sex = current?.Sex ?? Sex.Unisex;
        Discipline = current?.CategoryType ?? CategoryType.Kata;
        var selectedBelts = current?.AllowedBelts ?? [];
        var roster = current?.ParticipantIds ?? [];
        BeltOptions = selectedBelts.Concat(Enum.GetValues<Belts>()).Distinct()
            .Select(belt => new Choice<Belts>(belt, belt.ToString(), selectedBelts.Contains(belt))).ToArray();
        var peopleById = participants.ToDictionary(person => person.Id);
        Roster = new ObservableCollection<Choice<int>>(roster.Concat(participants.Select(person => person.Id))
            .Distinct().Where(peopleById.ContainsKey).Select(id =>
            {
                var person = peopleById[id];
                return new Choice<int>(id, $"{person.LastName} {person.FirstName} (ID {id})", roster.Contains(id));
            }));
        MoveUpCommand = new RelayCommand(_ => MoveSelected(-1), _ => CanMove(-1));
        MoveDownCommand = new RelayCommand(_ => MoveSelected(1), _ => CanMove(1));
        SaveCommand = new AsyncRelayCommand(async () =>
        {
            Status = string.Empty;
            if (!TryAge(MinAge, out var minimum) || !TryAge(MaxAge, out var maximum))
            {
                Status = "Wpisz wiek jako liczbę całkowitą albo pozostaw pole puste.";
                return;
            }
            var request = new CreateCategoryRequest(Name, minimum, maximum, Sex, Discipline,
                BeltOptions.Where(option => option.IsSelected).Select(option => option.Value).ToArray(),
                Roster.Where(option => option.IsSelected).Select(option => option.Value).ToArray());
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
    public CategoryType Discipline { get; set; }
    public Sex[] SexOptions { get; } = Enum.GetValues<Sex>();
    public CategoryType[] Disciplines { get; } = Enum.GetValues<CategoryType>();
    public IReadOnlyList<Choice<Belts>> BeltOptions { get; }
    public ObservableCollection<Choice<int>> Roster { get; }
    public Choice<int>? SelectedParticipant { get; set; }
    public ICommand MoveUpCommand { get; }
    public ICommand MoveDownCommand { get; }
    public ICommand SaveCommand { get; }
    public string Status
    {
        get => _status;
        private set { _status = value; OnPropertyChanged(); }
    }

    private bool CanMove(int offset)
    {
        var index = SelectedParticipant is null ? -1 : Roster.IndexOf(SelectedParticipant);
        return index >= 0 && index + offset >= 0 && index + offset < Roster.Count;
    }

    private void MoveSelected(int offset)
    {
        if (!CanMove(offset))
            return;
        var index = Roster.IndexOf(SelectedParticipant!);
        Roster.Move(index, index + offset);
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
