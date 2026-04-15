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

namespace QuickTranslate.ViewModels;

/// <summary>
/// ViewModel for the pronunciation popup window.
/// Displays the source word, IPA phonetics, and handles audio playback.
/// </summary>
public class PronunciationViewModel : ViewModelBase, IDisposable
{
    // Events to request View actions (since MediaElement is in View)
    public event EventHandler? RequestPlayFromView;
    public event EventHandler? RequestPauseFromView;
    public event EventHandler? RequestRestartFromView;

    private readonly IPronunciationService _pronunciationService;
    private readonly ISettingsService _settingsService;
    private IStreamingAudioPlayer? _streamingPlayer;
    private CancellationTokenSource? _streamingCts;

    private string _originalText = string.Empty;
    private string _phoneticsDisplay = string.Empty;
    private string _detectedLanguageCode = "en";
    private Uri? _audioUri;
    private bool _isPlaying;
    private bool _isVisible = false;
    private int _pronunciationGeneration = 0;
    private bool _isStreamingMode;
    private bool _isDownloadingChunks;

    private TimeSpan _totalDuration;
    private TimeSpan _currentPosition;
    private string _languageName = "English";

    public PronunciationViewModel(
        IPronunciationService pronunciationService,
        ISettingsService settingsService)
    {
        _pronunciationService = pronunciationService;
        _settingsService = settingsService;

        // Initialize commands
        PlayPauseCommand = new RelayCommand(ExecutePlayPause);
        RestartCommand = new RelayCommand(ExecuteRestart, () => CanRestart);
    }

    #region Commands

    public ICommand PlayPauseCommand { get; }
    public ICommand RestartCommand { get; }

    private bool CanRestart => IsStreamingMode ? (_streamingPlayer != null && !_isDownloadingChunks) : true;

    private void ExecutePlayPause()
    {
        System.Diagnostics.Debug.WriteLine($"[PlayPause] Clicked. _streamingPlayer={_streamingPlayer != null}, IsPlaying={IsPlaying}");

        if (_isStreamingMode)
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
                ExecuteRestart();
            }
        }
        else
        {
            // Non-streaming (MediaElement)
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

    private void ExecuteRestart()
    {
        System.Diagnostics.Debug.WriteLine($"[Restart] Clicked. _streamingPlayer={_streamingPlayer != null}");

        if (_isStreamingMode)
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

    #endregion

    #region Properties

    /// <summary>
    /// Gets the current pronunciation generation. Used to guard against race conditions.
    /// </summary>
    public int PronunciationGeneration => _pronunciationGeneration;

    /// <summary>
    /// The original text to pronounce.
    /// </summary>
    public string OriginalText
    {
        get => _originalText;
        set => SetProperty(ref _originalText, value);
    }

    /// <summary>
    /// Collection of syllables to display and animate.
    /// </summary>
    public System.Collections.ObjectModel.ObservableCollection<SyllableItem> Syllables { get; } = new();

    /// <summary>
    /// URI for TTS audio playback.
    /// </summary>
    public Uri? AudioUri
    {
        get => _audioUri;
        set => SetProperty(ref _audioUri, value);
    }

    /// <summary>
    /// Whether audio is currently playing.
    /// </summary>
    public bool IsPlaying
    {
        get => _isPlaying;
        set => SetProperty(ref _isPlaying, value);
    }

    private bool _isSingleWord;
    public bool IsSingleWord
    {
        get => _isSingleWord;
        set => SetProperty(ref _isSingleWord, value);
    }

    private bool _isSlowMode;
    public bool IsSlowMode
    {
        get => _isSlowMode;
        set
        {
            if (SetProperty(ref _isSlowMode, value))
            {
                // Refresh audio URI when slow mode changes via Service (fire and forget)
                _ = UpdateAudioUriAsync();
            }
        }
    }

    public bool IsVisible
    {
        get => _isVisible;
        set => SetProperty(ref _isVisible, value);
    }

    /// <summary>
    /// IPA phonetics display string (e.g., "/ˌpɹɑfəˈlæksɪs/")
    /// </summary>
    public string PhoneticsDisplay
    {
        get => _phoneticsDisplay;
        set
        {
            if (SetProperty(ref _phoneticsDisplay, value))
            {
                OnPropertyChanged(nameof(HasPhonetics));
            }
        }
    }

    /// <summary>
    /// Whether phonetics data is available.
    /// </summary>
    public bool HasPhonetics => !string.IsNullOrEmpty(_phoneticsDisplay);

    /// <summary>
    /// Whether streaming mode is active (bypasses MediaElement, uses NAudio).
    /// </summary>
    public bool IsStreamingMode
    {
        get => _isStreamingMode;
        private set => SetProperty(ref _isStreamingMode, value);
    }

    public TimeSpan TotalDuration
    {
        get => _totalDuration;
        set => SetProperty(ref _totalDuration, value);
    }

    public TimeSpan CurrentPosition
    {
        get => _currentPosition;
        set => SetProperty(ref _currentPosition, value);
    }

    public string LanguageName
    {
        get => _languageName;
        set => SetProperty(ref _languageName, value);
    }

    // Font settings from user preferences
    public double FontSize => _settingsService.Settings.FontSize * 2.0; // Large for pronunciation focus
    public string FontFamily => _settingsService.Settings.FontFamily;
    public double PhoneticsFontSize => _settingsService.Settings.FontSize * 1.5;

    #endregion

    #region Public Methods

    /// <summary>
    /// Prepares the window for loading state - shows immediately with spinner.
    /// Called before LoadPronunciationAsync to ensure window is visible during load.
    /// </summary>
    public void PrepareForLoading(string text)
    {
        _pronunciationGeneration++;
        ResetData();
        OriginalText = text?.Trim() ?? string.Empty;
        IsLoading = true;
        StatusMessage = string.Empty;
    }

    /// <summary>
    /// Loads pronunciation data for the given text.
    /// </summary>
    public async Task LoadPronunciationAsync(string text)
    {
        // Skip setup if PrepareForLoading was already called (detected via IsLoading)
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
            // Determine mode
            var words = OriginalText.Split(new[] { ' ', '\r', '\n', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            IsSingleWord = words.Length == 1;

            // Delegate business logic to service
            var result = await _pronunciationService.GetPronunciationAsync(OriginalText);

            if (!result.IsSuccess)
            {
                StatusMessage = result.Message;
                // Fallback for syllable display
                Syllables.Add(new SyllableItem { Text = OriginalText });
                await UpdateAudioUriAsync(); // Try getting audio anyway (e.g. if partial data)
                return;
            }

            var data = result.Data!;

            // Bind data
            _detectedLanguageCode = data.DetectedLanguageCode;
            LanguageName = GetLanguageName(_detectedLanguageCode);

            // Only show deep details for single words
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
                // Long text mode: Clear any phonetics/syllables that might have been returned
                PhoneticsDisplay = string.Empty;
                Syllables.Clear();
            }

            // Sync initial state
            IsSlowMode = false;

            // Audio URI might have been pre-fetched by the provider
            if (data.AudioUri != null)
            {
                AudioUri = data.AudioUri;
            }
            else
            {
                await UpdateAudioUriAsync();
            }

            System.Diagnostics.Debug.WriteLine($"Pronunciation loaded: {OriginalText}, IsSingleWord: {IsSingleWord}");
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

    private bool _isLoading;
    public bool IsLoading
    {
        get => _isLoading;
        set => SetProperty(ref _isLoading, value);
    }

    private string _statusMessage = string.Empty;
    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    private async Task UpdateAudioUriAsync()
    {
        if (string.IsNullOrEmpty(OriginalText)) return;

        IsLoading = true;
        StatusMessage = string.Empty;
        AudioUri = null; // Clear old audio while loading
        StopStreaming(); // Stop any active streaming

        try
        {
            // Capture generation to prevent race conditions
            int currentGen = _pronunciationGeneration;

            // Check if provider supports streaming
            if (_pronunciationService.SupportsStreaming)
            {
                // Use streaming mode
                IsStreamingMode = true;
                _streamingPlayer = new NAudioStreamingPlayer();
                _streamingCts = new CancellationTokenSource();

                // Subscribe to playback completed event
                _streamingPlayer.PlaybackCompleted += (s, e) =>
                {
                    // Only stop playing if we are done downloading all chunks
                    if (!_isDownloadingChunks)
                    {
                        IsPlaying = false;
                    }
                };

                // Smart Chunking: Split text into sentences/chunks to reduce Time To First Token
                var chunks = TextChunker.ChunkText(OriginalText);
                _isDownloadingChunks = true;
                IsPlaying = true; // Set playing to true immediately as we expect audio

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
                        // If a chunk fails, stop DOWNLOADING, but let PLAYER continue playing buffer.
                        StatusMessage = $"Streaming interrupted: {result.Message}";
                        break; // Exit loop, _isDownloadingChunks set to false, PlaybackCompleted handles the rest
                    }
                }

                _isDownloadingChunks = false;
            }
            else
            {
                // Use file-based mode (MediaElement)
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



    /// <summary>
    /// Stops any active streaming playback and cleans up resources.
    /// </summary>
    public void StopStreaming()
    {
        _isDownloadingChunks = false;
        _streamingCts?.Cancel();
        _streamingPlayer?.Stop();
        _streamingPlayer?.Dispose();
        _streamingPlayer = null;
        _streamingCts?.Dispose();
        _streamingCts = null;
    }

    /// <summary>
    /// Animates the syllables based on total audio duration.
    /// </summary>
    public async Task AnimateSyllablesAsync(TimeSpan totalDuration)
    {
        if (Syllables.Count == 0) return;

        // Reset all
        foreach (var s in Syllables) s.IsActive = false;

        double durationMs = totalDuration.TotalMilliseconds;
        // If slow mode (handled by MediaElement SpeedRatio), the *actual* wall-clock time is duration / ratio.
        // BUT MediaElement.NaturalDuration usually reports the *source* duration.
        // If we slow down playback, we must scale our wait times.
        // We'll assume the caller passes the EFFECTIVE duration (NaturalDuration / SpeedRatio).

        int syllableCount = Syllables.Count;
        int interval = (int)(durationMs / syllableCount);

        for (int i = 0; i < syllableCount; i++)
        {
            if (!_isPlaying) break; // Check cancellation

            // Deactivate previous
            if (i > 0) Syllables[i - 1].IsActive = false;

            // Activate current
            Syllables[i].IsActive = true;

            await Task.Delay(interval);
        }

        // Cleanup
        if (Syllables.Count > 0) Syllables[Syllables.Count - 1].IsActive = false;
    }

    /// <summary>
    /// Hides the pronunciation window.
    /// </summary>
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
