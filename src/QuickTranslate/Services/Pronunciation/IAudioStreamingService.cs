using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using QuickTranslate.Services.Audio;
using QuickTranslate.Models;

namespace QuickTranslate.Services.Pronunciation;

public interface IAudioStreamingService
{
    /// <summary>
    /// Orchestrates the streaming of multiple text chunks to an audio player.
    /// </summary>
    /// <param name="chunks">Divided text to pronounce.</param>
    /// <param name="languageCode">Detected language.</param>
    /// <param name="slowMode">Whether to use slower speech.</param>
    /// <param name="player">The audio player to feed bytes into.</param>
    /// <param name="onChunkStarted">Callback invoked when a chunk starts playing (receiving first bytes).</param>
    /// <param name="cancellationToken">Token to abort streaming.</param>
    /// <returns>Result of the operation.</returns>
    Task<PronunciationResult<bool>> StreamTextAsync(
        IList<string> chunks,
        string languageCode,
        bool slowMode,
        IStreamingAudioPlayer player,
        Action<int, Task> onChunkStarted,
        CancellationToken cancellationToken);
}
