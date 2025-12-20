using System;
using GTranslate.Translators;

namespace QuickTranslate.Services;

/// <summary>
/// Default implementation of ITranslatorFactory.
/// Creates translator instances based on provider name.
/// </summary>
public class TranslatorFactory : ITranslatorFactory
{
    private static readonly string[] _availableProviders = [
        "Google",
        "Bing",
        "Microsoft",
        "Yandex"
#if DEBUG
        , "Test"
#endif
    ];

    public string[] AvailableProviders => _availableProviders;

    public bool IsValidProvider(string providerName)
    {
        return Array.IndexOf(_availableProviders, providerName) >= 0;
    }

    public ITranslator Create(string providerName)
    {
        return providerName switch
        {
            "Google" => new GoogleTranslator(),
            "Bing" => new BingTranslator(),
            "Microsoft" => new MicrosoftTranslator(),
            "Yandex" => new YandexTranslator(),
#if DEBUG
            "Test" => new TestTranslator(),
#endif
            _ => throw new ArgumentException($"Unknown provider: {providerName}. Available: {string.Join(", ", _availableProviders)}")
        };
    }
}
