using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using GTranslate.Results;
using GTranslate.Translators;
using GTranslate;
using QuickTranslate.Models;

namespace QuickTranslate.Services.Translators;

/// <summary>
/// A custom Google Translator implementation that retrieves rich dictionary data.
/// </summary>
public class GoogleTranslator : ITranslator, IDisposable
{
    private const string ApiEndpoint = "https://translate.googleapis.com/translate_a/single";
    private readonly HttpClient _httpClient;
    private bool _disposed;

    public string Name => "Google";

    public GoogleTranslator()
    {
        _httpClient = new HttpClient();
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/96.0.4664.45 Safari/537.36");
    }

    public async Task<ITranslationResult> TranslateAsync(string text, string toLanguage, string? fromLanguage = null)
    {
        return await TranslateAsync(text, Language.GetLanguage(toLanguage), fromLanguage != null ? Language.GetLanguage(fromLanguage) : null);
    }

    public async Task<ITranslationResult> TranslateAsync(string text, ILanguage toLanguage, ILanguage? fromLanguage = null)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(toLanguage);

        string fromLangCode = fromLanguage?.ISO6391 ?? "auto";
        string toLangCode = toLanguage.ISO6391;

        // dt parameters explain:
        // t: translation
        // bd: basic dictionary (parts of speech, terms)
        // md: definitions (deep definitions, examples)
        // ss: synonyms (grouped)
        // ex: examples (usage sentences)
        // at: alternate translations
        // rw: related words
        // rm: transliteration (source/target)
        string url = $"{ApiEndpoint}?client=gtx&sl={fromLangCode}&tl={toLangCode}&dt=t&dt=bd&dt=md&dt=ss&dt=ex&dt=rm&dj=1&source=input&q={Uri.EscapeDataString(text)}";

        using var response = await _httpClient.GetAsync(url);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        // 1. Main Translation
        string translation = "";
        string? transliteration = null;
        if (root.TryGetProperty("sentences", out var sentences))
        {
            foreach (var sentence in sentences.EnumerateArray())
            {
                if (sentence.TryGetProperty("trans", out var trans))
                {
                    translation += trans.GetString() ?? "";
                }
                if (sentence.TryGetProperty("translit", out var translit))
                {
                    transliteration = translit.GetString();
                }
            }
        }

        string sourceLangDetected = root.TryGetProperty("src", out var src) ? src.GetString() ?? "en" : "en";

        // 2. Parse Dictionary Data
        var dictionaryEntries = new List<DictionaryEntry>();

        // "dict" property (Parts of Speech & Basic Terms) - THESE ARE TRANSLATIONS
        // This is usually the best source for target-language alternatives
        if (root.TryGetProperty("dict", out var dict))
        {
            foreach (var entry in dict.EnumerateArray())
            {
                var dictEntry = new DictionaryEntry { EntryType = DictionaryEntryType.Translation };
                if (entry.TryGetProperty("pos", out var pos))
                {
                    dictEntry.PartOfSpeech = pos.GetString() ?? "";
                }

                // In "dict", we have "terms" (list of words) and "entry" (deep details with reverse translation)
                if (entry.TryGetProperty("entry", out var entryDetail))
                {
                    foreach (var details in entryDetail.EnumerateArray())
                    {
                        var defEntry = new DefinitionEntry();
                        if (details.TryGetProperty("word", out var word))
                        {
                            defEntry.MainTerm = word.GetString() ?? "";
                        }

                        // Reverse translations
                        if (details.TryGetProperty("reverse_translation", out var reverseTrans))
                        {
                            foreach (var rt in reverseTrans.EnumerateArray())
                            {
                                defEntry.Synonyms.Add(rt.GetString() ?? "");
                            }
                        }

                        dictEntry.Definitions.Add(defEntry);
                    }
                }
                // Fallback to "terms" if "entry" is missing/empty but terms exist
                else if (entry.TryGetProperty("terms", out var terms))
                {
                    foreach (var term in terms.EnumerateArray())
                    {
                        dictEntry.Definitions.Add(new DefinitionEntry { MainTerm = term.GetString() ?? "" });
                    }
                }

                if (dictEntry.Definitions.Count > 0)
                {
                    dictionaryEntries.Add(dictEntry);
                }
            }
        }

        // "definitions" property (Source language definitions) - THESE ARE DEFINITIONS
        // Useful specifically when the user wants to understand the SOURCE word better
        if (root.TryGetProperty("definitions", out var definitions))
        {
            foreach (var defGroup in definitions.EnumerateArray())
            {
                string pos = defGroup.TryGetProperty("pos", out var p) ? p.GetString() ?? "" : "";

                // CHECK: Should we merge with existing "Translation" entries? 
                // NO, because the data structure is semantically different. 
                // We create a new entry for "Definitions".
                var defDictEntry = new DictionaryEntry { PartOfSpeech = pos, EntryType = DictionaryEntryType.Definition };

                if (defGroup.TryGetProperty("entry", out var entries))
                {
                    foreach (var entry in entries.EnumerateArray())
                    {
                        if (entry.TryGetProperty("gloss", out var gloss))
                        {
                            var defEntry = new DefinitionEntry
                            {
                                MainTerm = gloss.GetString() ?? ""
                            };

                            if (entry.TryGetProperty("example", out var ex))
                            {
                                defEntry.Synonyms.Add($"\"{ex.GetString()}\"");
                            }

                            defDictEntry.Definitions.Add(defEntry);
                        }
                    }
                }

                if (defDictEntry.Definitions.Count > 0)
                {
                    dictionaryEntries.Add(defDictEntry);
                }
            }
        }

        // "examples" property (Usage examples) - THESE ARE EXAMPLES
        if (root.TryGetProperty("examples", out var examples))
        {
            if (examples.TryGetProperty("example", out var exList))
            {
                var examplesEntry = new DictionaryEntry { PartOfSpeech = "Examples", EntryType = DictionaryEntryType.Example };
                foreach (var ex in exList.EnumerateArray())
                {
                    if (ex.TryGetProperty("text", out var textVal))
                    {
                        // Using raw html stripping if needed, but usually it comes with <b> tags
                        string cleanText = textVal.GetString()?.Replace("<b>", "").Replace("</b>", "") ?? "";
                        examplesEntry.Definitions.Add(new DefinitionEntry { MainTerm = cleanText });
                    }
                }
                if (examplesEntry.Definitions.Count > 0)
                {
                    dictionaryEntries.Add(examplesEntry);
                }
            }
        }


        return new QuickTranslate.Models.GoogleTranslationResult(
            translation,
            text,
            toLanguage as Language ?? Language.GetLanguage(toLangCode),
            Language.GetLanguage(sourceLangDetected),
            Name,
            dictionaryEntries,
            transliteration
        );
    }

    public Task<ITransliterationResult> TransliterateAsync(string text, string toLanguage, string? fromLanguage = null)
    {
        throw new NotImplementedException();
    }

    public Task<ITransliterationResult> TransliterateAsync(string text, ILanguage toLanguage, ILanguage? fromLanguage = null)
    {
        throw new NotImplementedException();
    }

    public Task<ILanguage> DetectLanguageAsync(string text)
    {
        throw new NotImplementedException();
    }

    public bool IsLanguageSupported(string language)
    {
        return true;
    }

    public bool IsLanguageSupported(ILanguage language)
    {
        return true;
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _httpClient.Dispose();
            _disposed = true;
        }
    }
}
