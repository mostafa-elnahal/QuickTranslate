namespace QuickTranslate.Services;

public interface IClipboardService
{
    /// <summary>
    /// Captures the current text selection using clipboard manipulation.
    /// Handles preserving and restoring the original clipboard content.
    /// </summary>
    /// <returns>The captured text, or empty string if failed.</returns>
    string CaptureSelection();

    /// <summary>
    /// Captures selection on an STA thread asynchronously.
    /// </summary>
    System.Threading.Tasks.Task<string> CaptureSelectionAsync();

    /// <summary>
    /// Safely sets text to the clipboard.
    /// </summary>
    void SetText(string text);
}
