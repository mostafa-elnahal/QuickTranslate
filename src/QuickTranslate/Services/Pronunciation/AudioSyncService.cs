using System;
using System.Collections.Generic;
using System.Linq;
using QuickTranslate.Models;

namespace QuickTranslate.Services.Pronunciation;

public class AudioSyncService : IAudioSyncService
{
    // BuildChunkWordMapping removed: now handled natively by TextChunker to guarantee 100% accuracy.


    public List<int> GetWordDurationsInMs(int startIndex, int endIndex, IList<WordItem> words, bool slowMode)
    {
        int wordCount = endIndex - startIndex;
        if (wordCount <= 0) return new List<int>();

        int totalChars = 0;
        for (int i = startIndex; i < endIndex; i++)
            totalChars += words[i].Text.Length;

        // Average speaking rate heuristics
        double charsPerSecond = slowMode ? 7.0 : 14.0;
        double totalDurationMs = (totalChars / charsPerSecond) * 1000.0;

        // Minimum duration per word to avoid flickering
        double minPerWordMs = 150;
        if (totalDurationMs / wordCount < minPerWordMs)
            totalDurationMs = wordCount * minPerWordMs;

        var durations = new List<int>();
        for (int i = startIndex; i < endIndex; i++)
        {
            double wordFraction = (double)words[i].Text.Length / totalChars;
            int delayMs = Math.Max((int)(totalDurationMs * wordFraction), (int)minPerWordMs);
            durations.Add(delayMs);
        }

        return durations;
    }
}
