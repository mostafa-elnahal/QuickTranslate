using System.Collections.Generic;

namespace QuickTranslate.Models;

public class DefinitionEntry
{
    public string MainTerm { get; set; } = string.Empty;
    public List<string> Synonyms { get; set; } = new List<string>();

    // Helper for XAML binding
    public string SynonymsText => Synonyms != null && Synonyms.Count > 0
        ? $"({string.Join(", ", Synonyms)})"
        : string.Empty;
}
