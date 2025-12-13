using System;
using System.Threading.Tasks;
using GTranslate.Translators;
using QuickTranslate.Models;

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
        // Translator will be created lazily on next translate call
    }

    public string[] GetAvailableProviders() => _translatorFactory.AvailableProviders;

    public async Task<TranslationModel> TranslateAsync(string text, string targetLanguage, string? sourceLanguage = null)
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

            var result = await _currentTranslator.TranslateAsync(text, targetLanguage, sourceLanguage);

            return new TranslationModel
            {
                OriginalText = text,
                MainTranslation = result.Translation,
                SourceLanguage = result.SourceLanguage.Name,
                TargetLanguage = result.TargetLanguage.Name,
                ProviderName = _currentProviderName,
                DictionaryEntries = []
            };
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
