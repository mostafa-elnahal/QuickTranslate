using System;
using System.Collections.Generic;
using NAudio.Wave;
using System.Threading;
using System.Threading.Tasks;
using QuickTranslate.Models;

namespace QuickTranslate.Services.Audio;

/// <summary>
/// NAudio-based streaming audio player that can play PCM chunks as they arrive.
/// </summary>
public class NAudioStreamingPlayer : IStreamingAudioPlayer
{
    private BufferedWaveProvider? _bufferedWaveProvider;
    private WaveOutEvent? _waveOut;
    private const long MaxHistoryBytes = 50 * 1024 * 1024; // 50MB cap on PCM history

    private bool _isPlaying;
    private bool _isStreamingActive;
    private bool _disposed;

    // Cache of all PCM data for replay support
    private readonly System.Collections.Generic.List<byte[]> _pcmHistory = new();
    private WaveFormat? _lastWaveFormat;
    private long _positionOffsetBytes = 0;

    // Per-chunk timepoint accumulation for word-level animation timing
    private readonly List<(IReadOnlyList<TimepointInfo> Timepoints, long ByteOffset)> _chunkTimepoints = new();
    // Per-chunk byte-offset boundaries for playback-position-based chunk highlighting (estimated timing)
    private readonly List<long> _chunkBoundaries = new();
    private long _totalHistoryBytes = 0;

    public bool IsPlaying => _isPlaying;

    public event EventHandler? PlaybackCompleted;
    public event EventHandler? SampleEnqueued;

    public void SetChunkTimepoints(IReadOnlyList<TimepointInfo> timepoints, int pcmDataLengthBytes)
    {
        _chunkTimepoints.Add((timepoints, _totalHistoryBytes));
    }

    public IReadOnlyList<TimepointInfo>? GetCombinedTimepoints()
    {
        if (_chunkTimepoints.Count == 0 || _lastWaveFormat == null)
            return null;

        var combined = new List<TimepointInfo>();
        foreach (var (timepoints, byteOffset) in _chunkTimepoints)
        {
            double offsetSeconds = (double)byteOffset / _lastWaveFormat.AverageBytesPerSecond;
            foreach (var tp in timepoints)
            {
                combined.Add(new TimepointInfo(tp.MarkName, tp.TimeSeconds + offsetSeconds));
            }
        }
        return combined;
    }

    public void RecordChunkBoundary()
    {
        _chunkBoundaries.Add(_totalHistoryBytes);
    }

    public IReadOnlyList<TimeSpan> GetChunkBoundaries()
    {
        if (_lastWaveFormat == null) return Array.Empty<TimeSpan>();
        var result = new TimeSpan[_chunkBoundaries.Count];
        for (int i = 0; i < _chunkBoundaries.Count; i++)
            result[i] = TimeSpan.FromSeconds(
                (double)_chunkBoundaries[i] / _lastWaveFormat.AverageBytesPerSecond);
        return result;
    }

    public void Initialize(int sampleRate = 24000, int channels = 1, int bitsPerSample = 16)
    {
        // Idempotency check: if already initialized with the same format, do nothing.
        if (_bufferedWaveProvider != null &&
            _bufferedWaveProvider.WaveFormat.SampleRate == sampleRate &&
            _bufferedWaveProvider.WaveFormat.Channels == channels &&
            _bufferedWaveProvider.WaveFormat.BitsPerSample == bitsPerSample)
        {
            System.Diagnostics.Debug.WriteLine($"[NAudioPlayer] Reusing existing player. Buffer: {_bufferedWaveProvider.BufferedBytes}/{_bufferedWaveProvider.BufferLength}");
            return;
        }

        System.Diagnostics.Debug.WriteLine($"[NAudioPlayer] Initializing new player: {sampleRate}Hz {channels}ch {bitsPerSample}bit");
        Stop(); // Clean up any previous session

        var waveFormat = new WaveFormat(sampleRate, bitsPerSample, channels);
        _bufferedWaveProvider = new BufferedWaveProvider(waveFormat)
        {
            BufferDuration = TimeSpan.FromMinutes(5), // 5 min buffer (~14MB at 24kHz) — ample for TTS audio
            DiscardOnBufferOverflow = false, // Block instead of discarding
            ReadFully = false // Essential: tells NAudio to stop and fire PlaybackStopped when buffer runs empty
        };

        _waveOut = new WaveOutEvent();
        _waveOut.Init(_bufferedWaveProvider);
        _waveOut.PlaybackStopped += OnPlaybackStopped;
        _lastWaveFormat = waveFormat;
        _positionOffsetBytes = 0;
    }

    /// <summary>
    /// Enqueues PCM data to the player's buffer for playback.
    /// Applies backpressure asynchronously if the internal buffer is full,
    /// yielding the calling thread instead of blocking it.
    /// </summary>
    /// <param name="pcmData">The PCM data to enqueue.</param>
    /// <param name="cancellationToken">Token to cancel the wait if the buffer is full.</param>
    public async Task EnqueueSamplesAsync(byte[] pcmData, CancellationToken cancellationToken = default)
    {
        if (_bufferedWaveProvider == null)
            throw new InvalidOperationException("Player not initialized. Call Initialize() first.");

        // Backpressure: If buffer is full, wait asynchronously until there's space.
        // This prevents "skipping" where new data overwrites/discards old data (if DiscardOnBufferOverflow was true),
        // or effectively pauses the download network stream until the user listens to some audio.
        while (_bufferedWaveProvider.BufferedBytes + pcmData.Length > _bufferedWaveProvider.BufferLength)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // If the player was stopped/disposed from another thread, abort.
            if (_bufferedWaveProvider == null) return;

            System.Diagnostics.Debug.WriteLine("[NAudioPlayer] Buffer full! waiting...");
            await Task.Delay(50, cancellationToken);
        }

        _bufferedWaveProvider.AddSamples(pcmData, 0, pcmData.Length);
        SampleEnqueued?.Invoke(this, EventArgs.Empty);

        // Cache for replay (capped to prevent unbounded memory growth)
        _pcmHistory.Add((byte[])pcmData.Clone());
        _totalHistoryBytes += pcmData.Length;
        while (_totalHistoryBytes > MaxHistoryBytes && _pcmHistory.Count > 0)
        {
            _totalHistoryBytes -= _pcmHistory[0].Length;
            _pcmHistory.RemoveAt(0);
        }

        // Auto-resume: if the WaveOutEvent stopped because the buffer ran dry
        // during active streaming, restart playback now that new data is available.
        if (_isStreamingActive && _waveOut != null && _waveOut.PlaybackState != PlaybackState.Playing)
        {
            _waveOut.Play();
            _isPlaying = true;
        }
    }

    public void Play()
    {
        if (_waveOut == null)
            throw new InvalidOperationException("Player not initialized. Call Initialize() first.");

        if (!_isPlaying)
        {
            _waveOut.Play();
            _isPlaying = true;
            IsPaused = false;
        }
    }

    public void Pause()
    {
        if (_waveOut == null) return;
        _waveOut.Pause();
        _isPlaying = false;
        IsPaused = true;
    }

    public void Resume()
    {
        if (_waveOut == null) return;
        _waveOut.Play();
        _isPlaying = true;
        IsPaused = false;
    }

    public void Stop()
    {
        if (_waveOut != null)
        {
            _waveOut.Stop();
            _waveOut.PlaybackStopped -= OnPlaybackStopped;
            _waveOut.Dispose();
            _waveOut = null;
        }

        _bufferedWaveProvider?.ClearBuffer();
        _bufferedWaveProvider = null;
        _isPlaying = false;
        _isStreamingActive = false;
        IsPaused = false;
        _pcmHistory.Clear(); // Clear history on full stop
        _chunkTimepoints.Clear();
        _chunkBoundaries.Clear();
        _totalHistoryBytes = 0;
    }

    public void Restart()
    {
        if (_pcmHistory.Count == 0 || _lastWaveFormat == null) return;

        // Reuse existing WaveOutEvent and BufferedWaveProvider instead of destroying
        // and recreating them. This avoids allocating a new 60-minute circular buffer
        // (~172MB) on every restart.
        if (_waveOut != null)
        {
            _waveOut.PlaybackStopped -= OnPlaybackStopped;
            _waveOut.Stop();
        }
        _bufferedWaveProvider?.ClearBuffer();

        // Re-enqueue all cached PCM data
        foreach (var chunk in _pcmHistory)
        {
            _bufferedWaveProvider.AddSamples(chunk, 0, chunk.Length);
        }

        _positionOffsetBytes = 0;

        // Resume playback
        if (_waveOut != null)
        {
            _waveOut.PlaybackStopped += OnPlaybackStopped;
            _waveOut.Play();
        }
        _isPlaying = true;
        IsPaused = false;
    }

    public void SetPosition(TimeSpan position)
    {
        if (_lastWaveFormat == null || _pcmHistory.Count == 0) return;

        _chunkTimepoints.Clear();
        _chunkBoundaries.Clear();

        long targetBytes = (long)(position.TotalSeconds * _lastWaveFormat.AverageBytesPerSecond);
        targetBytes -= targetBytes % _lastWaveFormat.BlockAlign; // Align to block boundary

        bool wasPlaying = _isPlaying;

        // Reuse existing WaveOutEvent and BufferedWaveProvider instead of
        // recreating them (avoids ~172MB circular buffer allocation per seek).
        if (_waveOut != null)
        {
            _waveOut.PlaybackStopped -= OnPlaybackStopped;
            _waveOut.Stop();
        }
        _bufferedWaveProvider?.ClearBuffer();

        long currentByte = 0;
        foreach (var chunk in _pcmHistory)
        {
            if (currentByte + chunk.Length <= targetBytes)
            {
                currentByte += chunk.Length;
                continue;
            }

            if (currentByte < targetBytes)
            {
                int skip = (int)(targetBytes - currentByte);
                int remaining = chunk.Length - skip;
                // Ensure skip and remaining are block aligned? chunk length should be properly aligned theoretically, but skip might not be?
                // targetBytes is aligned, currentByte is sum of chunk lengths (typically aligned).
                skip -= skip % _lastWaveFormat.BlockAlign;
                remaining = chunk.Length - skip;

                _bufferedWaveProvider.AddSamples(chunk, skip, remaining);
                currentByte += chunk.Length;
            }
            else
            {
                _bufferedWaveProvider.AddSamples(chunk, 0, chunk.Length);
                currentByte += chunk.Length;
            }
        }

        _positionOffsetBytes = targetBytes;

        if (_waveOut != null)
        {
            _waveOut.PlaybackStopped += OnPlaybackStopped;
            if (wasPlaying)
            {
                _waveOut.Play();
                _isPlaying = true;
                IsPaused = false;
            }
        }
    }

    public float Volume
    {
        get => _waveOut?.Volume ?? 1.0f;
        set
        {
            if (_waveOut != null) _waveOut.Volume = value;
        }
    }

    public TimeSpan CurrentPosition
    {
        get
        {
            if (_waveOut == null || _lastWaveFormat == null) return TimeSpan.Zero;
            long pos = _waveOut.GetPosition();
            return TimeSpan.FromSeconds((double)(_positionOffsetBytes + pos) / _lastWaveFormat.AverageBytesPerSecond);
        }
    }

    public TimeSpan TotalDuration
    {
        get
        {
            if (_lastWaveFormat == null) return TimeSpan.Zero;
            long totalBytes = 0;
            foreach (var chunk in _pcmHistory) totalBytes += chunk.Length;
            return TimeSpan.FromSeconds((double)totalBytes / _lastWaveFormat.AverageBytesPerSecond);
        }
    }

    public bool IsPaused { get; private set; }

    public bool IsStreamingActive => _isStreamingActive;

    public void BeginStreaming()
    {
        _isStreamingActive = true;
    }

    public void EndStreaming()
    {
        _isStreamingActive = false;

        // If the buffer already drained while we were still "streaming",
        // the OnPlaybackStopped handler suppressed the event and kept
        // _isPlaying = true to avoid UI flicker. Now that streaming is
        // done, check the actual WaveOut state to see if playback has
        // already stopped (buffer underrun). If so, fire the event now.
        bool actuallyPlaying = _waveOut != null && _waveOut.PlaybackState == PlaybackState.Playing;
        if (!actuallyPlaying && _bufferedWaveProvider != null && _bufferedWaveProvider.BufferedBytes == 0)
        {
            _isPlaying = false;
            PlaybackCompleted?.Invoke(this, EventArgs.Empty);
        }
    }

    private void OnPlaybackStopped(object? sender, StoppedEventArgs e)
    {
        if (_isStreamingActive)
        {
            // Buffer ran dry between SSE chunks — suppress the event.
            // The WaveOutEvent has stopped itself, but we keep _isPlaying true
            // so the UI doesn't flicker. Play() will be called again when
            // new samples are enqueued via EnqueueSamplesAsync.
            return;
        }

        _isPlaying = false;
        PlaybackCompleted?.Invoke(this, EventArgs.Empty);
    }

    public void Dispose()
    {
        if (_disposed) return;
        Stop();
        _disposed = true;
        GC.SuppressFinalize(this);
    }
}
