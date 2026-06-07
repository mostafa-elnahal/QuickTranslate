using System;

namespace QuickTranslate.Services;

public interface ITrayIconService : IDisposable
{


    /// <summary>
    /// Event triggered when the "Exit" menu item is clicked.
    /// </summary>
    event EventHandler ExitRequested;

    /// <summary>
    /// Event triggered when the "Settings..." menu item is clicked.
    /// </summary>
    event EventHandler SettingsRequested;

    /// <summary>
    /// Event triggered when the "OCR Text" menu item is clicked.
    /// </summary>
    event EventHandler OcrRequested;

    /// <summary>
    /// Event triggered when the "Show Main Window" menu item is clicked.
    /// </summary>
    event EventHandler ShowMainWindowRequested;

    /// <summary>
    /// Initializes and shows the tray icon.
    /// </summary>
    void Initialize();
    
    /// <summary>
    /// Updates the visibility of the tray icon.
    /// </summary>
    void SetVisible(bool visible);
}
