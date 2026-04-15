namespace QuickTranslate.Services;

public interface IDialogService
{
    void ShowWarning(string message, string title);
    void ShowError(string message, string title);
    void ShowInfo(string message, string title);
}
