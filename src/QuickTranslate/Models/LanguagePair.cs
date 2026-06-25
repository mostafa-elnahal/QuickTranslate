namespace QuickTranslate.Models;

public class LanguagePair
{
    public string SourceCode { get; set; } = string.Empty;
    public string TargetCode { get; set; } = string.Empty;
    public string DisplayName => $"{SourceCode} → {TargetCode}";
}
