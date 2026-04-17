using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using QuickTranslate.Services.Audio;
using QuickTranslate.Models;

namespace QuickTranslate.Services.Pronunciation;

public class AudioStreamingService : IAudioStreamingService
{
    private readonly IPronunciationService _pronunciationService;

    public AudioStreamingService(IPronunciationService pronunciationService)
    {
        _pronunciationService = pronunciationService;
    }

    public async Task<PronunciationResult<bool>> StreamTextAsync(
        IList<string> chunks,
        string languageCode,
        bool slowMode,
        IStreamingAudioPlayer player,
        Action<int, Task> onChunkStarted,
        CancellationToken cancellationToken)
    {
        try
        {
            for (int i = 0; i < chunks.Count; i++)
            {
                if (cancellationToken.IsCancellationRequested) 
                    break;

                // Create a signal for the first byte of THIS chunk
                var chunkStartedTcs = new TaskCompletionSource<bool>();
                void OnSampleEnqueued(object? s, EventArgs e) => chunkStartedTcs.TrySetResult(true);
                player.SampleEnqueued += OnSampleEnqueued;

                try
                {
                    // Notify caller that chunk i has context and a start signal
                    onChunkStarted?.Invoke(i, chunkStartedTcs.Task);

                    var result = await _pronunciationService.StreamAudioAsync(
                        chunks[i],
                        languageCode,
                        slowMode,
                        player,
                        cancellationToken);

                    if (!result.IsSuccess)
                    {
                        return result;
                    }
                }
                finally
                {
                    player.SampleEnqueued -= OnSampleEnqueued;
                }
            }

            return PronunciationResult<bool>.Success(true);
        }
        catch (Exception ex)
        {
            return PronunciationResult<bool>.Failure($"Streaming failed: {ex.Message}");
        }
    }
}
