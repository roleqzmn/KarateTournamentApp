using KarateTournamentApp.Models;
using KarateTournamentApp.ViewModels;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace KarateTournamentApp.Services
{
    public class ImportService
    {
        private readonly JsonService _jsonService;
        private readonly ExcelImportService _excelImportService;

        public ImportService()
        {
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
            var openFileDialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = fileFilter,
                Title = dialogTitle
            };

            if (openFileDialog.ShowDialog() != true)
            {
                return;
            }

            try
            {
                var importedParticipants = await importParticipants(openFileDialog.FileName);

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

                    MessageBox.Show($"Successfully imported {addedCount} of {importedParticipants.Count} participants!\n\n" +
                        $"Skipped: {importedParticipants.Count - addedCount} (duplicates or invalid data)",
                        "Import Completed", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    MessageBox.Show("File does not contain any valid participant data.",
                        "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error importing data from {formatName}:\n{ex.Message}",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public async Task CreateSampleExcelFileAsync()
        {
            var saveFileDialog = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "Excel files (*.xlsx)|*.xlsx",
                Title = "Save Excel Template",
                FileName = "participants_template.xlsx"
            };

            if (saveFileDialog.ShowDialog() == true)
            {
                try
                {
                    await _excelImportService.CreateSampleExcelFileAsync(saveFileDialog.FileName);
                    MessageBox.Show($"Excel template has been saved!\n\nPath: {saveFileDialog.FileName}",
                        "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error creating template:\n{ex.Message}",
                        "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        public async Task ImportDataAsync(CategoryManager categoryManager, ObservableCollection<Participant> allParticipants)
        {
            var openFileDialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*",
                Title = "Import Tournament Data"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                try
                {
                    var importedCategories = await _jsonService.LoadTournamentDataAsync(openFileDialog.FileName);

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

                        MessageBox.Show($"Successfully imported {importedCategories.Count} categories and {allParticipants.Count} participants!",
                            "Import Completed", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    else
                    {
                        MessageBox.Show("File does not contain any data.",
                            "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error importing data:\n{ex.Message}",
                        "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
    }
}
