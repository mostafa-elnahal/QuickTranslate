using System;

namespace QuickTranslate.Services.Audio;

/// <summary>
/// Interface for streaming audio playback.
/// </summary>
public interface IStreamingAudioPlayer : IDisposable
{
    /// <summary>
    /// Initializes the player for a new audio stream.
    /// </summary>
    /// <param name="sampleRate">Sample rate in Hz (e.g., 24000)</param>
    /// <param name="channels">Number of channels (1 = mono, 2 = stereo)</param>
    /// <param name="bitsPerSample">Bits per sample (e.g., 16)</param>
    void Initialize(int sampleRate = 24000, int channels = 1, int bitsPerSample = 16);

    /// <summary>
    /// Enqueues PCM audio data for playback.
    /// </summary>
    void EnqueueSamples(byte[] pcmData);

    /// <summary>
    /// Starts playback.
    /// </summary>
    void Play();

    /// <summary>
    /// Pauses playback.
    /// </summary>
    void Pause();

    /// <summary>
    /// Resumes playback.
    /// </summary>
    void Resume();

    /// <summary>
    /// Restarts playback from the beginning using cached data.
    /// </summary>
    void Restart();

    /// <summary>
    /// Stops playback and clears the buffer.
    /// </summary>
    void Stop();

    /// <summary>
    /// Gets or sets the volume (0.0 to 1.0).
    /// </summary>
    float Volume { get; set; }

    bool IsPaused { get; }

    /// <summary>
    /// Gets whether audio is currently playing.
    /// </summary>
    bool IsPlaying { get; }

    /// <summary>
    /// Event raised when playback completes.
    /// </summary>
    event EventHandler? PlaybackCompleted;
}
