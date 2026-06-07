using System;
using System.Threading;
using System.Threading.Tasks;
using QuickTranslate.Models;
using QuickTranslate.Services.Audio;

namespace QuickTranslate.Services;

public interface IPronunciationService
{
    /// <summary>
    /// Whether the active provider supports streaming audio.
    /// </summary>
    bool SupportsStreaming { get; }

    /// <summary>
    /// The maximum number of characters allowed per chunk for the active provider.
    /// </summary>
    int MaxChunkSize { get; }

    /// <summary>
    /// The level of timing support the active provider offers for word-level highlighting.
    /// </summary>
    TimingSupportLevel TimingSupport { get; }

    Task<PronunciationResult<PronunciationData>> GetPronunciationAsync(string text);
    Task<PronunciationResult<Uri?>> GetAudioUriAsync(string text, string languageCode, bool slowMode);

    /// <summary>
    /// Clears any in-memory caches held by the active provider (e.g., ElevenLabs audio cache).
    /// Called when the pronunciation popup is hidden to release retained PCM data.
    /// </summary>
    void ClearProviderCache();

    /// <summary>
    /// Streams audio directly to the player.
    /// </summary>
    Task<PronunciationResult<bool>> StreamAudioAsync(
        string text,
        string languageCode,
        bool slowMode,
        IStreamingAudioPlayer player,
        CancellationToken cancellationToken = default);
}
