using System;
using System.Collections.Generic;
namespace QuickTranslate.Models;

public class TranslationModel
{
    private static readonly HashSet<string> RtlLanguages = Constants.Languages.RtlLanguages;

    public string OriginalText { get; set; } = string.Empty;
    public string MainTranslation { get; set; } = string.Empty;
    public string Phonetic { get; set; } = string.Empty;
    public string ProviderName { get; set; } = "Google";
    public string SourceLanguage { get; set; } = "English";
    public string SourceLanguageCode { get; set; } = "en";
    public string TargetLanguage { get; set; } = "Arabic";
    public string TargetLanguageCode { get; set; } = "ar";
    public List<DictionaryEntry> DictionaryEntries { get; set; } = new List<DictionaryEntry>();

    /// <summary>
    /// Returns true if the target language is a Right-to-Left language.
    /// </summary>
    public bool IsRtl => RtlLanguages.Contains(TargetLanguage);

    /// <summary>
    /// Returns true if the source language is a Right-to-Left language.
    /// </summary>
    public bool IsSourceRtl => RtlLanguages.Contains(SourceLanguage);

    /// <summary>
    /// Returns true if the original text is a single word (no spaces).
    /// Used to determine if pronunciation section should be shown.
    /// </summary>
    public bool IsSingleWord => !string.IsNullOrWhiteSpace(OriginalText)
        && !OriginalText.Trim().Contains(' ');

    /// <summary>
    /// Returns true if phonetics data is available.
    /// </summary>
    public bool HasPhonetics => !string.IsNullOrWhiteSpace(Phonetic);

}
