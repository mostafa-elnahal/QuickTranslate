using System;

namespace QuickTranslate.Services;

public interface ITrayIconService : IDisposable
{
    /// <summary>
    /// Event triggered when the "Show Window" menu item or double-click occurs.
    /// </summary>
    event EventHandler ShowWindowRequested;

    /// <summary>
    /// Event triggered when the "Exit" menu item is clicked.
    /// </summary>
    event EventHandler ExitRequested;

    /// <summary>
    /// Initializes and shows the tray icon.
    /// </summary>
    void Initialize();
    
    /// <summary>
    /// Updates the visibility of the tray icon.
    /// </summary>
    void SetVisible(bool visible);
}
