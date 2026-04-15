using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Text;

namespace QuickTranslate.Services.Helpers;

/// <summary>
/// Proives methods to split text into chunks suitable for streaming TTS.
/// </summary>
public static class TextChunker
{
    /// <summary>
    /// Splits text into chunks using an adaptive strategy.
    /// The first chunk is small (~200 chars) for ultra-low latency.
    /// Subsequent chunks are large (~4000 chars) to minimize API requests and avoid rate limits.
    /// </summary>
    public static IEnumerable<string> ChunkText(string text)
    {
        if (string.IsNullOrEmpty(text)) yield break;

        // Configuration
        const int FirstChunkSize = 200;  // Very small for instant start
        const int StandardChunkSize = 4000; // Large for efficiency

        // Split by sentence terminators, keeping the terminator with the sentence
        // Pattern: lookbehind for [.!?] followed by whitespace
        var sentences = Regex.Split(text, @"(?<=[.!?])\s+");

        var currentChunk = new StringBuilder();
        int currentTargetSize = FirstChunkSize;

        foreach (var sentence in sentences)
        {
            // If adding the next sentence exceeds the target size, yield the current chunk
            if (currentChunk.Length + sentence.Length > currentTargetSize && currentChunk.Length > 0)
            {
                yield return currentChunk.ToString();
                currentChunk.Clear();

                // Gradual Ramp-Up Strategy:
                // 200 -> 500 -> 1500 -> 4000 (Max)
                // This prevents the "Silence Gap" where Chunk 2 is too big to download 
                // before Chunk 1 finishes playing.
                if (currentTargetSize < StandardChunkSize)
                {
                    if (currentTargetSize == FirstChunkSize) currentTargetSize = 500;
                    else if (currentTargetSize == 500) currentTargetSize = 1500;
                    else currentTargetSize = StandardChunkSize;
                }
            }

            // If the chunk isn't empty (we are appending), add a space
            if (currentChunk.Length > 0) currentChunk.Append(" ");
            currentChunk.Append(sentence);
        }

        if (currentChunk.Length > 0)
        {
            yield return currentChunk.ToString();
        }
    }
}
