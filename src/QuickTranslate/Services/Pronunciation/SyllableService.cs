using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace QuickTranslate.Services;

/// <summary>
/// Service for breaking English words into phonetic syllables for pronunciation practice.
/// Uses rule-based English syllabification algorithm.
/// </summary>
public class SyllableService : ISyllableService
{
    // Vowels for syllable detection
    private static readonly HashSet<char> Vowels = new() { 'a', 'e', 'i', 'o', 'u', 'y' };

    // Common consonant clusters that shouldn't be split
    private static readonly HashSet<string> ConsonantClusters = new()
    {
        "bl", "br", "ch", "cl", "cr", "dr", "fl", "fr", "gl", "gr",
        "ph", "pl", "pr", "sc", "sh", "sk", "sl", "sm", "sn", "sp",
        "st", "str", "sw", "th", "tr", "tw", "wh", "wr", "sch", "scr",
        "shr", "spl", "spr", "squ", "thr"
    };

    // Common suffixes to detect for better syllable breaks
    private static readonly string[] CommonSuffixes =
    {
        "tion", "sion", "cious", "tious", "ious", "eous", "ous",
        "ment", "ness", "less", "ful", "able", "ible", "ity", "ive",
        "ly", "er", "or", "ist", "ing", "ed", "es"
    };

    // Phonetic spelling mappings for common patterns
    private static readonly Dictionary<string, string> PhoneticMappings = new(StringComparer.OrdinalIgnoreCase)
    {
        // Vowel combinations
        {"oo", "oo"}, {"ee", "ee"}, {"ea", "ee"}, {"ai", "ay"}, {"ay", "ay"},
        {"oa", "oh"}, {"ow", "oh"}, {"ou", "ow"}, {"au", "aw"}, {"aw", "aw"},
        {"oi", "oy"}, {"oy", "oy"}, {"ie", "ee"}, {"ei", "ay"},
        
        // Consonant patterns
        {"ph", "f"}, {"gh", ""}, {"ch", "ch"}, {"sh", "sh"}, {"th", "th"},
        {"wh", "w"}, {"ck", "k"}, {"ng", "ng"}, {"qu", "kw"}, {"x", "ks"},
        
        // Common endings
        {"tion", "shuhn"}, {"sion", "zhuhn"}, {"ture", "chr"}, {"sure", "zhr"},
        
        // Silent letters
        {"igh", "y"}, {"ough", "oh"}, {"augh", "aw"}, {"eigh", "ay"}
    };

    // IPA to "Sounds like" mappings
    private static readonly Dictionary<string, string> IpaMappings = new(StringComparer.Ordinal)
    {
        // Vowels
        {"æ", "a"},   {"a", "ah"},   {"ɑ", "aa"},   {"ɒ", "o"},
        {"ə", "uh"},  {"e", "eh"},   {"ɛ", "eh"},   {"ɜ", "ur"},
        {"i", "ee"},  {"ɪ", "ih"},   {"o", "oh"},   {"ɔ", "aw"},
        {"u", "oo"},  {"ʊ", "uu"},   {"ʌ", "uh"},   {"y", "ew"},
        
        // Diphthongs
        {"aɪ", "eye"}, {"eɪ", "ay"}, {"oʊ", "oh"}, {"aʊ", "ow"}, {"ɔɪ", "oy"},
        {"eə", "air"}, {"ɪə", "ear"}, {"ʊə", "oor"},
        
        // Consonants (most are same, listing changes)
        {"ʃ", "sh"},  {"tʃ", "ch"},  {"θ", "th"},   {"ð", "th"},
        {"ʒ", "zh"},  {"dʒ", "j"},   {"ŋ", "ng"},   {"j", "y"},
        {"ɡ", "g"},   {"ɹ", "r"}
    };

    /// <inheritdoc />
    public List<(string Text, bool IsStressed)> GetSyllables(string word, string? ipa = null)
    {
        var result = new List<(string Text, bool IsStressed)>();

        // 1. Try to use IPA if available and valid
        if (!string.IsNullOrWhiteSpace(ipa))
        {
            var ipaSyllables = ParseIpa(ipa);
            if (ipaSyllables.Count > 0)
            {
                return ipaSyllables;
            }
        }

        // 2. Fallback to rule-based generation (no stress info known, assume none)
        if (string.IsNullOrWhiteSpace(word))
            return result;

        word = CleanWord(word);
        if (string.IsNullOrEmpty(word))
            return result;

        var rawSyllables = SplitIntoSyllables(word);
        foreach (var syllable in rawSyllables)
        {
            var phonetic = ConvertToPhonetic(syllable);
            if (!string.IsNullOrEmpty(phonetic))
            {
                result.Add((phonetic, false));
            }
        }

        // Fallback if empty
        if (result.Count == 0)
        {
            result.Add((word.ToLowerInvariant(), false));
        }

        return result;
    }

    /// <inheritdoc />
    public string GetSoundsLike(string word, string? ipa = null)
    {
        var syllables = GetSyllables(word, ipa);
        var sb = new StringBuilder();
        for (int i = 0; i < syllables.Count; i++)
        {
            if (i > 0) sb.Append(" · ");
            sb.Append(syllables[i].Text);
        }
        return sb.ToString();
    }

    /// <summary>
    /// Parses IPA string into respelled syllables with stress info.
    /// </summary>
    private List<(string Text, bool IsStressed)> ParseIpa(string ipa)
    {
        var result = new List<(string Text, bool IsStressed)>();

        // Clean IPA wrapper
        ipa = ipa.Trim('/', '[', ']', ' ');

        // Split logical groups (by stress or syllable separator)
        // We will manually tokenize to preserve stress info
        var currentSyllable = new StringBuilder();
        bool pendingStress = false;

        // Normalize stress markers
        ipa = ipa.Replace("'", "ˈ").Replace("ˌ", "."); // Treat secondary stress as syllable break for now, primary as stress

        int i = 0;
        while (i < ipa.Length)
        {
            char c = ipa[i];

            if (c == 'ˈ') // Primary Stress
            {
                // Finish current
                if (currentSyllable.Length > 0)
                {
                    result.Add((ConvertIpaSegment(currentSyllable.ToString()), pendingStress));
                    currentSyllable.Clear();
                }
                pendingStress = true; // Next one is stressed
            }
            else if (c == '.') // Syllable break (or secondary stress converted above)
            {
                if (currentSyllable.Length > 0)
                {
                    result.Add((ConvertIpaSegment(currentSyllable.ToString()), pendingStress));
                    currentSyllable.Clear();
                }
                pendingStress = false;
            }
            else
            {
                currentSyllable.Append(c);
            }
            i++;
        }

        if (currentSyllable.Length > 0)
        {
            result.Add((ConvertIpaSegment(currentSyllable.ToString()), pendingStress));
        }

        return result;
    }

    private string ConvertIpaSegment(string ipaSegment)
    {
        var sb = new StringBuilder();
        int i = 0;
        while (i < ipaSegment.Length)
        {
            // Try longest match from map
            bool matched = false;
            for (int len = Math.Min(3, ipaSegment.Length - i); len > 0; len--)
            {
                string sub = ipaSegment.Substring(i, len);
                if (IpaMappings.TryGetValue(sub, out string? val))
                {
                    sb.Append(val);
                    i += len;
                    matched = true;
                    break;
                }
            }

            if (!matched)
            {
                // Fallback: keep char if alpha, otherwise skip
                if (char.IsLetter(ipaSegment[i]))
                    sb.Append(ipaSegment[i]);
                i++;
            }
        }
        return sb.ToString();
    }

    /// <summary>
    /// Cleans the input word by removing non-alphabetic characters.
    /// </summary>
    private static string CleanWord(string word)
    {
        return Regex.Replace(word.Trim().ToLowerInvariant(), "[^a-z]", "");
    }

    /// <summary>
    /// Splits a word into syllables using linguistic rules.
    /// </summary>
    private List<string> SplitIntoSyllables(string word)
    {
        var syllables = new List<string>();
        var current = new StringBuilder();

        int i = 0;
        while (i < word.Length)
        {
            char c = word[i];
            current.Append(c);

            // Check if we're at a vowel
            if (IsVowel(c))
            {
                // Look ahead for syllable boundary
                int nextConsonantStart = i + 1;

                // Skip consecutive vowels (diphthongs like 'ea', 'ou', etc.)
                while (nextConsonantStart < word.Length && IsVowel(word[nextConsonantStart]))
                {
                    current.Append(word[nextConsonantStart]);
                    nextConsonantStart++;
                }

                // Count consonants between this vowel and the next
                int consonantCount = 0;
                int nextVowelPos = nextConsonantStart;
                while (nextVowelPos < word.Length && !IsVowel(word[nextVowelPos]))
                {
                    consonantCount++;
                    nextVowelPos++;
                }

                // Determine split point based on consonant cluster rules
                if (nextVowelPos < word.Length && consonantCount > 0)
                {
                    int splitAt = DetermineSplitPoint(word, nextConsonantStart, consonantCount);

                    // Add consonants before split to current syllable
                    for (int j = nextConsonantStart; j < splitAt; j++)
                    {
                        current.Append(word[j]);
                    }

                    // Save current syllable
                    if (current.Length > 0)
                    {
                        syllables.Add(current.ToString());
                        current.Clear();
                    }

                    i = splitAt;
                    continue;
                }
            }

            i++;
        }

        // Add remaining characters as last syllable
        if (current.Length > 0)
        {
            syllables.Add(current.ToString());
        }

        // Handle edge cases
        if (syllables.Count == 0 && word.Length > 0)
        {
            syllables.Add(word);
        }

        return syllables;
    }

    /// <summary>
    /// Determines where to split a consonant cluster between vowels.
    /// </summary>
    private int DetermineSplitPoint(string word, int consonantStart, int consonantCount)
    {
        if (consonantCount == 1)
        {
            // Single consonant goes with the following vowel
            return consonantStart;
        }

        if (consonantCount == 2)
        {
            // Check if it's a common cluster that stays together
            string cluster = word.Substring(consonantStart, 2);
            if (ConsonantClusters.Contains(cluster.ToLowerInvariant()))
            {
                return consonantStart; // Keep cluster together with following vowel
            }
            // Otherwise split between consonants
            return consonantStart + 1;
        }

        if (consonantCount >= 3)
        {
            // Check for 3-letter clusters
            if (consonantCount >= 3)
            {
                string threeCluster = word.Substring(consonantStart, Math.Min(3, word.Length - consonantStart));
                if (ConsonantClusters.Contains(threeCluster.ToLowerInvariant()))
                {
                    return consonantStart;
                }
            }

            // Check for 2-letter cluster at end
            string lastTwo = word.Substring(consonantStart + consonantCount - 2, 2);
            if (ConsonantClusters.Contains(lastTwo.ToLowerInvariant()))
            {
                return consonantStart + consonantCount - 2;
            }

            // Default: split after first consonant
            return consonantStart + 1;
        }

        return consonantStart;
    }

    /// <summary>
    /// Converts a syllable to simple phonetic spelling.
    /// </summary>
    private string ConvertToPhonetic(string syllable)
    {
        if (string.IsNullOrEmpty(syllable))
            return syllable;

        var result = new StringBuilder();
        int i = 0;

        while (i < syllable.Length)
        {
            bool matched = false;

            // Try multi-character patterns first (longest match)
            for (int len = Math.Min(4, syllable.Length - i); len > 1; len--)
            {
                string sub = syllable.Substring(i, len);
                if (PhoneticMappings.TryGetValue(sub, out string? replacement))
                {
                    result.Append(replacement);
                    i += len;
                    matched = true;
                    break;
                }
            }

            if (!matched)
            {
                char c = syllable[i];

                // Handle single vowels
                if (IsVowel(c))
                {
                    // Silent 'e' at end
                    if (c == 'e' && i == syllable.Length - 1 && syllable.Length > 1)
                    {
                        // Skip silent e
                    }
                    else
                    {
                        result.Append(GetSimpleVowelSound(c));
                    }
                }
                else
                {
                    // Keep consonant as-is
                    result.Append(c);
                }
                i++;
            }
        }

        return result.ToString();
    }

    /// <summary>
    /// Gets the simple phonetic representation of a vowel.
    /// </summary>
    private static string GetSimpleVowelSound(char vowel)
    {
        return vowel switch
        {
            'a' => "ah",
            'e' => "eh",
            'i' => "ih",
            'o' => "oh",
            'u' => "uh",
            'y' => "ee",
            _ => vowel.ToString()
        };
    }

    /// <summary>
    /// Checks if a character is a vowel.
    /// </summary>
    private static bool IsVowel(char c)
    {
        return Vowels.Contains(char.ToLowerInvariant(c));
    }
}
