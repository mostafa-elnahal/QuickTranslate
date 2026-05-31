namespace QuickTranslate.Models;

/// <summary>
/// Determines how the floating toolbar is displayed.
/// </summary>
public enum ToolbarDisplayMode
{
    /// <summary>
    /// Text selection mode: starts collapsed as a small icon, expands on hover.
    /// </summary>
    Selection,

    /// <summary>
    /// OCR capture mode: starts fully expanded under the selection rectangle.
    /// </summary>
    Ocr
}
