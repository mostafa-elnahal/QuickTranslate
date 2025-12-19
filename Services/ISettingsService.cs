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
    /// Saves current settings to persistent storage.
    /// </summary>
    void Save();
}
