using GTranslate.Translators;

namespace QuickTranslate.Services;

/// <summary>
/// Factory interface for creating translator instances.
/// Enables cleaner provider switching and easier testing.
/// </summary>
public interface ITranslatorFactory
{
    /// <summary>
    /// Creates a translator instance for the specified provider.
    /// </summary>
    /// <param name="providerName">Name of the provider (e.g., "Google", "Bing", "Yandex").</param>
    /// <returns>An ITranslator instance.</returns>
    ITranslator Create(string providerName);

    /// <summary>
    /// Gets the list of available provider names.
    /// </summary>
    string[] AvailableProviders { get; }

    /// <summary>
    /// Checks if a provider name is valid.
    /// </summary>
    bool IsValidProvider(string providerName);
}
