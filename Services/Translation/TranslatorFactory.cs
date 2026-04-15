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
        "Google (RPC)",
        //"Google (RPC)",
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

    // ... (previous code)
    public ITranslator Create(string providerName)
    {
        return providerName switch
        {
            "Google" => new QuickTranslate.Services.Translators.GoogleTranslator(),
            "Google (RPC)" => new QuickTranslate.Services.Translators.GoogleTranslator2(),
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
