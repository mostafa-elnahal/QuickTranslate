using System;
using System.Threading;
using System.Threading.Tasks;
using QuickTranslate.Models;
using QuickTranslate.Services.Translators;

namespace QuickTranslate.Services;

/// <summary>
/// Dictionary service that uses Google's free translation API for rich dictionary data.
/// Independent of the active translation provider — always uses Google for dictionary lookups.
/// </summary>
public class GoogleDictionaryService : IDictionaryService, IDisposable
{
    private readonly GoogleTranslator _translator;
    private bool _disposed;

    public GoogleDictionaryService()
    {
        _translator = new GoogleTranslator();
    }

    public async Task<TranslationModel> LookupAsync(string word, string targetLanguage, string? sourceLanguage = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(word))
            return new TranslationModel { ErrorMessage = "No word provided." };

        try
        {
            var result = await _translator.TranslateAsync(word.Trim(), targetLanguage, sourceLanguage)
                .WaitAsync(cancellationToken);

            if (result is GoogleTranslationResult googleResult)
            {
                return new TranslationModel
                {
                    OriginalText = word,
                    MainTranslation = googleResult.Translation,
                    SourceLanguageCode = googleResult.SourceLanguage.ISO6391,
                    TargetLanguageCode = googleResult.TargetLanguage.ISO6391,
                    SourceLanguage = googleResult.SourceLanguage.Name,
                    TargetLanguage = googleResult.TargetLanguage.Name,
                    ProviderName = "Google",
                    Phonetic = googleResult.SourceTransliteration ?? string.Empty,
                    DictionaryEntries = googleResult.DictionaryEntries
                };
            }

            return new TranslationModel
            {
                OriginalText = word,
                MainTranslation = result.Translation,
                SourceLanguageCode = result.SourceLanguage.ISO6391,
                TargetLanguageCode = result.TargetLanguage.ISO6391,
                SourceLanguage = result.SourceLanguage.Name,
                TargetLanguage = result.TargetLanguage.Name,
                ProviderName = "Google",
                DictionaryEntries = new System.Collections.Generic.List<DictionaryEntry>()
            };
        }
        catch (OperationCanceledException)
        {
            return new TranslationModel { ErrorMessage = "Dictionary lookup was cancelled." };
        }
        catch (Exception ex)
        {
            return new TranslationModel { OriginalText = word, ErrorMessage = ex.Message };
        }
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _translator.Dispose();
            _disposed = true;
        }
    }
}
