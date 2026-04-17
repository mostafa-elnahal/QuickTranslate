namespace QuickTranslate;

public static class Constants
{
    public static class PronunciationProviders
    {
        public const string Google = "Google";
        public const string Gemini = "Gemini";
    }

    public static class TranslationProviders
    {
        public const string Google = "Google";
        public const string Bing = "Bing";
        public const string Yandex = "Yandex";
    }

    public static class Languages
    {
        public static readonly System.Collections.Generic.HashSet<string> RtlLanguages = new(System.StringComparer.OrdinalIgnoreCase)
        {
            "Arabic", "Hebrew", "Persian", "Urdu", "Pashto", "Sindhi", "Kurdish"
        };
    }

    public static class Defaults
    {
        public const string TargetLanguage = "en";
    }
}
