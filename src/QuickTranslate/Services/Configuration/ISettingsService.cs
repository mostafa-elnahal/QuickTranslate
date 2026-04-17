using System;
using System.Threading.Tasks;
using QuickTranslate.Models;

namespace QuickTranslate.Services;

/// <summary>
/// Interface for managing application settings persistence.
/// </summary>
public interface ISettingsService
{
    /// <summary>
    /// Gets the current application settings.
    /// </summary>
    AppSettings Settings { get; }

    /// <summary>
    /// Loads settings from persistent storage.
    /// </summary>
    void Load();

    /// <summary>
    /// Loads settings from persistent storage asynchronously.
    /// </summary>
    Task LoadAsync();

    /// <summary>
    /// Saves current settings to persistent storage.
    /// </summary>
    void Save();

    /// <summary>
    /// Saves current settings to persistent storage asynchronously.
    /// </summary>
    Task SaveAsync();

    /// <summary>
    /// Raised when settings are saved.
    /// </summary>
    event EventHandler SettingsChanged;
}
