using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using QuickTranslate.Services;
using QuickTranslate.Services.Audio;
using QuickTranslate.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using QuickTranslate.Helpers;

namespace QuickTranslate.ViewModels;

/// <summary>
/// ViewModel for the popup translation window
/// </summary>
public partial class PopupViewModel : ObservableObject, IDisposable
{
    private readonly ITranslationService _translationService;
    private readonly ISettingsService _settingsService;
    private readonly IPronunciationService _pronunciationService;

    [ObservableProperty]
    private TranslationModel? _currentTranslation;

    partial void OnCurrentTranslationChanged(TranslationModel? value)
    {
        Header.CurrentTranslation = value;
    }

    [ObservableProperty]
    private bool _isVisible = false;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SwapLanguagesCommand))]
    private string _sourceLanguage = Constants.Defaults.TargetLanguage;

    [ObservableProperty]
    private string _targetLanguage = Constants.Defaults.TargetLanguage;

    public ObservableCollection<LanguageOption> AvailableLanguages { get; } = new();
    public ObservableCollection<LanguageOption> TargetLanguages { get; } = new();

    public PopupHeaderViewModel Header { get; }

    // Pronunciation State
    [ObservableProperty]
    private bool _isPronunciationLoading;


    /// <summary>
    /// Generation counter to track translation sessions.
    /// Incremented on each new translation to detect stale async callbacks.
    /// </summary>
    private int _translationGeneration = 0;

    /// <summary>
    /// Gets the current translation generation. Used to guard against race conditions.
    /// </summary>
    public int TranslationGeneration => _translationGeneration;

    private System.Threading.CancellationTokenSource? _translationCts;
    private bool _isHeuristicReTranslating;
    private string _lastDetectedSourceCode = string.Empty;
    private bool _hasCompletedFirstTranslation = false;
    private bool _isSwapping;

    private readonly IStreamingAudioPlayerFactory _playerFactory;
    private IStreamingAudioPlayer? _player;

    public PopupViewModel(
        ITranslationService translationService,
        ISettingsService settingsService,
        IPronunciationService pronunciationService,
        IClipboardService clipboardService,
        IStreamingAudioPlayerFactory streamingAudioPlayerFactory)
    {
        _translationService = translationService;
        _settingsService = settingsService;
        _pronunciationService = pronunciationService;

        _playerFactory = streamingAudioPlayerFactory;

        Header = new PopupHeaderViewModel(clipboardService);

        // Initialize from settings
        _sourceLanguage = _settingsService.Settings.DefaultSourceLanguage;
        _targetLanguage = _settingsService.Settings.DefaultTargetLanguage;

        LoadLanguages();
        _settingsService.SettingsChanged += OnSettingsChanged;

        InitializeProviders();
    }

    public bool AutoDetectSource => _settingsService.Settings.AutoDetectSource;
    public bool AutoDetectTargetLanguage => _settingsService.Settings.AutoDetectTargetLanguage;

    private void LoadLanguages()
    {
        AvailableLanguages.Clear();
        TargetLanguages.Clear();

        var supported = _translationService.GetSupportedLanguages();
        foreach (var lang in supported)
        {
            AvailableLanguages.Add(lang);
            if (lang.Code != "auto")
                TargetLanguages.Add(lang);
        }
    }

    partial void OnSourceLanguageChanged(string value)
    {
        if (_isHeuristicReTranslating || _isSwapping) return;

        bool isUserOverride = AutoDetectSource && value != "auto";
        if (!isUserOverride)
        {
            if (_settingsService.Settings.DefaultSourceLanguage != value)
            {
                _settingsService.Settings.DefaultSourceLanguage = value;
                _ = _settingsService.SaveAsync();
            }
            if (value != "auto")
            {
                TryAutoSelectTargetLanguage(value);
            }
        }
        if (CurrentTranslation != null && !string.IsNullOrEmpty(CurrentTranslation.OriginalText))
        {
            _ = TranslateAsync(CurrentTranslation.OriginalText, isReTranslation: true);
        }
    }

    partial void OnTargetLanguageChanged(string value)
    {
        if (_isHeuristicReTranslating || _isSwapping) return;

        if (_settingsService.Settings.DefaultTargetLanguage != value)
        {
            _settingsService.Settings.DefaultTargetLanguage = value;
            _ = _settingsService.SaveAsync();
        }
        if (CurrentTranslation != null && !string.IsNullOrEmpty(CurrentTranslation.OriginalText))
        {
            _ = TranslateAsync(CurrentTranslation.OriginalText, isReTranslation: true);
        }
    }

    [RelayCommand]
    private async Task PlayPronunciationAsync()
    {
        if (CurrentTranslation == null || !CurrentTranslation.IsSingleWord) return;

        // Prevent concurrent requests
        if (IsPronunciationLoading) return;

        IsPronunciationLoading = true;

        try
        {
            var text = CurrentTranslation.OriginalText.Trim();
            var langCode = CurrentTranslation.SourceLanguageCode;

            // Dispose previous player to avoid stale PCM data accumulation
            _player?.Stop();
            _player?.Dispose();
            _player = _playerFactory.CreatePlayer();

            _player.BeginStreaming();
            var result = await _pronunciationService.StreamAudioAsync(text, langCode, false, _player);
            _player.EndStreaming();

            if (!result.IsSuccess)
            {
                IsPronunciationLoading = false;
                return;
            }

            // Await playback completion. _player.IsPlaying becomes false when 
            // NAudio finishes playing and the buffer is empty (ReadFully=false).
            while (_player != null && _player.IsPlaying)
            {
                await Task.Delay(100);
            }
            IsPronunciationLoading = false;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Pronunciation Play Area Error: {ex.Message}");
            IsPronunciationLoading = false;
        }
    }

    private void OnSettingsChanged(object? sender, EventArgs e)
    {
        OnPropertyChanged(nameof(TranslationFontSize));
        OnPropertyChanged(nameof(TranslationFontFamily));
        OnPropertyChanged(nameof(TranslationFontWeight));
        OnPropertyChanged(nameof(ShowPronunciation));
        OnPropertyChanged(nameof(AutoDetectTargetLanguage));
    }

    private void InitializeProviders()
    {
        Providers.Clear();
        foreach (var provider in _translationService.GetProviderStates())
        {
            Providers.Add(provider);
        }
    }

    #region Computed Properties

    public double TranslationFontSize => _settingsService.Settings.FontSize;
    public string TranslationFontFamily => _settingsService.Settings.FontFamily;
    public string TranslationFontWeight => _settingsService.Settings.FontWeight;
    public bool ShowPronunciation => _settingsService.Settings.ShowPronunciation;

    public string CurrentProviderName => _translationService.ProviderName;
    public string[] AvailableProviders => _translationService.GetAvailableProviders();
    public ObservableCollection<ProviderInfo> Providers { get; } = new();

    #endregion

    #region Public Methods

    public async Task TranslateAsync(string sourceText, bool isReTranslation = false)
    {
        int genBefore = _translationGeneration;
        DebugLog.Write($"TranslateAsync ENTER: text='{sourceText}', isReTranslation={isReTranslation}, gen={genBefore}, ctsCanceled={_translationCts?.IsCancellationRequested}");

        try
        {
            _translationCts?.Cancel();
            _translationCts?.Dispose();
            _translationCts = new System.Threading.CancellationTokenSource();

            _translationGeneration++;

            if (!isReTranslation)
            {
                IsVisible = false;
                CurrentTranslation = null;
                _hasCompletedFirstTranslation = false;
            }

            DebugLog.Write($"TranslateAsync: gen now={_translationGeneration}, starting translation request");

            if (string.IsNullOrWhiteSpace(sourceText))
            {
                CurrentTranslation = await _translationService.TranslateAsync(sourceText, TargetLanguage, null, _translationCts.Token);
                DebugLog.Write($"TranslateAsync: empty text translation completed, CurrentTranslation={CurrentTranslation != null}");
                return;
            }

            string? sourceLang = SourceLanguage == "auto" ? null : SourceLanguage;
            bool wasAutoDetect = SourceLanguage == "auto";

            if (AutoDetectTargetLanguage)
            {
                sourceLang = null;
                wasAutoDetect = true;
            }

            CurrentTranslation = await _translationService.TranslateAsync(sourceText, TargetLanguage, sourceLang, _translationCts.Token);
            if (CurrentTranslation?.IsSuccess == true && CurrentTranslation.SourceLanguageCode != "auto")
            {
                var detectedSource = CurrentTranslation.SourceLanguageCode;
                _lastDetectedSourceCode = detectedSource;

                if (wasAutoDetect && AutoDetectTargetLanguage && !_hasCompletedFirstTranslation)
                {
                    var bestTarget = TryAutoSelectTargetLanguageByHeuristic(detectedSource);
                    if (bestTarget != null && bestTarget != TargetLanguage)
                    {
                        _isHeuristicReTranslating = true;
                        var reResult = await _translationService.TranslateAsync(sourceText, bestTarget, detectedSource, _translationCts.Token);
                        if (_translationCts.Token.IsCancellationRequested)
                        {
                            _isHeuristicReTranslating = false;
                            _hasCompletedFirstTranslation = true;
                            return;
                        }
                        if (reResult.IsSuccess)
                        {
                            CurrentTranslation = reResult;
                            RecordLanguagePair(detectedSource, bestTarget);
                        }
                        SourceLanguage = detectedSource;
                        TargetLanguage = bestTarget;
                        _isHeuristicReTranslating = false;
                        _hasCompletedFirstTranslation = true;
                        return;
                    }
                    RecordLanguagePair(detectedSource, TargetLanguage);
                    _hasCompletedFirstTranslation = true;
                    SourceLanguage = detectedSource;
                }
                else if (wasAutoDetect && _settingsService.Settings.AutoDetectSource)
                {
                    RecordLanguagePair(detectedSource, TargetLanguage);
                    var remembered = TryGetRememberedTarget(detectedSource);
                    if (remembered != null && remembered != TargetLanguage)
                    {
                        TargetLanguage = remembered;
                    }
                }
                _hasCompletedFirstTranslation = true;
            }
            DebugLog.Write($"TranslateAsync EXIT: gen={_translationGeneration}, CurrentTranslation={CurrentTranslation != null}");
        }
        catch (TaskCanceledException)
        {
            DebugLog.Write($"TranslateAsync: TaskCanceledException caught, gen={_translationGeneration}");
        }
        catch (Exception ex)
        {
            DebugLog.Write($"TranslateAsync: Exception '{ex.Message}', gen={_translationGeneration}");
            System.Diagnostics.Debug.WriteLine($"Translation Error: {ex.Message}");
        }
    }

    public async Task SetProviderAsync(string providerName)
    {
        if (_translationService.ProviderName == providerName) return;

        _translationService.SetProvider(providerName);

        // Sync local provider collection
        foreach (var p in Providers)
        {
            p.IsSelected = p.Name == providerName;
        }

        OnPropertyChanged(nameof(CurrentProviderName));
        OnPropertyChanged(nameof(Providers));

        if (CurrentTranslation != null && !string.IsNullOrEmpty(CurrentTranslation.OriginalText))
        {
            await TranslateAsync(CurrentTranslation.OriginalText, isReTranslation: true);
        }
    }

    [RelayCommand(CanExecute = nameof(CanSwapLanguages))]
    private async Task SwapLanguagesAsync()
    {
        var tempText = CurrentTranslation?.MainTranslation ?? string.Empty;

        _isSwapping = true;

        if (AutoDetectSource && SourceLanguage == "auto")
        {
            TargetLanguage = _lastDetectedSourceCode;
        }
        else
        {
            var swapSource = SourceLanguage;
            SourceLanguage = TargetLanguage;
            TargetLanguage = swapSource;
        }

        _isSwapping = false;

        if (!string.IsNullOrEmpty(tempText))
        {
            await TranslateAsync(tempText, isReTranslation: true);
        }
    }

    private bool CanSwapLanguages()
    {
        if (SourceLanguage != "auto") return true;
        return !string.IsNullOrEmpty(_lastDetectedSourceCode);
    }

    private void RecordLanguagePair(string sourceCode, string targetCode)
    {
        var pairs = _settingsService.Settings.RecentLanguagePairs;
        if (pairs.TryGetValue(sourceCode, out var existing) && existing == targetCode)
            return;
        pairs[sourceCode] = targetCode;
        _ = _settingsService.SaveAsync();
    }

    private void TryAutoSelectTargetLanguage(string sourceCode)
    {
        var manual = _settingsService.Settings.ManualLanguagePairs;
        if (manual.TryGetValue(sourceCode, out var manualTarget) && manualTarget != TargetLanguage)
        {
            TargetLanguage = manualTarget;
            return;
        }

        var auto = _settingsService.Settings.RecentLanguagePairs;
        if (auto.TryGetValue(sourceCode, out var autoTarget) && autoTarget != TargetLanguage)
        {
            TargetLanguage = autoTarget;
        }
    }

    private string? TryAutoSelectTargetLanguageByHeuristic(string sourceCode)
    {
        var manual = _settingsService.Settings.ManualLanguagePairs;
        var auto = _settingsService.Settings.RecentLanguagePairs;

        if (manual.TryGetValue(sourceCode, out var manualTarget) && manualTarget != TargetLanguage)
            return manualTarget;
        if (auto.TryGetValue(sourceCode, out var autoTarget) && autoTarget != TargetLanguage)
            return autoTarget;

        var systemLang = LanguageHelper.GetSystemLanguageCode();
        if (!string.IsNullOrEmpty(systemLang) && !sourceCode.Equals(systemLang, StringComparison.OrdinalIgnoreCase))
            return systemLang;

        var allPairs = GetAllLanguagePairs();
        if (allPairs.Count > 0)
        {
            var lastTarget = allPairs.Values.LastOrDefault(t => !t.Equals(sourceCode, StringComparison.OrdinalIgnoreCase));
            if (lastTarget != null)
                return lastTarget;
        }

        var fallback = _settingsService.Settings.DefaultTargetLanguage;
        if (!string.IsNullOrEmpty(fallback) && !fallback.Equals(sourceCode, StringComparison.OrdinalIgnoreCase))
            return fallback;

        return null;
    }

    private Dictionary<string, string> GetAllLanguagePairs()
    {
        var all = new Dictionary<string, string>(_settingsService.Settings.ManualLanguagePairs);
        foreach (var kvp in _settingsService.Settings.RecentLanguagePairs)
        {
            if (!all.ContainsKey(kvp.Key))
                all[kvp.Key] = kvp.Value;
        }
        return all;
    }

    private string? TryGetRememberedTarget(string sourceCode)
    {
        var manual = _settingsService.Settings.ManualLanguagePairs;
        if (manual.TryGetValue(sourceCode, out var manualTarget))
            return manualTarget;

        var auto = _settingsService.Settings.RecentLanguagePairs;
        if (auto.TryGetValue(sourceCode, out var autoTarget))
            return autoTarget;

        return null;
    }

    public void HideWindow()
    {
        int genBefore = _translationGeneration;
        bool ctsWasCanceled = _translationCts?.IsCancellationRequested ?? false;
        _translationCts?.Cancel();
        _translationGeneration++;
        IsVisible = false;
        CurrentTranslation = null;
        DebugLog.Write($"HideWindow: gen {genBefore} -> {_translationGeneration}, ctsWasCanceled={ctsWasCanceled}, ctsIsNull={_translationCts == null}");
    }

    #endregion

    #region IDisposable

    public void Dispose()
    {
        _translationCts?.Cancel();
        _translationCts?.Dispose();

        if (_translationService is IDisposable disposable)
        {
            disposable.Dispose();
        }

        _player?.Dispose();
    }

    #endregion
}
