using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using QuickTranslate.Services;
using QuickTranslate.Services.Audio;
using QuickTranslate.Services.Pronunciation;
using QuickTranslate.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace QuickTranslate.ViewModels;

public partial class PronunciationViewModel : ObservableObject, IDisposable
{
    private readonly IPronunciationService _pronunciationService;
    private readonly ISettingsService _settingsService;
    private readonly ILanguageMetadataService _languageService;
    private readonly IAudioPlaybackService _playbackService;
    private readonly IWordHighlightService _highlightService;

    private CancellationTokenSource? _highlightCts;
    private int[] _wordToChunkIndex = Array.Empty<int>();
    private string _detectedLanguageCode = Constants.Defaults.TargetLanguage;
    private int _pronunciationGeneration;
    public int PronunciationGeneration => _pronunciationGeneration;

    [ObservableProperty]
    private string _originalText = string.Empty;

    [ObservableProperty]
    private string _phoneticsDisplay = string.Empty;

    partial void OnPhoneticsDisplayChanged(string value) => OnPropertyChanged(nameof(HasPhonetics));

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(PlayPauseCommand))]
    [NotifyCanExecuteChangedFor(nameof(RestartCommand))]
    private bool _isPlaying;

    [ObservableProperty]
    private bool _isVisible;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RestartCommand))]
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
        await LoadAudioAsync();
    }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RestartCommand))]
    private bool _isLoading;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    public ObservableCollection<SyllableItem> Syllables { get; } = new();
    public ObservableCollection<WordItem> Words { get; } = new();

    public bool HasPhonetics => !string.IsNullOrEmpty(PhoneticsDisplay);
    public double BaseFontSize => _settingsService.Settings.FontSize;
    public string FontFamily => _settingsService.Settings.FontFamily;

    public PronunciationViewModel(
        IPronunciationService pronunciationService,
        ISettingsService settingsService,
        ILanguageMetadataService languageService,
        IAudioPlaybackService playbackService,
        IWordHighlightService highlightService)
    {
        _pronunciationService = pronunciationService;
        _settingsService = settingsService;
        _languageService = languageService;
        _playbackService = playbackService;
        _highlightService = highlightService;

        _settingsService.SettingsChanged += OnSettingsChanged;
    }

    private void OnSettingsChanged(object? sender, EventArgs e)
    {
        OnPropertyChanged(nameof(BaseFontSize));
        OnPropertyChanged(nameof(FontFamily));
    }

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

            if (!IsSingleWord) PopulateWords(words);

            var result = await _pronunciationService.GetPronunciationAsync(OriginalText);
            if (!result.IsSuccess)
            {
                StatusMessage = result.Message;
                Syllables.Add(new SyllableItem { Text = OriginalText });
                await LoadAudioAsync();
                return;
            }

            var data = result.Data!;
            _detectedLanguageCode = data.DetectedLanguageCode;
            LanguageName = _languageService.GetLanguageName(_detectedLanguageCode);

            if (IsSingleWord)
            {
                if (!string.IsNullOrEmpty(data.Phonetics)) PhoneticsDisplay = $"/{data.Phonetics}/";
                foreach (var s in data.Syllables) Syllables.Add(s);
            }
            else
            {
                PhoneticsDisplay = string.Empty;
                Syllables.Clear();
            }

            IsSlowMode = false;
            OnPropertyChanged(nameof(IsSlowMode));

            await LoadAudioAsync();
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

    private async Task LoadAudioAsync()
    {
        if (string.IsNullOrEmpty(OriginalText)) return;

        IsLoading = true;
        StatusMessage = string.Empty;
        _playbackService.Stop();

        try
        {
            int currentGen = _pronunciationGeneration;

            int? firstChunkSize = _pronunciationService.TimingSupport == TimingSupportLevel.None
                ? _pronunciationService.MaxChunkSize : null;
            var (chunks, wordToChunkIndex) = TextChunker.ChunkText(
                OriginalText, _pronunciationService.MaxChunkSize, firstChunkSize);
            _wordToChunkIndex = wordToChunkIndex;

            IsDownloadingChunks = true;
            IsPlaying = true;

            var firstSampleTcs = new TaskCompletionSource<bool>();
            void OnFirstSample(object? s, EventArgs e)
            {
                firstSampleTcs.TrySetResult(true);
                _playbackService.SampleEnqueued -= OnFirstSample;
            }
            _playbackService.SampleEnqueued += OnFirstSample;

            _ = Task.Run(async () =>
            {
                try
                {
                    await firstSampleTcs.Task;

                    if (_pronunciationGeneration != currentGen) return;

                    var player = _playbackService.Player;
                    if (player == null) return;

                    var timingSupport = _pronunciationService.TimingSupport;

                    _highlightCts?.Cancel();
                    _highlightCts = new CancellationTokenSource();
                    var ct = _highlightCts.Token;

                    if (timingSupport == TimingSupportLevel.Exact && player.GetCombinedTimepoints() is { Count: > 0 })
                    {
                        _ = _highlightService.AnimateWordsAsync(
                            _playbackService, Words, ct);
                    }
                    else if (timingSupport == TimingSupportLevel.Estimated)
                    {
                        _ = AnimateChunksAsync(ct);
                    }
                }
                catch (OperationCanceledException) { }
            });

            _playbackService.PlaybackCompleted += OnPlaybackCompleted;

            _ = Task.Run(async () =>
            {
                try
                {
                    await _playbackService.StartAsync(
                        chunks.ToList(),
                        _detectedLanguageCode,
                        IsSlowMode);

                    if (_pronunciationGeneration != currentGen) return;
                }
                catch (Exception ex)
                {
                    if (_pronunciationGeneration == currentGen)
                    {
                        System.Windows.Application.Current.Dispatcher.Invoke(() =>
                            StatusMessage = "Audio streaming failed.");
                        System.Diagnostics.Debug.WriteLine($"Streaming background error: {ex.Message}");
                    }
                }
                finally
                {
                    if (_pronunciationGeneration == currentGen)
                    {
                        System.Windows.Application.Current.Dispatcher.Invoke(() =>
                        {
                            IsDownloadingChunks = false;
                            _playbackService.PlaybackCompleted -= OnPlaybackCompleted;
                        });
                    }
                }
            });
        }
        catch (Exception ex)
        {
            StatusMessage = "Audio error.";
            System.Diagnostics.Debug.WriteLine($"LoadAudio Error: {ex.Message}");
            _playbackService.Stop();
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task AnimateChunksAsync(CancellationToken ct, Task? startSignal = null)
    {
        if (startSignal != null)
            await startSignal;

        while (!ct.IsCancellationRequested)
        {
            var player = _playbackService.Player;
            if (player == null) break;

            var boundaries = player.GetChunkBoundaries();
            var position = player.CurrentPosition;

            int activeChunk = 0;
            for (int i = 0; i < boundaries.Count; i++)
            {
                if (position >= boundaries[i])
                    activeChunk = i + 1;
                else
                    break;
            }

            await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
            {
                for (int i = 0; i < Words.Count; i++)
                    Words[i].IsInActiveChunk = (_wordToChunkIndex[i] == activeChunk);
            }, System.Windows.Threading.DispatcherPriority.Background);

            await Task.Delay(100, ct);
        }
    }

    private void OnPlaybackCompleted(object? sender, EventArgs e)
    {
        System.Windows.Application.Current.Dispatcher.Invoke(() =>
        {
            IsPlaying = false;
            ClearWordHighlights();
            RestartCommand.NotifyCanExecuteChanged();
        });
    }

    [RelayCommand]
    private void PlayPause()
    {
        if (!_playbackService.IsPlaying && !_playbackService.IsPaused)
        {
            Restart();
            return;
        }

        if (_playbackService.IsPlaying)
        {
            _playbackService.Pause();
            IsPlaying = false;
        }
        else if (_playbackService.IsPaused)
        {
            _playbackService.Resume();
            IsPlaying = true;
        }
    }

    [RelayCommand(CanExecute = nameof(CanRestart))]
    private void Restart()
    {
        _playbackService.Restart();
        IsPlaying = true;

        _highlightCts?.Cancel();
        _highlightCts = new CancellationTokenSource();
        var ct = _highlightCts.Token;

        var player = _playbackService.Player;
        var timingSupport = _pronunciationService.TimingSupport;
        if (timingSupport == TimingSupportLevel.Exact && player?.GetCombinedTimepoints() is { Count: > 0 })
        {
            _ = _highlightService.AnimateWordsAsync(
                _playbackService, Words, ct);
        }
        else if (timingSupport == TimingSupportLevel.Estimated)
        {
            _ = AnimateChunksAsync(ct);
        }
    }

    private bool CanRestart => !IsDownloadingChunks;

    public void StopStreaming()
    {
        IsDownloadingChunks = false;
        ClearWordHighlights();
        _highlightCts?.Cancel();
        _playbackService.Stop();
    }

    public void Seek(TimeSpan position)
    {
        _playbackService.SetPosition(position);
        CurrentPosition = position;
    }

    public void SyncPlaybackPosition()
    {
        TotalDuration = _playbackService.TotalDuration;
        CurrentPosition = _playbackService.CurrentPosition;
    }

    private void ResetData()
    {
        OriginalText = string.Empty;
        Syllables.Clear();
        Words.Clear();
        _highlightCts?.Cancel();
        _wordToChunkIndex = Array.Empty<int>();
        PhoneticsDisplay = string.Empty;
        IsPlaying = false;
    }

    private void PopulateWords(string[] words)
    {
        Words.Clear();
        foreach (var word in words)
            Words.Add(new WordItem { Text = word });
    }

    public void HideWindow()
    {
        _pronunciationGeneration++;
        IsVisible = false;
        IsPlaying = false;
        StopStreaming();
        _pronunciationService.ClearProviderCache();
    }

    private void ClearWordHighlights()
    {
        foreach (var w in Words)
            w.IsActiveWord = false;
    }

    public void Dispose()
    {
        _settingsService.SettingsChanged -= OnSettingsChanged;
        StopStreaming();
        _playbackService.Dispose();
        GC.SuppressFinalize(this);
    }
}
