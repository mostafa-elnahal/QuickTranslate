using System;
using System.Threading;
using System.Threading.Tasks;
using QuickTranslate.Services.Audio;
using QuickTranslate.Services.Helpers;
using CommunityToolkit.Mvvm.Input;
using System.Linq;

namespace QuickTranslate.ViewModels;

public partial class PronunciationViewModel
{
    #region Commands

    [RelayCommand]
    private void PlayPause()
    {
        if (IsStreamingMode)
        {
            if (_streamingPlayer == null) return;
            if (_streamingPlayer.IsPlaying)
            {
                _streamingPlayer.Pause();
                IsPlaying = false;
            }
            else if (_streamingPlayer.IsPaused)
            {
                _streamingPlayer.Resume();
                IsPlaying = true;
            }
            else
            {
                Restart();
            }
        }
        else
        {
            if (IsPlaying)
            {
                RequestPauseFromView?.Invoke(this, EventArgs.Empty);
                IsPlaying = false;
            }
            else
            {
                RequestPlayFromView?.Invoke(this, EventArgs.Empty);
                IsPlaying = true;
            }
        }
    }

    [RelayCommand(CanExecute = nameof(CanRestart))]
    private void Restart()
    {
        if (IsStreamingMode)
        {
            if (_streamingPlayer != null)
            {
                _streamingPlayer.Restart();
                IsPlaying = true;
            }
        }
        else
        {
            RequestRestartFromView?.Invoke(this, EventArgs.Empty);
            IsPlaying = true;
        }
    }

    private bool CanRestart => IsStreamingMode ? (_streamingPlayer != null && !IsDownloadingChunks) : true;

    #endregion

    private async Task UpdateAudioUriAsync()
    {
        if (string.IsNullOrEmpty(OriginalText)) return;

        IsLoading = true;
        StatusMessage = string.Empty;
        AudioUri = null;
        StopStreaming();

        try
        {
            int currentGen = _pronunciationGeneration;

            if (_pronunciationService.SupportsStreaming)
            {
                IsStreamingMode = true;
                _streamingPlayer = new NAudioStreamingPlayer();
                _streamingCts = new CancellationTokenSource();

                _streamingPlayer.PlaybackCompleted += (s, e) =>
                {
                    if (!IsDownloadingChunks)
                    {
                        IsPlaying = false;
                        ClearWordHighlights();
                    }
                };

                var chunks = TextChunker.ChunkText(Words.ToList(), _pronunciationService.MaxChunkSize).ToList();
                _chunkWordRanges = chunks.Select(c => (c.StartWordIndex, c.EndWordIndex)).ToList();
                var chunksList = chunks.Select(c => c.Text).ToList();

                IsDownloadingChunks = true;
                IsPlaying = true;
                
                // Create a global start signal for the first bit of audio received
                var firstSampleTcs = new TaskCompletionSource<bool>();
                void OnFirstSample(object? s, EventArgs e)
                {
                    firstSampleTcs.TrySetResult(true);
                    if (_streamingPlayer != null) _streamingPlayer.SampleEnqueued -= OnFirstSample;
                }
                _streamingPlayer.SampleEnqueued += OnFirstSample;

                // Fire the global animation exactly once for the entire stream
                _ = AnimateWordsAsync(_streamingCts.Token, firstSampleTcs.Task);

                // Run streaming in the background so the UI (and initial playback) doesn't block
                _ = Task.Run(async () =>
                {
                    try
                    {
                        var result = await _streamingService.StreamTextAsync(
                            chunksList,
                            _detectedLanguageCode,
                            IsSlowMode,
                            _streamingPlayer,
                            (idx, startTask) => { }, // Ignore per-chunk activation, using global loop
                            _streamingCts.Token);

                        if (_pronunciationGeneration != currentGen)
                        {
                            return;
                        }

                        if (!result.IsSuccess) StatusMessage = result.Message;
                    }
                    catch (Exception ex)
                    {
                        if (_pronunciationGeneration == currentGen)
                        {
                            StatusMessage = "Audio streaming failed.";
                            System.Diagnostics.Debug.WriteLine($"Streaming background error: {ex.Message}");
                        }
                    }
                    finally
                    {
                        if (_pronunciationGeneration == currentGen)
                            IsDownloadingChunks = false;
                    }
                });
            }
            else
            {
                IsStreamingMode = false;
                var result = await _pronunciationService.GetAudioUriAsync(OriginalText, _detectedLanguageCode, IsSlowMode);

                if (_pronunciationGeneration != currentGen) return;

                if (result.IsSuccess) AudioUri = result.Data;
                else StatusMessage = result.Message;
            }
        }
        catch (Exception ex)
        {
            StatusMessage = "Audio error.";
            System.Diagnostics.Debug.WriteLine($"UpdateAudioUri Error: {ex.Message}");
            StopStreaming();
        }
        finally
        {
            IsLoading = false;
        }
    }

    public void StopStreaming()
    {
        IsDownloadingChunks = false;
        _wordAnimationCts?.Cancel();
        _streamingCts?.Cancel();
        _streamingPlayer?.Stop();
        _streamingPlayer?.Dispose();
        _streamingPlayer = null;
        _streamingCts?.Dispose();
        _streamingCts = null;
        ClearWordHighlights();
    }
}
