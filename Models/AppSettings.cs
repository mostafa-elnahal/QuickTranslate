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
    /// The hotkey combination string for translation (e.g., "Ctrl+Q").
    /// </summary>
    public string Hotkey { get; set; } = "Ctrl+Q";

    /// <summary>
    /// The hotkey combination string for pronunciation practice (e.g., "Ctrl+Shift+P").
    /// </summary>
    public string PronunciationHotkey { get; set; } = "Ctrl+Shift+P";

    /// <summary>
    /// Saved window width. Null = first launch (use default).
    /// </summary>
    public double? SavedWindowWidth { get; set; } = null;

    /// <summary>
    /// Saved window height. Null = first launch (use SizeToContent with MaxHeight).
    /// </summary>
    public double? SavedWindowHeight { get; set; } = null;

    /// <summary>
    /// Saved pronunciation window width. Null = first launch (use default).
    /// </summary>
    public double? SavedPronunciationWindowWidth { get; set; } = null;

    /// <summary>
    /// Saved pronunciation window height. Null = first launch (use SizeToContent with MaxHeight).
    /// </summary>
    public double? SavedPronunciationWindowHeight { get; set; } = null;

    /// <summary>
    /// Selected pronunciation provider (e.g., "Google", "Gemini").
    /// </summary>
    public string PronunciationProvider { get; set; } = "Google";

    /// <summary>
    /// API Key for Gemini pronunciation provider (in-memory only).
    /// </summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public string GeminiApiKey { get; set; } = string.Empty;

    /// <summary>
    /// Encrypted API Key for storage.
    /// </summary>
    public string EncryptedGeminiApiKey { get; set; } = string.Empty;

    /// <summary>
    /// Font size for the main translation text.
    /// </summary>
    public double FontSize { get; set; } = 18;

    /// <summary>
    /// Font family for the main translation text.
    /// </summary>
    public string FontFamily { get; set; } = "Segoe UI";

    /// <summary>
    /// Font weight for the main translation text.
    /// </summary>
    public string FontWeight { get; set; } = "Medium";

    /// <summary>
    /// Show pronunciation section for single-word translations.
    /// </summary>
    public bool ShowPronunciation { get; set; } = true;
}
