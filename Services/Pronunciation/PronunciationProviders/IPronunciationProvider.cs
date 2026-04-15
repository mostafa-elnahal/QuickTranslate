using System;
using System.Threading;
using System.Threading.Tasks;
using QuickTranslate.Models;
using QuickTranslate.Services.Audio;

namespace QuickTranslate.Services.Providers;

public interface IPronunciationProvider
{
    string Name { get; }

    /// <summary>
    /// Whether this provider supports streaming audio output.
    /// </summary>
    bool SupportsStreaming { get; }

    Task<PronunciationResult<PronunciationData>> GetPronunciationAsync(string text);

    /// <summary>
    /// Gets audio as a file URI (non-streaming, for MediaElement).
    /// </summary>
    Task<PronunciationResult<Uri?>> GetAudioUriAsync(string text, string languageCode, bool slowMode);

    /// <summary>
    /// Streams audio directly to the player (streaming mode).
    /// </summary>
    Task<PronunciationResult<bool>> StreamAudioAsync(
        string text,
        string languageCode,
        bool slowMode,
        IStreamingAudioPlayer player,
        CancellationToken cancellationToken = default);
}
