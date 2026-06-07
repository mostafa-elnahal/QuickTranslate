using System;
using System.Collections.Generic;
using System.Text;

namespace QuickTranslate.Services.Pronunciation;

/// <summary>
/// Splits text into chunks suitable for streaming TTS, respecting provider character limits.
/// </summary>
public static class TextChunker
{
    /// <summary>
    /// Splits text into chunks using an adaptive strategy up to <paramref name="maxChunkSize"/>.
    /// Returns the chunks and a mapping from word index to chunk index.
    /// </summary>
    /// <param name="text">The text to split.</param>
    /// <param name="maxChunkSize">Maximum characters per chunk.</param>
    /// <param name="firstChunkSize">
    /// Optional explicit size for the first chunk. If null, defaults to min(150, maxChunkSize).
    /// </param>
    public static (IEnumerable<string> Chunks, int[] WordToChunkIndex) ChunkText(
        string text, int maxChunkSize = 4000, int? firstChunkSize = null)
    {
        if (string.IsNullOrEmpty(text))
            return (Array.Empty<string>(), Array.Empty<int>());

        var words = text.Split(new[] { ' ', '\r', '\n', '\t' }, StringSplitOptions.RemoveEmptyEntries);
        if (words.Length == 0)
            return (Array.Empty<string>(), Array.Empty<int>());

        int targetFirst = firstChunkSize ?? Math.Min(150, maxChunkSize);
        int standardChunkSize = maxChunkSize;

        var chunks = new List<string>();
        var wordToChunk = new int[words.Length];
        var currentChunk = new StringBuilder();
        int currentTargetSize = targetFirst;

        for (int i = 0; i < words.Length; i++)
        {
            var word = words[i];

            if (currentChunk.Length + word.Length + 1 > currentTargetSize && currentChunk.Length > 0)
            {
                chunks.Add(currentChunk.ToString().Trim());
                currentChunk.Clear();

                if (currentTargetSize < standardChunkSize)
                {
                    if (currentTargetSize == targetFirst)
                        currentTargetSize = Math.Min(targetFirst * 2, standardChunkSize);
                    else if (currentTargetSize < standardChunkSize / 2)
                        currentTargetSize = Math.Min(currentTargetSize * 3, standardChunkSize);
                    else
                        currentTargetSize = standardChunkSize;
                }
            }

            if (currentChunk.Length > 0) currentChunk.Append(" ");
            currentChunk.Append(word);
            wordToChunk[i] = chunks.Count;
        }

        if (currentChunk.Length > 0)
            chunks.Add(currentChunk.ToString().Trim());

        return (chunks, wordToChunk);
    }
}
