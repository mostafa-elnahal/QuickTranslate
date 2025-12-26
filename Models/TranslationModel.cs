using System;
using System.Collections.Generic;

namespace QuickTranslate.Models;

public class TranslationModel
{
    private static readonly HashSet<string> RtlLanguages = new(StringComparer.OrdinalIgnoreCase)
    {
        "Arabic", "Hebrew", "Persian", "Urdu", "Pashto", "Sindhi", "Kurdish"
    };

    public string OriginalText { get; set; } = string.Empty;
    public string MainTranslation { get; set; } = string.Empty;
    public string Phonetic { get; set; } = string.Empty;
    public string ProviderName { get; set; } = "Google";
    public string SourceLanguage { get; set; } = "English";
    public string TargetLanguage { get; set; } = "Arabic";
    public List<DictionaryEntry> DictionaryEntries { get; set; } = new List<DictionaryEntry>();

    /// <summary>
    /// Returns true if the target language is a Right-to-Left language.
    /// </summary>
    public bool IsRtl => RtlLanguages.Contains(TargetLanguage);

    /// <summary>
    /// Returns true if the source language is a Right-to-Left language.
    /// </summary>
    public bool IsSourceRtl => RtlLanguages.Contains(SourceLanguage);
}

public enum DictionaryEntryType
{
    Translation, // Standard translation variants (dt=bd)
    Definition,  // Source definitions (dt=md)
    Example      // Usage examples (dt=ex)
}

public class DictionaryEntry
{
    public string PartOfSpeech { get; set; } = string.Empty; // e.g., "noun", "verb"
    public DictionaryEntryType EntryType { get; set; } = DictionaryEntryType.Translation;
    public List<DefinitionEntry> Definitions { get; set; } = new List<DefinitionEntry>();
}

public class DefinitionEntry
{
    public string MainTerm { get; set; } = string.Empty;
    public List<string> Synonyms { get; set; } = new List<string>();

    // Helper for XAML binding
    public string SynonymsText => Synonyms != null && Synonyms.Count > 0
        ? $"({string.Join(", ", Synonyms)})"
        : string.Empty;
}
