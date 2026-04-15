using System;
using System.Windows;

namespace QuickTranslate.Services;

public interface IHotkeyService : IDisposable
{
    /// <summary>
    /// Event triggered when a registered hotkey is pressed.
    /// Returns the ID of the hotkey.
    /// </summary>
    event EventHandler<int> HotkeyPressed;

    /// <summary>
    /// Registers a global hotkey with a specific ID.
    /// If an ID is already registered, it will be overwritten.
    /// </summary>
    /// <param name="id">Unique identifier for the hotkey.</param>
    /// <param name="hotkeyString">The hotkey string (e.g., "Ctrl+Shift+T").</param>
    /// <param name="window">The window to attach the hotkey to.</param>
    /// <returns>True if registration was successful, false otherwise.</returns>
    bool Register(int id, string hotkeyString, Window window);

    /// <summary>
    /// Unregisters the hotkey with the specified ID.
    /// </summary>
    /// <param name="id">The unique identifier of the hotkey.</param>
    void Unregister(int id);

    /// <summary>
    /// Unregisters all hotkeys.
    /// </summary>
    void UnregisterAll();
}
