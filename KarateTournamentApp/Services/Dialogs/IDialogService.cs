namespace KarateTournamentApp.Services.Dialogs
{
    public enum DialogButtons
    {
        Ok,
        OkCancel,
        YesNo
    }

    public enum DialogIcon
    {
        None,
        Information,
        Warning,
        Error,
        Question
    }

    public enum DialogResult
    {
        Ok,
        Cancel,
        Yes,
        No
    }

    public interface IDialogService
    {
        DialogResult ShowMessage(string message, string title, DialogButtons buttons = DialogButtons.Ok, DialogIcon icon = DialogIcon.Information);
        string? ShowTextInput(string title, string prompt, string defaultValue = "");
        string? ShowOpenFileDialog(string title, string filter);
        string? ShowSaveFileDialog(string title, string filter, string defaultFileName);
    }
}
