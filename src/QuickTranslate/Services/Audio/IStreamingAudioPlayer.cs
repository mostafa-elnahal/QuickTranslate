using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using QuickTranslate.Models;

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
    /// Applies backpressure asynchronously if the internal buffer is full.
    /// </summary>
    /// <param name="pcmData">The PCM audio data to enqueue.</param>
    /// <param name="cancellationToken">Token to cancel the wait if the buffer is full.</param>
    Task EnqueueSamplesAsync(byte[] pcmData, CancellationToken cancellationToken = default);

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
    /// Gets the current playback position.
    /// </summary>
    TimeSpan CurrentPosition { get; }

    /// <summary>
    /// Gets the total duration of the audio enqueued so far.
    /// </summary>
    TimeSpan TotalDuration { get; }

    /// <summary>
    /// Sets the playback position.
    /// </summary>
    void SetPosition(TimeSpan position);

    /// <summary>
    /// Indicates that an active stream is feeding data to the player.
    /// While true, buffer-drain events are suppressed.
    /// </summary>
    bool IsStreamingActive { get; }

    /// <summary>
    /// Signals that a streaming session has started. PlaybackCompleted
    /// will be suppressed until <see cref="EndStreaming"/> is called.
    /// </summary>
    void BeginStreaming();

    /// <summary>
    /// Signals that no more data will be streamed. If the buffer has
    /// already drained, PlaybackCompleted fires immediately.
    /// </summary>
    void EndStreaming();

    /// <summary>
    /// Provides per-word timing data for a chunk being enqueued.
    /// The player accumulates these across chunks, applying byte-offset-based
    /// time shifting so that <see cref="GetCombinedTimepoints"/> returns
    /// absolute timing relative to the start of the entire stream.
    /// </summary>
    /// <param name="timepoints">Word-level timepoints for this chunk, relative to chunk start.</param>
    /// <param name="pcmDataLengthBytes">Length of the PCM data for this chunk (for offset calculation).</param>
    void SetChunkTimepoints(IReadOnlyList<TimepointInfo> timepoints, int pcmDataLengthBytes);

    /// <summary>
    /// Records the current total PCM byte count as a chunk boundary.
    /// Used by providers without exact timing to enable playback-position-based chunk highlighting.
    /// </summary>
    void RecordChunkBoundary();

    /// <summary>
    /// Returns start times of each chunk relative to the stream beginning,
    /// derived from recorded byte boundaries and the audio format.
    /// Returns an empty list if no boundaries have been recorded.
    /// </summary>
    IReadOnlyList<TimeSpan> GetChunkBoundaries();

    /// <summary>
    /// Returns all chunk timepoints combined with absolute offsets,
    /// or null if no timepoints have been provided.
    /// </summary>
    IReadOnlyList<TimepointInfo>? GetCombinedTimepoints();

    /// <summary>
    /// Event raised when playback completes.
    /// </summary>
    event EventHandler? PlaybackCompleted;

    /// <summary>
    /// Event raised when the first sample of a new stream is enqueued.
    /// </summary>
    event EventHandler? SampleEnqueued;
}
