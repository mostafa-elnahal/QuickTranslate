using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using QuickTranslate.Services.Audio;
using QuickTranslate.Services.Pronunciation;

namespace QuickTranslate.Services;

public class AudioPlaybackService : IAudioPlaybackService
{
    private readonly IStreamingAudioPlayerFactory _audioPlayerFactory;
    private readonly IAudioStreamingService _streamingService;

    private IStreamingAudioPlayer? _player;
    private CancellationTokenSource? _streamingCts;

    public AudioPlaybackService(
        IStreamingAudioPlayerFactory audioPlayerFactory,
        IAudioStreamingService streamingService)
    {
        _audioPlayerFactory = audioPlayerFactory;
        _streamingService = streamingService;
    }

    public IStreamingAudioPlayer? Player => _player;
    public bool IsPlaying => _player?.IsPlaying ?? false;
    public bool IsPaused => _player?.IsPaused ?? false;
    public TimeSpan CurrentPosition => _player?.CurrentPosition ?? TimeSpan.Zero;
    public TimeSpan TotalDuration => _player?.TotalDuration ?? TimeSpan.Zero;

    private float _volume = 1.0f;
    public float Volume
    {
        get => _volume;
        set
        {
            _volume = value;
            if (_player != null) _player.Volume = value;
        }
    }

    public event EventHandler? PlaybackCompleted;
    public event EventHandler? SampleEnqueued;

    public async Task StartAsync(IList<string> chunks, string languageCode, bool slowMode,
        Action<int, Task>? onChunkStarted = null)
    {
        Stop();

        _player = _audioPlayerFactory.CreatePlayer();
        _streamingCts = new CancellationTokenSource();
        var ct = _streamingCts.Token;

        _player.BeginStreaming();

        _player.PlaybackCompleted += OnPlaybackCompleted;
        if (SampleEnqueued != null)
            _player.SampleEnqueued += OnSampleEnqueued;

        try
        {
            var result = await _streamingService.StreamTextAsync(
                chunks, languageCode, slowMode, _player,
                onChunkStarted ?? ((_, _) => { }),
                ct);

            if (!result.IsSuccess)
            {
                MessageBox.Show(result.Message, "Streaming Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"AudioPlaybackService streaming error: {ex.Message}");
        }
        finally
        {
            _player?.EndStreaming();
        }
    }

    public void Play()
    {
        if (_player == null) return;
        if (_player.IsPaused)
            _player.Resume();
        else
            _player.Play();
    }

    public void Pause() => _player?.Pause();

    public void Resume() => _player?.Resume();

    public void Stop()
    {
        if (_streamingCts != null)
        {
            try { _streamingCts.Cancel(); } catch (ObjectDisposedException) { }
            _streamingCts.Dispose();
            _streamingCts = null;
        }

        if (_player != null)
        {
            _player.PlaybackCompleted -= OnPlaybackCompleted;
            if (SampleEnqueued != null)
                _player.SampleEnqueued -= OnSampleEnqueued;
            _player.Stop();
            _player.Dispose();
            _player = null;
        }
    }

    public void Restart() => _player?.Restart();

    public void SetPosition(TimeSpan position) => _player?.SetPosition(position);

    private void OnPlaybackCompleted(object? sender, EventArgs e)
    {
        PlaybackCompleted?.Invoke(this, e);
    }

    private void OnSampleEnqueued(object? sender, EventArgs e)
    {
        SampleEnqueued?.Invoke(this, e);
    }

    public void Dispose()
    {
        Stop();
        GC.SuppressFinalize(this);
    }
}
