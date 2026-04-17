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
        _wordAnimationCts = new CancellationTokenSource();
        var ct = _wordAnimationCts.Token;

        foreach (var s in Syllables) s.IsActive = false;

        double durationMs = totalDuration.TotalMilliseconds;
        int interval = (int)(durationMs / Syllables.Count);

        for (int i = 0; i < Syllables.Count; i++)
        {
            while (!IsPlaying && !ct.IsCancellationRequested)
            {
                await Task.Delay(50, ct).ContinueWith(_ => {});
            }
            if (ct.IsCancellationRequested) break;

            if (i > 0) Syllables[i - 1].IsActive = false;
            Syllables[i].IsActive = true;

            var targetTime = DateTime.UtcNow.AddMilliseconds(interval);
            while (DateTime.UtcNow < targetTime)
            {
                if (ct.IsCancellationRequested) break;
                if (!IsPlaying) targetTime = targetTime.AddMilliseconds(50);
                
                int waitMs = Math.Min(50, Math.Max(1, (int)(targetTime - DateTime.UtcNow).TotalMilliseconds));
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
        _wordAnimationCts?.Cancel();
        _wordAnimationCts = new CancellationTokenSource();
        await AnimateWordsAsync(_wordAnimationCts.Token);
    }

    public async Task AnimateWordsAsync(CancellationToken ct, Task? startSignal = null)
    {
        if (Words.Count == 0 || IsSingleWord) return;
        if (startSignal != null) await startSignal;

        ClearWordHighlights();

        var wordDurations = _syncService.GetWordDurationsInMs(0, Words.Count, Words, IsSlowMode);
        
        // Calculate relative start times for ALL words
        var wordStartTimes = new List<double>();
        double currentOffset = 0;
        foreach (var duration in wordDurations)
        {
            wordStartTimes.Add(currentOffset);
            currentOffset += duration;
        }

        TimeSpan audioStartTime = TimeSpan.Zero;
        bool startTimeCaptured = false;
        DateTime loopStartTime = DateTime.UtcNow;

        while (!ct.IsCancellationRequested)
        {
            if (!IsPlaying)
            {
                await Task.Delay(50, ct).ContinueWith(_ => { });
                continue;
            }

            var player = IsStreamingMode ? StreamingPlayer : null;
            TimeSpan currentPos = player?.CurrentPosition ?? CurrentPosition;

            bool forceStart = !startTimeCaptured && (DateTime.UtcNow - loopStartTime).TotalMilliseconds > 500;

            if (!startTimeCaptured && (currentPos > TimeSpan.Zero || forceStart))
            {
                audioStartTime = currentPos;
                startTimeCaptured = true;
            }

            if (startTimeCaptured)
            {
                double elapsedMs = (currentPos - audioStartTime).TotalMilliseconds;
                
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
            }

            await Task.Delay(30, ct).ContinueWith(_ => { });
        }

        ClearWordHighlights();
    }
    private void ClearWordHighlights()
    {
        _wordAnimationCts?.Cancel();
        foreach (var w in Words)
        {
            w.IsInActiveChunk = false;
            w.IsActiveWord = false;
        }
    }
}
