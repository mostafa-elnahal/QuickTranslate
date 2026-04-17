using System.Collections.Generic;

namespace QuickTranslate.Services;

/// <summary>
/// Service for breaking words into phonetic syllables for pronunciation practice.
/// </summary>
public interface ISyllableService
{
    /// <summary>
    /// Splits a word into simple phonetic syllables, optionally using IPA for better accuracy.
    /// </summary>
    /// <param name="word">The word to split.</param>
    /// <param name="ipa">Optional IPA string from translation service (e.g., "/həˈloʊ/").</param>
    /// <returns>List of phonetic syllables with stress information.</returns>
    List<(string Text, bool IsStressed)> GetSyllables(string word, string? ipa = null);

    /// <summary>
    /// Gets the formatted "sounds like" string with separators.
    /// </summary>
    string GetSoundsLike(string word, string? ipa = null);
}
