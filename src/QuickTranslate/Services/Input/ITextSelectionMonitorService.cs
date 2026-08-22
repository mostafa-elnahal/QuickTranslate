using System;
using System.Windows;

namespace QuickTranslate.Services.Input;

/// <summary>
/// Service that monitors for text selection gestures across the OS.
/// When the user performs a text selection (drag-release, multi-click, keyboard selection),
/// it raises an event with cursor coordinates without performing eager capture.
/// </summary>
public interface ITextSelectionMonitorService : IDisposable
{
    /// <summary>
    /// Raised when a text selection gesture is detected.
    /// Payload is the physical screen coordinates of the cursor/selection.
    /// </summary>
    event Action<Point>? SelectionDetected;

    /// <summary>
    /// Gets or sets whether monitoring is active.
    /// </summary>
    bool IsEnabled { get; set; }

    /// <summary>
    /// Starts monitoring for text selection.
    /// </summary>
    void Start();

    /// <summary>
    /// Stops monitoring for text selection.
    /// </summary>
    void Stop();
}
