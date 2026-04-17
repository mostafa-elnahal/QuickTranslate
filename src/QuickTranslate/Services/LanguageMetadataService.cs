using System.Globalization;

namespace QuickTranslate.Services;

/// <summary>
/// Handles language names and codes (e.g. converting "en" to "English").
/// </summary>
public interface ILanguageMetadataService
{
    /// <summary> Gets the display name for a language code (e.g. "en" -> "English") </summary>
    string GetLanguageName(string languageCode);

    /// <summary> Maps a full language name back to its ISO code (e.g. "English" -> "en") </summary>
    string MapToIso6391(string? languageName);
}

/// <summary>
/// Helper service for all things language-metadata related.
/// </summary>
public class LanguageMetadataService : ILanguageMetadataService
{
    public string GetLanguageName(string languageCode)
    {
        if (string.IsNullOrEmpty(languageCode)) return "Unknown";
        try
        {
            var culture = new CultureInfo(languageCode);
            return culture.DisplayName;
        }
        catch
        {
            return languageCode.ToUpper();
        }
    }

    public string MapToIso6391(string? languageName)
    {
        return QuickTranslate.Helpers.LanguageHelper.MapToIso6391(languageName);
    }
}
