using System;
using System.Collections.Generic;
using System.ComponentModel;

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

public class DictionaryEntry : INotifyPropertyChanged
{
    private bool _isExpanded = true;

    public string PartOfSpeech { get; set; } = string.Empty; // e.g., "noun", "verb"
    public DictionaryEntryType EntryType { get; set; } = DictionaryEntryType.Translation;
    public List<DefinitionEntry> Definitions { get; set; } = new List<DefinitionEntry>();

    /// <summary>
    /// Gets the header text based on entry type for UI display.
    /// </summary>
    public string HeaderText => EntryType switch
    {
        DictionaryEntryType.Translation => PartOfSpeech,
        DictionaryEntryType.Definition => $"{PartOfSpeech} • definitions",
        DictionaryEntryType.Example => "Examples",
        _ => PartOfSpeech
    };

    /// <summary>
    /// Whether this section is expanded (showing definitions).
    /// </summary>
    public bool IsExpanded
    {
        get => _isExpanded;
        set
        {
            if (_isExpanded != value)
            {
                _isExpanded = value;
                PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(IsExpanded)));
            }
        }
    }

    /// <summary>
    /// Initialize expansion state based on entry type.
    /// Translations start expanded, Definition/Example start collapsed.
    /// </summary>
    public void InitializeExpandedState()
    {
        _isExpanded = EntryType == DictionaryEntryType.Translation;
    }

    public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
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
