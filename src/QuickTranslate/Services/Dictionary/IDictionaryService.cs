using System.Threading;
using System.Threading.Tasks;
using QuickTranslate.Models;

namespace QuickTranslate.Services;

/// <summary>
/// Provides dictionary lookup functionality independent of the active translation provider.
/// Always uses a dedicated dictionary API (e.g., Google) for rich dictionary data.
/// </summary>
public interface IDictionaryService
{
    /// <summary>
    /// Looks up dictionary entries for a single word.
    /// Returns a TranslationModel with DictionaryEntries populated.
    /// </summary>
    Task<TranslationModel> LookupAsync(string word, string targetLanguage, string? sourceLanguage = null, CancellationToken cancellationToken = default);
}
