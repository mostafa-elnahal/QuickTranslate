namespace QuickTranslate;

/// <summary>
/// Model for displaying a translation provider in the UI.
/// </summary>
public class ProviderInfo
{
    public string Name { get; set; } = string.Empty;
    public string BrandColor { get; set; } = "#888888";
    public bool IsSelected { get; set; }

    /// <summary>
    /// Creates provider info with brand colors for known providers.
    /// </summary>
    public static ProviderInfo Create(string providerName, bool isSelected = false)
    {
        var brandColor = providerName switch
        {
            "Google" => "#4285F4",
            "Bing" => "#00A4EF",
            "Microsoft" => "#737373",
            "Yandex" => "#FF0000",
            _ => "#888888"
        };

        return new ProviderInfo
        {
            Name = providerName,
            BrandColor = brandColor,
            IsSelected = isSelected
        };
    }
}
