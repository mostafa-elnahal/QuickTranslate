using System.Threading.Tasks;
using QuickTranslate.Models;

namespace QuickTranslate.Services;

/// <summary>
/// Abstraction for translation services.
/// Allows swapping between different translation providers (Google, Bing, Yandex, etc.)
/// </summary>
public interface ITranslationService
{
    /// <summary>
    /// Translates text from one language to another.
    /// </summary>
    /// <param name="text">The text to translate.</param>
    /// <param name="targetLanguage">The target language code (e.g., "ar", "en").</param>
    /// <param name="sourceLanguage">Optional source language code. If null, auto-detect.</param>
    /// <returns>A TranslationModel with the result.</returns>
    Task<TranslationModel> TranslateAsync(string text, string targetLanguage, string? sourceLanguage = null);

    /// <summary>
    /// Gets the name of the current translation provider.
    /// </summary>
    string ProviderName { get; }

    /// <summary>
    /// Sets the translation provider by name.
    /// </summary>
    /// <param name="providerName">Name of the provider (e.g., "Google", "Bing", "Yandex").</param>
    void SetProvider(string providerName);

    /// <summary>
    /// Returns a list of available provider names.
    /// </summary>
    string[] GetAvailableProviders();
}
