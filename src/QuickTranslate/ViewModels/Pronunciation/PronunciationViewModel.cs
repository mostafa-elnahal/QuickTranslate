using System;
using System.Threading;
using System.Threading.Tasks;
using QuickTranslate.Services;
using QuickTranslate.Services.Audio;
using QuickTranslate.Services.Pronunciation;
using QuickTranslate.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.Generic;
using System.Collections.ObjectModel;

using QuickTranslate.Services.Providers;

namespace QuickTranslate.ViewModels;

public partial class PronunciationViewModel : ObservableObject, IDisposable
{
    private readonly IPronunciationService _pronunciationService;
    private readonly ISettingsService _settingsService;
    private readonly ILanguageMetadataService _languageService;
    private readonly IAudioSyncService _syncService;
    private readonly IAudioStreamingService _streamingService;
    private readonly IStreamingAudioPlayerFactory _audioPlayerFactory;

    private IStreamingAudioPlayer? _streamingPlayer;
    public IStreamingAudioPlayer? StreamingPlayer => _streamingPlayer;

    private CancellationTokenSource? _streamingCts;
    private CancellationTokenSource? _wordAnimationCts;

    private List<(int StartIndex, int EndIndex)> _chunkWordRanges = new();

    /// <summary>
    /// Exact word-timing from GCP TTS mark timepoints. Null when using estimated timing.
    /// </summary>
    private IReadOnlyList<TimepointInfo>? _exactTimepoints;
    [ObservableProperty]
    private string _originalText = string.Empty;

    [ObservableProperty]
    private string _phoneticsDisplay = string.Empty;

    partial void OnPhoneticsDisplayChanged(string value) => OnPropertyChanged(nameof(HasPhonetics));

    private string _detectedLanguageCode = Constants.Defaults.TargetLanguage;



    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(PlayPauseCommand))]
    [NotifyCanExecuteChangedFor(nameof(RestartCommand))]
    private bool _isPlaying;

    [ObservableProperty]
    private bool _isVisible = false;



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
        await UpdateAudioUriAsync();
    }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RestartCommand))]
    private bool _isLoading;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    private int _pronunciationGeneration = 0;
    public int PronunciationGeneration => _pronunciationGeneration;

    public PronunciationViewModel(
        IPronunciationService pronunciationService,
        ISettingsService settingsService,
        ILanguageMetadataService languageService,
        IAudioSyncService syncService,
        IAudioStreamingService streamingService,
        IStreamingAudioPlayerFactory audioPlayerFactory)
    {
        _pronunciationService = pronunciationService;
        _settingsService = settingsService;
        _languageService = languageService;
        _syncService = syncService;
        _streamingService = streamingService;
        _audioPlayerFactory = audioPlayerFactory;

        _settingsService.SettingsChanged += OnSettingsChanged;
    }

    private void OnSettingsChanged(object? sender, EventArgs e)
    {
        OnPropertyChanged(nameof(BaseFontSize));
        OnPropertyChanged(nameof(FontFamily));
    }

    public ObservableCollection<SyllableItem> Syllables { get; } = new();
    public ObservableCollection<WordItem> Words { get; } = new();

    public bool HasPhonetics => !string.IsNullOrEmpty(PhoneticsDisplay);

    public double BaseFontSize => _settingsService.Settings.FontSize;
    public string FontFamily => _settingsService.Settings.FontFamily;

    public void Dispose()
    {
        _settingsService.SettingsChanged -= OnSettingsChanged;
        StopStreaming();
        GC.SuppressFinalize(this);
    }
}
