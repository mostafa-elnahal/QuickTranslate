namespace QuickTranslate.Models;

/// <summary>
/// Model for displaying a pronunciation provider in the Settings UI.
/// Separate from <see cref="ProviderInfo"/> which serves translation providers.
/// </summary>
public class PronunciationProviderInfo
{
    public string Name { get; init; } = string.Empty;
    public string BrandColor { get; init; } = "#888888";
    public bool RequiresApiKey { get; init; }
    public string Description { get; init; } = string.Empty;

    public static PronunciationProviderInfo Create(string providerName)
    {
        return providerName switch
        {
            Constants.PronunciationProviders.Google => new PronunciationProviderInfo
            {
                Name = providerName,
                BrandColor = "#4285F4",
                RequiresApiKey = false,
                Description = "Free, no API key needed"
            },
            Constants.PronunciationProviders.Gemini => new PronunciationProviderInfo
            {
                Name = providerName,
                BrandColor = "#8E24AA",
                RequiresApiKey = true,
                Description = "AI voice, streaming"
            },
            Constants.PronunciationProviders.Gcp => new PronunciationProviderInfo
            {
                Name = providerName,
                BrandColor = "#34A853",
                RequiresApiKey = true,
                Description = "HD voice, word timing"
            },
            _ => new PronunciationProviderInfo
            {
                Name = providerName
            }
        };
    }
}
