using System.Windows;

namespace QuickTranslate.Services;

/// <summary>
/// Implementation of the DialogService using standard WPF MessageBox.
/// </summary>
public class DialogService : IDialogService
{
    /// <inheritdoc/>
    public void ShowWarning(string message, string title)
    {
        MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Warning);
    }

    /// <inheritdoc/>
    public void ShowError(string message, string title)
    {
        MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Error);
    }

    /// <inheritdoc/>
    public void ShowInfo(string message, string title)
    {
        MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Information);
    }
}
