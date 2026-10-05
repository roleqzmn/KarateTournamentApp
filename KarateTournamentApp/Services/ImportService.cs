using KarateTournamentApp.Models;
using KarateTournamentApp.ViewModels;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using KarateTournamentApp.Services.Dialogs;

namespace KarateTournamentApp.Services
{
    public class ImportService
    {
        private readonly JsonService _jsonService;
        private readonly ExcelImportService _excelImportService;
        private readonly IDialogService _dialogService;

        public ImportService(IDialogService dialogService)
        {
            _dialogService = dialogService ?? throw new ArgumentNullException(nameof(dialogService));
            _jsonService = new JsonService();
            _excelImportService = new ExcelImportService();
        }

        public Task ImportParticipantsFromExcelAsync(CategoryManager categoryManager, ObservableCollection<Participant> allParticipants, bool divideByBelt, bool divideByAge)
        {
            return ImportParticipantsAsync(
                categoryManager,
                allParticipants,
                divideByBelt,
                divideByAge,
                "Excel files (*.xlsx;*.xls)|*.xlsx;*.xls|All files (*.*)|*.*",
                "Import Participants from Excel",
                "Excel",
                _excelImportService.ImportParticipantsFromExcelAsync);
        }

        private async Task ImportParticipantsAsync(
            CategoryManager categoryManager,
            ObservableCollection<Participant> allParticipants,
            bool divideByBelt,
            bool divideByAge,
            string fileFilter,
            string dialogTitle,
            string formatName,
            Func<string, Task<List<Participant>>> importParticipants)
        {
            var filePath = _dialogService.ShowOpenFileDialog(dialogTitle, fileFilter);
            if (filePath == null)
            {
                return;
            }

            try
            {
                var importedParticipants = await importParticipants(filePath);

                if (importedParticipants != null && importedParticipants.Any())
                {
                    int addedCount = 0;
                    foreach (var participant in importedParticipants)
                    {
                        if (!allParticipants.Any(p => p.FirstName == participant.FirstName && p.LastName == participant.LastName))
                        {
                            categoryManager.AssignParticipant(participant, divideByBelt, divideByAge);
                            allParticipants.Add(participant);
                            addedCount++;
                        }
                    }

                    _dialogService.ShowMessage($"Successfully imported {addedCount} of {importedParticipants.Count} participants!\n\n" +
                        $"Skipped: {importedParticipants.Count - addedCount} (duplicates or invalid data)",
                        "Import Completed", DialogButtons.Ok, DialogIcon.Information);
                }
                else
                {
                    _dialogService.ShowMessage("File does not contain any valid participant data.",
                        "Error", DialogButtons.Ok, DialogIcon.Warning);
                }
            }
            catch (Exception ex)
            {
                _dialogService.ShowMessage($"Error importing data from {formatName}:\n{ex.Message}",
                    "Error", DialogButtons.Ok, DialogIcon.Error);
            }
        }

        public async Task CreateSampleExcelFileAsync()
        {
            var filePath = _dialogService.ShowSaveFileDialog(
                "Save Excel Template",
                "Excel files (*.xlsx)|*.xlsx",
                "participants_template.xlsx");
            if (filePath == null)
            {
                return;
            }

            try
            {
                await _excelImportService.CreateSampleExcelFileAsync(filePath);
                _dialogService.ShowMessage($"Excel template has been saved!\n\nPath: {filePath}",
                    "Success", DialogButtons.Ok, DialogIcon.Information);
            }
            catch (Exception ex)
            {
                _dialogService.ShowMessage($"Error creating template:\n{ex.Message}",
                    "Error", DialogButtons.Ok, DialogIcon.Error);
            }
        }

        public async Task ImportDataAsync(CategoryManager categoryManager, ObservableCollection<Participant> allParticipants)
        {
            var filePath = _dialogService.ShowOpenFileDialog(
                "Import Tournament Data",
                "JSON files (*.json)|*.json|All files (*.*)|*.*");
            if (filePath == null)
            {
                return;
            }

            try
            {
                var importedCategories = await _jsonService.LoadTournamentDataAsync(filePath);

                if (importedCategories != null && importedCategories.Any())
                {
                    categoryManager.DefinedCategories.Clear();
                    categoryManager.DefinedCategories.AddRange(importedCategories);

                    allParticipants.Clear();
                    foreach (var category in importedCategories)
                    {
                        foreach (var participant in category.Participants)
                        {
                            if (!allParticipants.Any(p => p.Id == participant.Id))
                            {
                                allParticipants.Add(participant);
                            }
                        }
                    }

                    _dialogService.ShowMessage($"Successfully imported {importedCategories.Count} categories and {allParticipants.Count} participants!",
                        "Import Completed", DialogButtons.Ok, DialogIcon.Information);
                }
                else
                {
                    _dialogService.ShowMessage("File does not contain any data.",
                        "Error", DialogButtons.Ok, DialogIcon.Warning);
                }
            }
            catch (Exception ex)
            {
                _dialogService.ShowMessage($"Error importing data:\n{ex.Message}",
                    "Error", DialogButtons.Ok, DialogIcon.Error);
            }
        }
    }
}
