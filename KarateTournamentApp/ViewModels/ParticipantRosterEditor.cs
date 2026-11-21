using System.Collections.ObjectModel;
using System.Windows.Input;
using KarateClassLibrary.Contracts;
using KarateTournamentApp.Commands;

namespace KarateTournamentApp.ViewModels;

public sealed class ParticipantRosterEditor
{
    public ParticipantRosterEditor(IReadOnlyList<ParticipantDto> participants, IReadOnlyList<int> selectedIds)
    {
        var peopleById = participants.ToDictionary(person => person.Id);
        var selection = selectedIds.ToHashSet();
        Choices = new(selectedIds.Concat(participants.Select(person => person.Id))
            .Distinct().Where(peopleById.ContainsKey).Select(id =>
            {
                var person = peopleById[id];
                return new Entry(id, $"{person.LastName} {person.FirstName} (ID {id})", selection.Contains(id));
            }));
        MoveUpCommand = new RelayCommand(_ => MoveSelected(-1), _ => CanMove(-1));
        MoveDownCommand = new RelayCommand(_ => MoveSelected(1), _ => CanMove(1));
    }

    public ObservableCollection<Entry> Choices { get; }
    public Entry? SelectedParticipant { get; set; }
    public ICommand MoveUpCommand { get; }
    public ICommand MoveDownCommand { get; }

    public int[] GetSelectedIds() => Choices.Where(entry => entry.IsSelected).Select(entry => entry.Id).ToArray();

    private bool CanMove(int offset)
    {
        var index = SelectedParticipant is null ? -1 : Choices.IndexOf(SelectedParticipant);
        return index >= 0 && index + offset >= 0 && index + offset < Choices.Count;
    }

    private void MoveSelected(int offset)
    {
        if (!CanMove(offset))
            return;
        var index = Choices.IndexOf(SelectedParticipant!);
        Choices.Move(index, index + offset);
    }

    public sealed class Entry(int id, string label, bool isSelected)
    {
        public int Id { get; } = id;
        public string Label { get; } = label;
        public bool IsSelected { get; set; } = isSelected;
    }
}
