using System;

namespace QuickTranslate.Services.Input;

/// <summary>
/// Monitors for changes to the foreground window across the OS.
/// Useful for detecting when the user switches applications or context.
/// </summary>
public interface IForegroundWindowMonitorService : IDisposable
{
    /// <summary>
    /// Raised when the active, foreground window changes.
    /// The payload is the HWND of the new foreground window.
    /// </summary>
    event Action<IntPtr>? ForegroundWindowChanged;

    /// <summary>
    /// Starts monitoring for foreground window changes.
    /// </summary>
    void Start();

    /// <summary>
    /// Stops monitoring.
    /// </summary>
    void Stop();
}
