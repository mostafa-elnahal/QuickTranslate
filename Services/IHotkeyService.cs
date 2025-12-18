using System;
using System.Windows;

namespace QuickTranslate.Services;

public interface IHotkeyService : IDisposable
{
    /// <summary>
    /// Event triggered when the registered hotkey is pressed.
    /// </summary>
    event EventHandler HotkeyPressed;

    /// <summary>
    /// Registers the global hotkey for the specified window.
    /// </summary>
    /// <param name="window">The window to attach the hotkey to.</param>
    void Register(Window window);
    
    /// <summary>
    /// Unregisters the global hotkey.
    /// </summary>
    void Unregister();
}
