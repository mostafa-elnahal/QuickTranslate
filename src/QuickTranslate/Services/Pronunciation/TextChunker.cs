using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Text;
using QuickTranslate.Models;
using System.Linq;

namespace QuickTranslate.Services.Helpers;

/// <summary>
/// Proives methods to split text into chunks suitable for streaming TTS.
/// </summary>
public static class TextChunker
{
    public struct ChunkResult
    {
        public string Text;
        public int StartWordIndex;
        public int EndWordIndex;
    }

    /// <summary>
    /// Splits words into chunks using an adaptive strategy up to a maximum limit.
    /// Returns a list of strings and their corresponding word indices.
    /// </summary>
    public static IEnumerable<ChunkResult> ChunkText(IList<WordItem> words, int maxChunkSize = 4000)
    {
        if (words == null || words.Count == 0) yield break;

        // Configuration
        int firstChunkSize = Math.Min(150, maxChunkSize); // Keep first chunk small for low-latency
        int standardChunkSize = maxChunkSize;

        var currentChunk = new StringBuilder();
        int currentTargetSize = firstChunkSize;
        int startIndex = 0;

        for (int i = 0; i < words.Count; i++)
        {
            var word = words[i].Text;
            
            // If adding the next word exceeds the target size, yield the current chunk
            // (Unless the word itself is longer than the target size, in which case we still must yield what we have first)
            if (currentChunk.Length + word.Length + 1 > currentTargetSize && currentChunk.Length > 0)
            {
                yield return new ChunkResult { 
                    Text = currentChunk.ToString().Trim(),
                    StartWordIndex = startIndex,
                    EndWordIndex = i
                };
                
                currentChunk.Clear();
                startIndex = i;

                // Gradual Ramp-Up Strategy:
                if (currentTargetSize < standardChunkSize)
                {
                    if (currentTargetSize == firstChunkSize) 
                        currentTargetSize = Math.Min(firstChunkSize * 2, standardChunkSize);
                    else if (currentTargetSize < standardChunkSize / 2) 
                        currentTargetSize = Math.Min(currentTargetSize * 3, standardChunkSize);
                    else 
                        currentTargetSize = standardChunkSize;
                }
            }

            if (currentChunk.Length > 0) currentChunk.Append(" ");
            currentChunk.Append(word);
        }

        if (currentChunk.Length > 0)
        {
            yield return new ChunkResult {
                Text = currentChunk.ToString().Trim(),
                StartWordIndex = startIndex,
                EndWordIndex = words.Count
            };
        }
    }

    /// <summary>
    /// Legacy support for raw string chunking.
    /// </summary>
    public static IEnumerable<string> ChunkText(string text, int maxChunkSize = 4000)
    {
        if (string.IsNullOrEmpty(text)) yield break;
        
        var words = text.Split(new[] { ' ', '\r', '\n', '\t' }, StringSplitOptions.RemoveEmptyEntries)
                       .Select(w => new WordItem { Text = w })
                       .ToList();

        foreach (var result in ChunkText(words, maxChunkSize))
        {
            yield return result.Text;
        }
    }
}
