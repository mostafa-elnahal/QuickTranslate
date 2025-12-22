namespace QuickTranslate.Models;

/// <summary>
/// Represents the application's persistent settings.
/// </summary>
public class AppSettings
{
    /// <summary>
    /// Whether to start the application with Windows.
    /// </summary>
    public bool StartWithWindows { get; set; } = false;

    /// <summary>
    /// Window opacity (0.0 to 1.0).
    /// </summary>
    public double WindowOpacity { get; set; } = 0.9;

    /// <summary>
    /// Default source language code (e.g., "auto", "en", "ar").
    /// </summary>
    public string DefaultSourceLanguage { get; set; } = "auto";

    /// <summary>
    /// Default target language code (e.g., "en", "ar").
    /// </summary>
    public string DefaultTargetLanguage { get; set; } = "en";

    /// <summary>
    /// Default translation provider name.
    /// </summary>
    public string DefaultProvider { get; set; } = "Google";

    /// <summary>
    /// The hotkey combination string (e.g., "F1").
    /// </summary>
    public string Hotkey { get; set; } = "F1";

    /// <summary>
    /// Saved window width. Null = first launch (use default).
    /// </summary>
    public double? SavedWindowWidth { get; set; } = null;

    /// <summary>
    /// Saved window height. Null = first launch (use SizeToContent with MaxHeight).
    /// </summary>
    public double? SavedWindowHeight { get; set; } = null;

    /// <summary>
    /// Font size for the main translation text.
    /// </summary>
    public double FontSize { get; set; } = 18;
}
