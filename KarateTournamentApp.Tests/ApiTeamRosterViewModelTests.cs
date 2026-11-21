using KarateClassLibrary;
using KarateClassLibrary.Contracts;
using KarateTournamentApp.Commands;
using KarateTournamentApp.Services.Dialogs;
using KarateTournamentApp.ViewModels;
using Xunit;

namespace KarateTournamentApp.Tests;

public sealed class ApiTeamRosterViewModelTests
{
    private static readonly ParticipantDto First = new(17, "Ada", "Nowak", 15, null, Belts.Kyu8, Sex.Female, [CategoryType.Kata]);
    private static readonly ParticipantDto Second = new(42, "Ola", "Testowa", 15, null, Belts.Kyu8, Sex.Female, [CategoryType.Kata]);
    private static readonly CategoryDto TeamCategory = new(1, "Team Kata", 12, 17, false, Sex.Female, CategoryType.Team,
        [Belts.Kyu8], [], [], [], [], false, null);

    [Fact]
    public async Task SavesExistingParticipantIdsInTheSelectedOrderAndKeepsTheTeamIdOnEdit()
    {
        var writes = new List<(int? TeamId, SaveTeamRequest Request)>();
        var viewModel = new ApiTeamRosterViewModel(TeamCategory, [First, Second], (id, request) =>
        {
            writes.Add((id, request));
            return Task.FromResult(new TeamDto(id ?? 99, request.Name!, request.Sex!.Value, request.MemberParticipantIds!));
        }, _ => throw new InvalidOperationException(), new WpfDialogService());
        viewModel.Name = "  Drużyna A  ";
        foreach (var member in viewModel.Roster.Choices)
            member.IsSelected = true;
        viewModel.Roster.SelectedParticipant = viewModel.Roster.Choices.Single(member => member.Id == Second.Id);
        viewModel.Roster.MoveUpCommand.Execute(null);

        var save = Assert.IsType<AsyncRelayCommand>(viewModel.SaveCommand);
        await save.ExecuteAsync();

        var firstWrite = Assert.Single(writes);
        Assert.Null(firstWrite.TeamId);
        Assert.Equal(Sex.Female, firstWrite.Request.Sex);
        Assert.Equal(new[] { Second.Id, First.Id }, firstWrite.Request.MemberParticipantIds);
        Assert.Equal("Drużyna A", Assert.Single(viewModel.Teams).Name);
        Assert.Equal(new[] { Second.Id, First.Id }, viewModel.Roster.GetSelectedIds());

        viewModel.Name = "Drużyna B";
        await save.ExecuteAsync();

        Assert.Equal(99, writes[1].TeamId);
        Assert.Equal("Drużyna B", Assert.Single(viewModel.Teams).Name);
    }

    [Fact]
    public async Task SwitchingToTeamKataKeepsTheCategorySeparateFromIndividualRosterEntries()
    {
        CreateCategoryRequest? stored = null;
        var editor = new ApiCategoryEditorViewModel(null, [First, Second], request =>
        {
            stored = request;
            return Task.CompletedTask;
        });
        editor.Name = "Team Kata juniorki";
        editor.Roster.Choices[0].IsSelected = true;
        editor.BeltOptions.Single(option => option.Value == Belts.Kyu8).IsSelected = true;
        editor.Discipline = CategoryType.Team;

        await Assert.IsType<AsyncRelayCommand>(editor.SaveCommand).ExecuteAsync();

        Assert.NotNull(stored);
        Assert.Equal(CategoryType.Team, stored.CategoryType);
        Assert.Empty(stored.ParticipantIds!);
    }
}
