using System;
using NAudio.Wave;
using System.Threading;

namespace QuickTranslate.Services.Audio;

/// <summary>
/// NAudio-based streaming audio player that can play PCM chunks as they arrive.
/// </summary>
public class NAudioStreamingPlayer : IStreamingAudioPlayer
{
    private BufferedWaveProvider? _bufferedWaveProvider;
    private WaveOutEvent? _waveOut;
    private bool _isPlaying;
    private bool _disposed;

    // Cache of all PCM data for replay support
    private readonly System.Collections.Generic.List<byte[]> _pcmHistory = new();
    private WaveFormat? _lastWaveFormat;

    public bool IsPlaying => _isPlaying;

    public event EventHandler? PlaybackCompleted;

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
            BufferDuration = TimeSpan.FromMinutes(60), // Huge buffer (60 mins) to allow full pre-download
            DiscardOnBufferOverflow = false // Block instead of discarding
        };

        _waveOut = new WaveOutEvent();
        _waveOut.Init(_bufferedWaveProvider);
        _waveOut.PlaybackStopped += OnPlaybackStopped;
        _lastWaveFormat = waveFormat;
    }

    public void EnqueueSamples(byte[] pcmData)
    {
        if (_bufferedWaveProvider == null)
            throw new InvalidOperationException("Player not initialized. Call Initialize() first.");

        // Backpressure: If buffer is full, wait until there's space.
        // This prevents "skipping" where new data overwrites/discards old data (if DiscardOnBufferOverflow was true),
        // or effectively pauses the download network stream until the user listens to some audio.
        while (_bufferedWaveProvider.BufferedBytes + pcmData.Length > _bufferedWaveProvider.BufferLength)
        {
            // If the player was stopped/disposed from another thread, abort.
            if (_bufferedWaveProvider == null) return;

            System.Diagnostics.Debug.WriteLine("[NAudioPlayer] Buffer full! waiting...");
            Thread.Sleep(50); // Small wait
        }

        _bufferedWaveProvider.AddSamples(pcmData, 0, pcmData.Length);

        // Cache for replay
        _pcmHistory.Add((byte[])pcmData.Clone());
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
        IsPaused = false;
        _pcmHistory.Clear(); // Clear history on full stop
    }

    public void Restart()
    {
        if (_pcmHistory.Count == 0 || _lastWaveFormat == null) return;

        // Stop current playback without clearing history
        if (_waveOut != null)
        {
            _waveOut.Stop();
            _waveOut.PlaybackStopped -= OnPlaybackStopped;
            _waveOut.Dispose();
            _waveOut = null;
        }
        _bufferedWaveProvider?.ClearBuffer();

        // Re-initialize
        _bufferedWaveProvider = new BufferedWaveProvider(_lastWaveFormat)
        {
            BufferDuration = TimeSpan.FromMinutes(60),
            DiscardOnBufferOverflow = false
        };
        _waveOut = new WaveOutEvent();
        _waveOut.Init(_bufferedWaveProvider);
        _waveOut.PlaybackStopped += OnPlaybackStopped;

        // Re-enqueue all cached PCM data
        foreach (var chunk in _pcmHistory)
        {
            _bufferedWaveProvider.AddSamples(chunk, 0, chunk.Length);
        }

        // Start playback
        _waveOut.Play();
        _isPlaying = true;
        IsPaused = false;
    }

    public float Volume
    {
        get => _waveOut?.Volume ?? 1.0f;
        set
        {
            if (_waveOut != null) _waveOut.Volume = value;
        }
    }

    public bool IsPaused { get; private set; }

    private void OnPlaybackStopped(object? sender, StoppedEventArgs e)
    {
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
