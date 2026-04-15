using System;
using System.Collections.Generic;

namespace QuickTranslate.Models;

public class PronunciationData
{
    public string OriginalText { get; set; } = string.Empty;
    public string Phonetics { get; set; } = string.Empty;
    public Uri? AudioUri { get; set; }
    public string DetectedLanguageCode { get; set; } = "en";
    public List<SyllableItem> Syllables { get; set; } = new();
}
