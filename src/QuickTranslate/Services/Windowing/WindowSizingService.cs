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

    public void ApplySize(Window window, WindowType type = WindowType.Translation)
    {
        var settings = _settingsService.Settings;
        double? savedWidth = type == WindowType.Translation ? settings.SavedWindowWidth : settings.SavedPronunciationWindowWidth;
        double? savedHeight = type == WindowType.Translation ? settings.SavedWindowHeight : settings.SavedPronunciationWindowHeight;

        if (savedWidth.HasValue && savedHeight.HasValue)
        {
            // Subsequent launch: Restore saved size
            window.SizeToContent = SizeToContent.Manual;
            window.Width = savedWidth.Value;
            window.Height = savedHeight.Value;
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

    public void SaveSize(Window window, WindowType type = WindowType.Translation)
    {
        // Only save if window has a reasonable size
        if (window.ActualWidth > 0 && window.ActualHeight > 0)
        {
            var settings = _settingsService.Settings;
            if (type == WindowType.Translation)
            {
                settings.SavedWindowWidth = window.ActualWidth;
                settings.SavedWindowHeight = window.ActualHeight;
            }
            else
            {
                settings.SavedPronunciationWindowWidth = window.ActualWidth;
                settings.SavedPronunciationWindowHeight = window.ActualHeight;
            }
            _settingsService.Save();
        }
    }
}
