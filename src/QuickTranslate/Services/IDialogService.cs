namespace QuickTranslate.Services;

/// <summary>
/// Service for displaying system dialogs and message boxes.
/// Abstracted to allow ViewModels to trigger UI notifications without direct framework dependencies.
/// </summary>
public interface IDialogService
{
    /// <summary>
    /// Displays a warning message box with an OK button and Warning icon.
    /// </summary>
    /// <param name="message">The message to display.</param>
    /// <param name="title">The title of the dialog window.</param>
    void ShowWarning(string message, string title);

    /// <summary>
    /// Displays an error message box with an OK button and Error icon.
    /// </summary>
    /// <param name="message">The message to display.</param>
    /// <param name="title">The title of the dialog window.</param>
    void ShowError(string message, string title);

    /// <summary>
    /// Displays an informational message box with an OK button and Information icon.
    /// </summary>
    /// <param name="message">The message to display.</param>
    /// <param name="title">The title of the dialog window.</param>
    void ShowInfo(string message, string title);
}
