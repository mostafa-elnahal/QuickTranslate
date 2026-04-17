namespace QuickTranslate.Models;

public enum DictionaryEntryType
{
    Translation, // Standard translation variants (dt=bd)
    Definition,  // Source definitions (dt=md)
    Example      // Usage examples (dt=ex)
}
