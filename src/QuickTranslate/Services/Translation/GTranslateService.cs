using System;
using System.Threading.Tasks;
using GTranslate.Translators;
using QuickTranslate.Models;
using QuickTranslate.Services.Translators;

namespace QuickTranslate.Services;

/// <summary>
/// Translation service implementation using the GTranslate library.
/// Lazy-loads translators to minimize memory usage.
/// </summary>
public class GTranslateService : ITranslationService, IDisposable
{
    private readonly ITranslatorFactory _translatorFactory;

    private ITranslator? _currentTranslator;
    private string _currentProviderName = "Google";

    public string ProviderName => _currentProviderName;

    public GTranslateService() : this(new TranslatorFactory())
    {
    }

    public GTranslateService(ITranslatorFactory translatorFactory)
    {
        _translatorFactory = translatorFactory;
    }

    public void SetProvider(string providerName)
    {
        if (!_translatorFactory.IsValidProvider(providerName))
        {
            throw new ArgumentException($"Unknown provider: {providerName}. Available: {string.Join(", ", _translatorFactory.AvailableProviders)}");
        }

        if (_currentProviderName == providerName && _currentTranslator != null)
            return;

        // Dispose old translator before creating new one
        DisposeCurrentTranslator();
        _currentProviderName = providerName;
        _currentTranslator = _translatorFactory.Create(providerName);
    }

    public string[] GetAvailableProviders() => _translatorFactory.AvailableProviders;

    public System.Collections.Generic.IEnumerable<ProviderInfo> GetProviderStates()
    {
        foreach (var name in GetAvailableProviders())
        {
            yield return ProviderInfo.Create(name, name == _currentProviderName);
        }
    }

    public LanguageOption[] GetSupportedLanguages()
    {
        // GTranslate exposes all ISO-639-1 languages
        // We will map its Language dictionary to our LanguageOption
        var langs = new System.Collections.Generic.List<LanguageOption>
        {
            new LanguageOption("Auto-detect", "auto")
        };

        foreach (var lang in GTranslate.Language.LanguageDictionary.Values)
        {
            langs.Add(new LanguageOption(lang.Name, lang.ISO6391));
        }

        return langs.ToArray();
    }

    public async Task<TranslationModel> TranslateAsync(string text, string targetLanguage, string? sourceLanguage = null, System.Threading.CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return new TranslationModel
            {
                OriginalText = text ?? string.Empty,
                MainTranslation = string.Empty,
                ProviderName = _currentProviderName
            };
        }

        try
        {
            // Lazy-load the translator using factory
            _currentTranslator ??= _translatorFactory.Create(_currentProviderName);

            // WaitAsync allows the caller to short-circuit if cancelled, even if the underlying SDK task doesn't support it.
            var resultTask = _currentTranslator.TranslateAsync(text, targetLanguage, sourceLanguage);
            var result = await resultTask.WaitAsync(cancellationToken);

            var model = new TranslationModel
            {
                OriginalText = text,
                MainTranslation = result.Translation,
                SourceLanguage = result.SourceLanguage.Name,
                SourceLanguageCode = result.SourceLanguage.ISO6391,
                TargetLanguage = result.TargetLanguage.Name,
                TargetLanguageCode = result.TargetLanguage.ISO6391,
                ProviderName = _currentProviderName,
                DictionaryEntries = []
            };

            // Process Rich Dictionary Data if available
            if (result is GoogleTranslationResult googleResult)
            {
                model.DictionaryEntries = googleResult.DictionaryEntries;
                // Use source transliteration (pronunciation of original word)
                model.Phonetic = googleResult.SourceTransliteration ?? string.Empty;
            }

            return model;
        }
        catch (Exception ex)
        {
            return new TranslationModel
            {
                OriginalText = text,
                MainTranslation = $"[Translation Error: {ex.Message}]",
                ProviderName = _currentProviderName
            };
        }
    }

    private void DisposeCurrentTranslator()
    {
        if (_currentTranslator is IDisposable disposable)
        {
            disposable.Dispose();
        }
        _currentTranslator = null;
    }

    public void Dispose()
    {
        DisposeCurrentTranslator();
        GC.SuppressFinalize(this);
    }
}
