namespace QuickTranslate.Models;

/// <summary>
/// Represents an available OCR language.
/// </summary>
public record OcrLanguage(string Code, string DisplayName)
{
    public override string ToString() => DisplayName;
}
