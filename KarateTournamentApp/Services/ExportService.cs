using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using KarateTournamentApp.Services.Dialogs;

namespace KarateTournamentApp.Services
{
    public class ExportService
    {
        private readonly JsonService _jsonService;
        private readonly IDialogService _dialogService;

        public ExportService(IDialogService dialogService)
        {
            _dialogService = dialogService ?? throw new ArgumentNullException(nameof(dialogService));
            _jsonService = new JsonService();
        }

        public async Task ExportDataAsync(string filePath, CategoryManager categoryManager)
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                throw new ArgumentException("An export file path is required.", nameof(filePath));
            }

            try
            {
                await _jsonService.SaveTournamentDataAsync(filePath, categoryManager.DefinedCategories);
                _dialogService.ShowMessage($"Pomyślnie wyeksportowano {categoryManager.DefinedCategories.Count} kategorii!",
                    "Export zakończony", DialogButtons.Ok, DialogIcon.Information);
            }
            catch (Exception ex)
            {
                _dialogService.ShowMessage($"Błąd podczas exportu danych:\n{ex.Message}",
                    "Błąd", DialogButtons.Ok, DialogIcon.Error);
            }
        }
    }
}
