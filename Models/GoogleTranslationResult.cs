using System.Collections.Generic;
using GTranslate.Results;
using GTranslate.Translators;
using GTranslate;

namespace QuickTranslate.Models;

/// <summary>
/// A custom translation result that includes rich dictionary data from Google.
/// </summary>
public class GoogleTranslationResult : ITranslationResult<Language>, ITranslationResult
{
    public string Translation { get; }
    public string Source { get; }
    public Language TargetLanguage { get; }
    public Language SourceLanguage { get; }
    public string Service { get; }
    public string? Transliteration { get; }

    // Rich Dictionary Data
    public List<DictionaryEntry> DictionaryEntries { get; }

    public GoogleTranslationResult(
        string translation,
        string source,
        Language targetLanguage,
        Language sourceLanguage,
        string service,
        List<DictionaryEntry>? dictionaryEntries = null,
        string? transliteration = null)
    {
        Translation = translation;
        Source = source;
        TargetLanguage = targetLanguage;
        SourceLanguage = sourceLanguage;
        Service = service;
        DictionaryEntries = dictionaryEntries ?? new List<DictionaryEntry>();
        Transliteration = transliteration;
    }

    ILanguage ITranslationResult<ILanguage>.SourceLanguage => SourceLanguage;
    ILanguage ITranslationResult<ILanguage>.TargetLanguage => TargetLanguage;
}
