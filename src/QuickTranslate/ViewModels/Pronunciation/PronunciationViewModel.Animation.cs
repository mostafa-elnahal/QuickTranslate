using System;
using System.Threading;
using System.Threading.Tasks;
using QuickTranslate.Models;
using System.Linq;
using System.Collections.Generic;

namespace QuickTranslate.ViewModels;

public partial class PronunciationViewModel
{
    public async Task AnimateSyllablesAsync(TimeSpan totalDuration)
    {
        if (Syllables.Count == 0) return;

        _wordAnimationCts?.Cancel();
        _wordAnimationCts?.Dispose();
        _wordAnimationCts = new CancellationTokenSource();
        var ct = _wordAnimationCts.Token;

        foreach (var s in Syllables) s.IsActive = false;

        double durationMs = totalDuration.TotalMilliseconds;
        int interval = (int)(durationMs / Syllables.Count);

        var sw = System.Diagnostics.Stopwatch.StartNew();

        for (int i = 0; i < Syllables.Count; i++)
        {
            while (!IsPlaying && !ct.IsCancellationRequested)
            {
                await Task.Delay(50, ct).ContinueWith(_ => {});
            }
            if (ct.IsCancellationRequested) break;

            if (i > 0) Syllables[i - 1].IsActive = false;
            Syllables[i].IsActive = true;

            long targetTimeMs = sw.ElapsedMilliseconds + interval;
            while (sw.ElapsedMilliseconds < targetTimeMs)
            {
                if (ct.IsCancellationRequested) break;
                if (!IsPlaying) targetTimeMs += 50;
                
                int waitMs = Math.Min(50, Math.Max(1, (int)(targetTimeMs - sw.ElapsedMilliseconds)));
                try { await Task.Delay(waitMs, ct); }
                catch (TaskCanceledException) { break; }
            }
            if (ct.IsCancellationRequested) break;
        }
        if (!ct.IsCancellationRequested && Syllables.Count > 0) 
            Syllables[Syllables.Count - 1].IsActive = false;
    }

    public async Task AnimateWordsAsync(TimeSpan totalDuration)
    {
        if (_wordAnimationCts != null)
        {
            try { _wordAnimationCts.Cancel(); } catch (ObjectDisposedException) { }
            _wordAnimationCts.Dispose();
            _wordAnimationCts = null;
        }
        _wordAnimationCts = new CancellationTokenSource();
        await AnimateWordsAsync(_wordAnimationCts.Token);
    }

    public async Task AnimateWordsAsync(CancellationToken ct, Task? startSignal = null)
    {
        if (Words.Count == 0 || IsSingleWord) return;
        if (startSignal != null) await startSignal;

        ClearWordHighlights();

        // Build word start times: use exact GCP timepoints when available, else estimate
        var wordStartTimes = new List<double>();
        if (_exactTimepoints != null && _exactTimepoints.Count > 0)
        {
            // GCP timepoints give us exact start times in seconds for each word
            foreach (var tp in _exactTimepoints)
                wordStartTimes.Add(tp.TimeSeconds * 1000.0); // convert to ms
            
            // Pad if fewer timepoints than words (shouldn't happen, but be safe)
            while (wordStartTimes.Count < Words.Count)
                wordStartTimes.Add(wordStartTimes.Count > 0 ? wordStartTimes[^1] + 200 : 0);
        }
        else
        {
            // Estimated timing via character-rate heuristics
            var wordDurations = _syncService.GetWordDurationsInMs(0, Words.Count, Words, IsSlowMode);
            double currentOffset = 0;
            foreach (var duration in wordDurations)
            {
                wordStartTimes.Add(currentOffset);
                currentOffset += duration;
            }
        }

        while (!ct.IsCancellationRequested)
        {
            if (!IsPlaying)
            {
                await Task.Delay(50, ct).ContinueWith(_ => { });
                continue;
            }

            var player = IsStreamingMode ? StreamingPlayer : null;
            TimeSpan currentPos = player?.CurrentPosition ?? CurrentPosition;

            double elapsedMs = currentPos.TotalMilliseconds;
            
            int activeWordIndex = -1;
            for (int i = 0; i < wordStartTimes.Count; i++)
            {
                if (elapsedMs >= wordStartTimes[i] && 
                    (i == wordStartTimes.Count - 1 || elapsedMs < wordStartTimes[i + 1]))
                {
                    activeWordIndex = i;
                    break;
                }
            }

            if (activeWordIndex != -1)
            {
                // Find which chunk this word belongs to
                int activeChunkIndex = -1;
                if (IsStreamingMode && _chunkWordRanges != null)
                {
                    activeChunkIndex = _chunkWordRanges.FindIndex(r => activeWordIndex >= r.StartIndex && activeWordIndex < r.EndIndex);
                }

                for (int i = 0; i < Words.Count; i++)
                {
                    Words[i].IsActiveWord = (i == activeWordIndex);
                    Words[i].IsInActiveChunk = (activeChunkIndex != -1 && i >= _chunkWordRanges![activeChunkIndex].StartIndex && i < _chunkWordRanges[activeChunkIndex].EndIndex);
                }
            }

            // Exit when the last word is finished
            double lastWordStart = wordStartTimes[^1];
            double msPerChar = IsSlowMode ? 150.0 : 80.0;
            double lastWordDuration = (Words[^1].Text?.Length ?? 5) * msPerChar;
            
            if (elapsedMs > lastWordStart + lastWordDuration)
            {
                break;
            }

            await Task.Delay(30, ct).ContinueWith(_ => { });
        }

        ClearWordHighlights();
    }
    private void ClearWordHighlights()
    {
        foreach (var w in Words)
        {
            w.IsInActiveChunk = false;
            w.IsActiveWord = false;
        }
    }
}
