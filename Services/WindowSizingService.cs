using System;
using System.Windows;

namespace QuickTranslate.Services;

/// <summary>
/// Service for managing window size persistence.
/// On first launch: Uses SizeToContent with MaxHeight constraint.
/// On subsequent launches: Restores saved dimensions.
/// </summary>
public class WindowSizingService : IWindowSizingService
{
    private readonly ISettingsService _settingsService;

    /// <summary>
    /// Default max height for initial sizing (in DIPs).
    /// </summary>
    private const double DefaultMaxHeight = 400;

    /// <summary>
    /// Default max width for initial sizing (in DIPs).
    /// Keeps text readable without being too wide.
    /// </summary>
    private const double DefaultMaxWidth = 500;

    /// <summary>
    /// Minimum width to ensure header buttons are visible.
    /// </summary>
    private const double MinWidth = 280;

    public WindowSizingService(ISettingsService settingsService)
    {
        _settingsService = settingsService;
    }

    public void ApplySize(Window window)
    {
        var settings = _settingsService.Settings;

        if (settings.SavedWindowWidth.HasValue && settings.SavedWindowHeight.HasValue)
        {
            // Subsequent launch: Restore saved size
            window.SizeToContent = SizeToContent.Manual;
            window.Width = settings.SavedWindowWidth.Value;
            window.Height = settings.SavedWindowHeight.Value;
        }
        else
        {
            // First launch: Smart sizing - wrap around content with constraints
            // Window sizes to fit content, but capped at max dimensions
            window.SizeToContent = SizeToContent.WidthAndHeight;
            window.MinWidth = MinWidth;
            window.MaxWidth = DefaultMaxWidth;
            window.MaxHeight = DefaultMaxHeight;

            // Clear constraints after first layout so user can resize freely
            EventHandler onContentRendered = null!;
            onContentRendered = (sender, e) =>
            {
                window.ContentRendered -= onContentRendered;
                // Remove max constraints to allow free resizing
                window.MaxWidth = double.PositiveInfinity;
                window.MaxHeight = double.PositiveInfinity;
                // Switch to manual sizing for user control
                window.SizeToContent = SizeToContent.Manual;
            };
            window.ContentRendered += onContentRendered;
        }
    }

    public void SaveSize(Window window)
    {
        // Only save if window has a reasonable size
        if (window.ActualWidth > 0 && window.ActualHeight > 0)
        {
            var settings = _settingsService.Settings;
            settings.SavedWindowWidth = window.ActualWidth;
            settings.SavedWindowHeight = window.ActualHeight;
            _settingsService.Save();
        }
    }
}
