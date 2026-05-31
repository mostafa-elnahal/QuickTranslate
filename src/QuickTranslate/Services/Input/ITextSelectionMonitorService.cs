using System;

namespace QuickTranslate.Services.Input;

/// <summary>
/// Service that monitors for text selection across the OS.
/// When the user selects text (drag-release), it captures the selection and raises an event.
/// </summary>
public interface ITextSelectionMonitorService : IDisposable
{
    /// <summary>
    /// Raised when text has been selected via mouse drag-release.
    /// Payload is the selected text.
    /// </summary>
    event Action<string>? TextSelected;

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
