using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using QuickTranslate.Models;
using QuickTranslate.Services.Audio;

namespace QuickTranslate.Services;

public class WordHighlightService : IWordHighlightService
{
    public async Task AnimateWordsAsync(
        IAudioPlaybackService playbackService,
        IList<WordItem> words,
        CancellationToken ct,
        Task? startSignal = null)
    {
        if (words.Count == 0) return;
        if (startSignal != null) await startSignal;

        ClearWordHighlights(words);

        var wordStartTimes = BuildWordStartTimes(playbackService.Player, words.Count);

        while (!ct.IsCancellationRequested)
        {
            if (!playbackService.IsPlaying)
            {
                await Task.Delay(50, ct).ContinueWith(_ => { });
                continue;
            }

            var player = playbackService.Player;
            if (player == null) break;

            var playerTimepoints = player.GetCombinedTimepoints();
            int playerTimepointCount = playerTimepoints?.Count ?? 0;
            if (playerTimepointCount > wordStartTimes.Count)
            {
                wordStartTimes = BuildWordStartTimesFromTimepoints(playerTimepoints!);
            }

            TimeSpan currentPos = player.CurrentPosition;
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
                for (int i = 0; i < words.Count; i++)
                    words[i].IsActiveWord = (i == activeWordIndex);
            }

            double lastWordStart = wordStartTimes.Count > 0 ? wordStartTimes[^1] : 0;
            double exitMs;
            if (playerTimepointCount >= words.Count)
            {
                exitMs = player.TotalDuration.TotalMilliseconds;
            }
            else
            {
                exitMs = lastWordStart + 200;
            }

            if (elapsedMs > exitMs)
            {
                break;
            }

            await Task.Delay(30, ct).ContinueWith(_ => { });
        }

        ClearWordHighlights(words);
    }

    private static List<double> BuildWordStartTimes(IStreamingAudioPlayer? player, int wordCount)
    {
        var playerTimepoints = player?.GetCombinedTimepoints();
        if (playerTimepoints != null && playerTimepoints.Count > 0)
        {
            return BuildWordStartTimesFromTimepoints(playerTimepoints);
        }

        return new List<double>(wordCount);
    }

    private static List<double> BuildWordStartTimesFromTimepoints(IReadOnlyList<TimepointInfo> timepoints)
    {
        var startTimes = new List<double>(timepoints.Count);
        foreach (var tp in timepoints)
            startTimes.Add(tp.TimeSeconds * 1000.0);
        return startTimes;
    }

    private static void ClearWordHighlights(IList<WordItem> words)
    {
        foreach (var w in words)
            w.IsActiveWord = false;
    }
}
