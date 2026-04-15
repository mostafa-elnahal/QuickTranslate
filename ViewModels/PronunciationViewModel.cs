using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using QuickTranslate.Helpers;
using QuickTranslate.Services;
using QuickTranslate.Services.Audio;
using QuickTranslate.Services.Helpers;
using QuickTranslate.Models;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace QuickTranslate.ViewModels;

/// <summary>
/// ViewModel for the pronunciation popup window.
/// Displays the source word, IPA phonetics, and handles audio playback.
/// </summary>
public partial class PronunciationViewModel : ObservableObject, IDisposable
{
    // Events to request View actions (since MediaElement is in View)
    public event EventHandler? RequestPlayFromView;
    public event EventHandler? RequestPauseFromView;
    public event EventHandler? RequestRestartFromView;

    private readonly IPronunciationService _pronunciationService;
    private readonly ISettingsService _settingsService;
    private IStreamingAudioPlayer? _streamingPlayer;
    private CancellationTokenSource? _streamingCts;

    [ObservableProperty]
    private string _originalText = string.Empty;

    [ObservableProperty]
    private string _phoneticsDisplay = string.Empty;

    partial void OnPhoneticsDisplayChanged(string value) => OnPropertyChanged(nameof(HasPhonetics));

    private string _detectedLanguageCode = "en";

    [ObservableProperty]
    private Uri? _audioUri;

    [ObservableProperty]
    private bool _isPlaying;

    [ObservableProperty]
    private bool _isVisible = false;

    [ObservableProperty]
    private bool _isStreamingMode;

    [ObservableProperty]
    private bool _isDownloadingChunks;

    [ObservableProperty]
    private TimeSpan _totalDuration;

    [ObservableProperty]
    private TimeSpan _currentPosition;

    [ObservableProperty]
    private string _languageName = "English";

    [ObservableProperty]
    private bool _isSingleWord;

    [ObservableProperty]
    private bool _isSlowMode;

    async partial void OnIsSlowModeChanged(bool value)
    {
        await UpdateAudioUriAsync();
    }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RestartCommand))]
    private bool _isLoading;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    /// <summary>
    /// Generation counter to track translation sessions.
    /// Incremented on each new translation to detect stale async callbacks.
    /// </summary>
    private int _pronunciationGeneration = 0;

    public int PronunciationGeneration => _pronunciationGeneration;

    public PronunciationViewModel(
        IPronunciationService pronunciationService,
        ISettingsService settingsService)
    {
        _pronunciationService = pronunciationService;
        _settingsService = settingsService;
    }

    #region Commands

    [RelayCommand]
    private void PlayPause()
    {
        System.Diagnostics.Debug.WriteLine($"[PlayPause] Clicked. _streamingPlayer={_streamingPlayer != null}, IsPlaying={IsPlaying}");

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
        System.Diagnostics.Debug.WriteLine($"[Restart] Clicked. _streamingPlayer={_streamingPlayer != null}");

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

    #region Properties

    public System.Collections.ObjectModel.ObservableCollection<SyllableItem> Syllables { get; } = new();

    public bool HasPhonetics => !string.IsNullOrEmpty(PhoneticsDisplay);

    public double FontSize => _settingsService.Settings.FontSize * 2.0;
    public string FontFamily => _settingsService.Settings.FontFamily;
    public double PhoneticsFontSize => _settingsService.Settings.FontSize * 1.5;

    #endregion

    #region Public Methods

    public void PrepareForLoading(string text)
    {
        _pronunciationGeneration++;
        ResetData();
        OriginalText = text?.Trim() ?? string.Empty;
        IsLoading = true;
        StatusMessage = string.Empty;
    }

    public async Task LoadPronunciationAsync(string text)
    {
        bool alreadyPrepared = IsLoading && OriginalText == text?.Trim();

        if (!alreadyPrepared)
        {
            _pronunciationGeneration++;

            if (string.IsNullOrWhiteSpace(text))
            {
                ResetData();
                return;
            }

            ResetData();
            OriginalText = text.Trim();
            IsLoading = true;
            StatusMessage = string.Empty;
        }

        try
        {
            var words = OriginalText.Split(new[] { ' ', '\r', '\n', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            IsSingleWord = words.Length == 1;

            var result = await _pronunciationService.GetPronunciationAsync(OriginalText);

            if (!result.IsSuccess)
            {
                StatusMessage = result.Message;
                Syllables.Add(new SyllableItem { Text = OriginalText });
                await UpdateAudioUriAsync();
                return;
            }

            var data = result.Data!;

            _detectedLanguageCode = data.DetectedLanguageCode;
            LanguageName = GetLanguageName(_detectedLanguageCode);

            if (IsSingleWord)
            {
                if (!string.IsNullOrEmpty(data.Phonetics))
                {
                    PhoneticsDisplay = $"/{data.Phonetics}/";
                }

                foreach (var s in data.Syllables)
                {
                    Syllables.Add(s);
                }
            }
            else
            {
                PhoneticsDisplay = string.Empty;
                Syllables.Clear();
            }

            IsSlowMode = false;
            OnPropertyChanged(nameof(IsSlowMode));

            if (data.AudioUri != null)
            {
                AudioUri = data.AudioUri;
            }
            else
            {
                await UpdateAudioUriAsync();
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Pronunciation Error: {ex.Message}");
            Syllables.Add(new SyllableItem { Text = OriginalText });
            StatusMessage = "An unexpected error occurred.";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void ResetData()
    {
        OriginalText = string.Empty;
        Syllables.Clear();
        AudioUri = null;
        PhoneticsDisplay = string.Empty;
        IsPlaying = false;
    }

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
                    }
                };

                var chunks = TextChunker.ChunkText(OriginalText);
                IsDownloadingChunks = true;
                IsPlaying = true;

                foreach (var chunk in chunks)
                {
                    if (_streamingCts.IsCancellationRequested) break;

                    var result = await _pronunciationService.StreamAudioAsync(
                        chunk,
                        _detectedLanguageCode,
                        IsSlowMode,
                        _streamingPlayer,
                        _streamingCts.Token);

                    if (_pronunciationGeneration != currentGen)
                    {
                        StopStreaming();
                        return;
                    }

                    if (!result.IsSuccess)
                    {
                        StatusMessage = $"Streaming interrupted: {result.Message}";
                        break;
                    }
                }

                IsDownloadingChunks = false;
            }
            else
            {
                IsStreamingMode = false;
                var result = await _pronunciationService.GetAudioUriAsync(OriginalText, _detectedLanguageCode, IsSlowMode);

                if (_pronunciationGeneration != currentGen) return;

                if (result.IsSuccess)
                {
                    AudioUri = result.Data;
                }
                else
                {
                    StatusMessage = result.Message;
                }
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
        _streamingCts?.Cancel();
        _streamingPlayer?.Stop();
        _streamingPlayer?.Dispose();
        _streamingPlayer = null;
        _streamingCts?.Dispose();
        _streamingCts = null;
    }

    public async Task AnimateSyllablesAsync(TimeSpan totalDuration)
    {
        if (Syllables.Count == 0) return;

        foreach (var s in Syllables) s.IsActive = false;

        double durationMs = totalDuration.TotalMilliseconds;
        int syllableCount = Syllables.Count;
        int interval = (int)(durationMs / syllableCount);

        for (int i = 0; i < syllableCount; i++)
        {
            if (!IsPlaying) break;

            if (i > 0) Syllables[i - 1].IsActive = false;

            Syllables[i].IsActive = true;

            await Task.Delay(interval);
        }

        if (Syllables.Count > 0) Syllables[Syllables.Count - 1].IsActive = false;
    }

    public void HideWindow()
    {
        _pronunciationGeneration++;
        IsVisible = false;
        IsPlaying = false;
        StopStreaming();
    }

    #endregion

    private string GetLanguageName(string code)
    {
        if (string.IsNullOrEmpty(code)) return "Unknown";
        try
        {
            var culture = new System.Globalization.CultureInfo(code);
            return culture.DisplayName;
        }
        catch
        {
            return code.ToUpper();
        }
    }

    #region IDisposable

    public void Dispose()
    {
        StopStreaming();
        GC.SuppressFinalize(this);
    }

    #endregion
}
