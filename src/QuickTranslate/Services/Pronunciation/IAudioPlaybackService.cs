using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using QuickTranslate.Services.Audio;

namespace QuickTranslate.Services;

public interface IAudioPlaybackService : IDisposable
{
    IStreamingAudioPlayer? Player { get; }
    bool IsPlaying { get; }
    bool IsPaused { get; }
    TimeSpan CurrentPosition { get; }
    TimeSpan TotalDuration { get; }
    float Volume { get; set; }

    event EventHandler? PlaybackCompleted;
    event EventHandler? SampleEnqueued;

    Task StartAsync(IList<string> chunks, string languageCode, bool slowMode,
        Action<int, Task>? onChunkStarted = null);
    void Play();
    void Pause();
    void Resume();
    void Stop();
    void Restart();
    void SetPosition(TimeSpan position);
}
