using System.Collections.Generic;
using System.ComponentModel;

namespace QuickTranslate.Models;

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
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsExpanded)));
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

    public event PropertyChangedEventHandler? PropertyChanged;
}
