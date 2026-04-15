using System;
using System.Linq;

namespace QuickTranslate.Helpers;

public static class LanguageHelper
{
    private static readonly System.Collections.Generic.Dictionary<string, string> LanguageMap = new(System.StringComparer.OrdinalIgnoreCase)
    {
        { "english", "en" },
        { "arabic", "ar" },
        { "french", "fr" },
        { "german", "de" },
        { "spanish", "es" },
        { "italian", "it" },
        { "portuguese", "pt" },
        { "russian", "ru" },
        { "japanese", "ja" },
        { "korean", "ko" },
        { "chinese", "zh" },
        { "chinese (simplified)", "zh" },
        { "chinese (traditional)", "zh" }
    };

    public static string MapToIso6391(string? languageName)
    {
        if (string.IsNullOrWhiteSpace(languageName)) return "en";
        
        if (LanguageMap.TryGetValue(languageName.Trim(), out var code))
        {
            return code;
        }

        // Potential fallback: Try CultureInfo lookup
        try
        {
            var cultures = System.Globalization.CultureInfo.GetCultures(System.Globalization.CultureTypes.NeutralCultures);
            var match = cultures.FirstOrDefault(c => c.EnglishName.Contains(languageName, StringComparison.OrdinalIgnoreCase));
            if (match != null) return match.TwoLetterISOLanguageName;
        }
        catch { /* Ignore */ }

        return "en"; // Default fallback
    }
}
