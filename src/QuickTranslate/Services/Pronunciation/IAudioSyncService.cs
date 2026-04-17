using System;
using System.Collections.Generic;
using QuickTranslate.Models;

namespace QuickTranslate.Services.Pronunciation;

public interface IAudioSyncService
{
    /// <summary>
    /// Calculates the estimated duration for each word in a given range based on character length and speaking rate.
    /// </summary>
    List<int> GetWordDurationsInMs(int startIndex, int endIndex, IList<WordItem> words, bool slowMode);
}
