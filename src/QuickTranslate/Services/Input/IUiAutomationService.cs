namespace QuickTranslate.Services.Input;

/// <summary>
/// Uses UI Automation API to read selected text from the focused application
/// without touching the clipboard. Falls back caller should provide clipboard fallback.
/// </summary>
public interface IUiAutomationService
{
    /// <summary>
    /// Tries to get the currently selected text via UIA.
    /// Returns null if UIA fails or no text is selected.
    /// </summary>
    string? TryGetSelectedText();
}
