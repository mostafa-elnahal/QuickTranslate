using System.Windows;

namespace QuickTranslate.Services;

/// <summary>
/// Service for managing window size persistence.
/// </summary>
public interface IWindowSizingService
{
    /// <summary>
    /// Applies saved size or initial constraints to the window.
    /// Call this in OnSourceInitialized.
    /// </summary>
    void ApplySize(Window window);

    /// <summary>
    /// Saves current window size to settings.
    /// Call this when window is resized.
    /// </summary>
    void SaveSize(Window window);
}
