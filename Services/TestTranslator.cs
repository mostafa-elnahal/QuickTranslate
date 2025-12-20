using System;
using System.Threading.Tasks;
using GTranslate;
using GTranslate.Results;
using GTranslate.Translators;

namespace QuickTranslate.Services;

#if DEBUG
public class TestTranslator : ITranslator
{
    public string Name => "Test";

    public Task<ITranslationResult> TranslateAsync(string text, string to, string? from = null)
    {
        var result = new MockTranslationResult
        {
            Translation = $"[TEST] {text}",
            OriginalString = text,
            SourceLanguage = new MockLanguage("Auto", "auto", "auto"),
            TargetLanguage = new MockLanguage("Test", to, to),
            Service = "Test"
        };

        return Task.FromResult<ITranslationResult>(result);
    }

    public Task<ITranslationResult> TranslateAsync(string text, ILanguage to, ILanguage? from = null)
    {
        return TranslateAsync(text, to.ISO6391, from?.ISO6391);
    }

    public Task<ITransliterationResult> TransliterateAsync(string text, string to, string? from = null)
    {
        throw new NotSupportedException();
    }

    public Task<ITransliterationResult> TransliterateAsync(string text, ILanguage to, ILanguage? from = null)
    {
        throw new NotSupportedException();
    }

    public Task<ILanguage> DetectLanguageAsync(string text)
    {
        return Task.FromResult<ILanguage>(new MockLanguage("English", "en", "en"));
    }

    public bool IsLanguageSupported(string language) => true;

    public bool IsLanguageSupported(ILanguage language) => true;

    public void Dispose() { }

    // Mock classes to satisfy interfaces
    private class MockLanguage : ILanguage
    {
        public MockLanguage(string name, string iso, string nativeName)
        {
            Name = name;
            ISO6391 = iso;
            NativeName = nativeName;
        }

        public string Name { get; }
        public string ISO6391 { get; }
        public string ISO6393 { get; } = "try"; // Mock 3-letter code
        public string NativeName { get; }
    }

    private class MockTranslationResult : ITranslationResult
    {
        public string Translation { get; set; } = "";
        public string OriginalString { get; set; } = "";
        public string Source { get => OriginalString; } // Implementation of Source
        public ILanguage SourceLanguage { get; set; } = new MockLanguage("?", "?", "?");
        public ILanguage TargetLanguage { get; set; } = new MockLanguage("?", "?", "?");
        public string Service { get; set; } = "Test";

        // Possibly required members by interface (guessing common ones, compiler will catch if missing)
        // If ITranslatorResult has more, I'll add them.
    }
}
#endif
