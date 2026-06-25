using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using QuickTranslate.Models;
using QuickTranslate.Services;
using QuickTranslate.Services.Audio;

namespace QuickTranslate.ViewModels;

public partial class MainViewModel : ObservableObject, IDisposable
{
    private readonly ITranslationService _translationService;
    private readonly IDictionaryService _dictionaryService;
    private readonly ISettingsService _settingsService;
    private readonly IPronunciationService _pronunciationService;
    private readonly IClipboardService _clipboardService;
    private readonly IStreamingAudioPlayerFactory _audioPlayerFactory;

    [ObservableProperty]
    private bool _isVisible = true;

    [ObservableProperty]
    private bool _isMaximized;

    [ObservableProperty]
    private string _sourceText = string.Empty;

    [ObservableProperty]
    private string _translatedText = string.Empty;

    [ObservableProperty]
    private bool _isTranslating;

    [ObservableProperty]
    private string _sourceLanguage;

    [ObservableProperty]
    private string _targetLanguage;

    [ObservableProperty]
    private bool _autoDetectSource = true;

    [ObservableProperty]
    private bool _autoTranslateEnabled = true;

    [ObservableProperty]
    private string _selectedTranslationProvider = string.Empty;

    [ObservableProperty]
    private string _selectedPronunciationProvider = string.Empty;

    [ObservableProperty]
    private bool _isSettingsPopupOpen;

    [ObservableProperty]
    private bool _isBottomPanelExpanded;

    [ObservableProperty]
    private string _activeBottomTab = "dictionary";

    [ObservableProperty]
    private string _dictionaryWord = string.Empty;

    [ObservableProperty]
    private string _dictionaryPhonetic = string.Empty;

    [ObservableProperty]
    private string _dictionaryPartOfSpeech = string.Empty;

    [ObservableProperty]
    private string _dictionaryDefinition = string.Empty;

    [ObservableProperty]
    private string _dictionarySearchText = string.Empty;

    [ObservableProperty]
    private bool _isDictionaryLoading;

    [ObservableProperty]
    private bool _hasDictionaryResult;

    [ObservableProperty]
    private bool _isTranslationsExpanded = true;

    [ObservableProperty]
    private bool _isDefinitionsExpanded;

    [ObservableProperty]
    private bool _isExamplesExpanded;

    [ObservableProperty]
    private bool _isSourceCopied;

    [ObservableProperty]
    private bool _isTargetCopied;

    [ObservableProperty]
    private bool _isBookmarked;

    [ObservableProperty]
    private string _detectedSourceLanguage = string.Empty;

    private string _lastDetectedSourceCode = string.Empty;

    public ObservableCollection<string> DictionarySynonyms { get; } = new();
    public ObservableCollection<DictionaryEntry> DictionaryEntries { get; } = new();
    public ObservableCollection<DictionaryEntry> TranslationEntries { get; } = new();
    public ObservableCollection<DictionaryEntry> DefinitionEntries { get; } = new();
    public ObservableCollection<DictionaryEntry> ExampleEntries { get; } = new();
    public ObservableCollection<LanguageOption> Languages { get; } = new();
    public ObservableCollection<LanguageOption> TargetLanguages { get; } = new();
    public ObservableCollection<string> TranslationProviders { get; } = new();
    public ObservableCollection<PronunciationProviderInfo> PronunciationProviders { get; } = new();

    private CancellationTokenSource? _translationCts;
    private CancellationTokenSource? _debounceCts;
    private int _translationGeneration;
    private IStreamingAudioPlayer? _sourcePlayer;
    private IStreamingAudioPlayer? _targetPlayer;

    public double TranslationFontSize => _settingsService.Settings.FontSize;
    public string TranslationFontFamily => _settingsService.Settings.FontFamily;

    public MainViewModel(
        ITranslationService translationService,
        IDictionaryService dictionaryService,
        ISettingsService settingsService,
        IPronunciationService pronunciationService,
        IClipboardService clipboardService,
        IStreamingAudioPlayerFactory audioPlayerFactory)
    {
        _translationService = translationService;
        _dictionaryService = dictionaryService;
        _settingsService = settingsService;
        _pronunciationService = pronunciationService;
        _clipboardService = clipboardService;
        _audioPlayerFactory = audioPlayerFactory;

        _sourceLanguage = _settingsService.Settings.DefaultSourceLanguage;
        _targetLanguage = _settingsService.Settings.DefaultTargetLanguage;
        _autoDetectSource = _settingsService.Settings.AutoDetectSource;
        _autoTranslateEnabled = _settingsService.Settings.AutoTranslateEnabled;

        LoadLanguages();
        InitializeProviders();
        _settingsService.SettingsChanged += OnSettingsChanged;
    }

    private void LoadLanguages()
    {
        Languages.Clear();
        TargetLanguages.Clear();

        var supported = _translationService.GetSupportedLanguages();
        foreach (var lang in supported)
        {
            Languages.Add(lang);
            if (lang.Code != "auto")
                TargetLanguages.Add(lang);
        }
    }

    private void InitializeProviders()
    {
        foreach (var p in _translationService.GetAvailableProviders())
            TranslationProviders.Add(p);

        PronunciationProviders.Add(PronunciationProviderInfo.Create(Constants.PronunciationProviders.Google));
        PronunciationProviders.Add(PronunciationProviderInfo.Create(Constants.PronunciationProviders.ElevenLabs));
        PronunciationProviders.Add(PronunciationProviderInfo.Create(Constants.PronunciationProviders.Gemini));
        PronunciationProviders.Add(PronunciationProviderInfo.Create(Constants.PronunciationProviders.Gcp));

        SelectedTranslationProvider = _translationService.ProviderName;
        SelectedPronunciationProvider = _settingsService.Settings.PronunciationProvider;
    }

    private void OnSettingsChanged(object? sender, EventArgs e)
    {
        OnPropertyChanged(nameof(TranslationFontSize));
        OnPropertyChanged(nameof(TranslationFontFamily));

        if (SourceLanguage != _settingsService.Settings.DefaultSourceLanguage)
            SourceLanguage = _settingsService.Settings.DefaultSourceLanguage;
        if (TargetLanguage != _settingsService.Settings.DefaultTargetLanguage)
            TargetLanguage = _settingsService.Settings.DefaultTargetLanguage;
        if (AutoDetectSource != _settingsService.Settings.AutoDetectSource)
            AutoDetectSource = _settingsService.Settings.AutoDetectSource;
        if (AutoTranslateEnabled != _settingsService.Settings.AutoTranslateEnabled)
            AutoTranslateEnabled = _settingsService.Settings.AutoTranslateEnabled;
    }

    partial void OnSourceTextChanged(string value)
    {
        StartDebounceTranslate();
    }

    partial void OnSourceLanguageChanged(string value)
    {
        AutoDetectSource = value == "auto";
        if (_settingsService.Settings.DefaultSourceLanguage != value)
        {
            _settingsService.Settings.DefaultSourceLanguage = value;
            _ = _settingsService.SaveAsync();
        }
        if (value != "auto")
        {
            TryAutoSelectTargetLanguage(value);
        }
        StartDebounceTranslate();
    }

    partial void OnTargetLanguageChanged(string value)
    {
        if (_settingsService.Settings.DefaultTargetLanguage != value)
        {
            _settingsService.Settings.DefaultTargetLanguage = value;
            _ = _settingsService.SaveAsync();
        }
        StartDebounceTranslate();
    }

    partial void OnAutoTranslateEnabledChanged(bool value)
    {
        if (_settingsService.Settings.AutoTranslateEnabled != value)
        {
            _settingsService.Settings.AutoTranslateEnabled = value;
            _ = _settingsService.SaveAsync();
        }
        if (value && !string.IsNullOrWhiteSpace(SourceText))
            StartDebounceTranslate();
    }

    partial void OnAutoDetectSourceChanged(bool value)
    {
        if (_settingsService.Settings.AutoDetectSource != value)
        {
            _settingsService.Settings.AutoDetectSource = value;
            _ = _settingsService.SaveAsync();
        }
        if (value && SourceLanguage != "auto")
            SourceLanguage = "auto";
        if (!value)
            DetectedSourceLanguage = string.Empty;
    }

    partial void OnSelectedTranslationProviderChanged(string value)
    {
        if (string.IsNullOrEmpty(value)) return;
        if (_translationService.ProviderName == value) return;
        _translationService.SetProvider(value);
        _settingsService.Settings.DefaultProvider = value;
        _ = _settingsService.SaveAsync();
    }

    partial void OnSelectedPronunciationProviderChanged(string value)
    {
        if (string.IsNullOrEmpty(value)) return;
        if (_settingsService.Settings.PronunciationProvider == value) return;
        _settingsService.Settings.PronunciationProvider = value;
        _ = _settingsService.SaveAsync();
    }

    private void StartDebounceTranslate()
    {
        if (!AutoTranslateEnabled)
            return;

        _debounceCts?.Cancel();
        _debounceCts?.Dispose();
        _debounceCts = new CancellationTokenSource();

        var token = _debounceCts.Token;

        if (string.IsNullOrWhiteSpace(SourceText))
        {
            TranslatedText = string.Empty;
            return;
        }

        Task.Run(async () =>
        {
            try
            {
                await Task.Delay(800, token);
                if (!token.IsCancellationRequested)
                {
                    await Application.Current.Dispatcher.InvokeAsync(async () =>
                    {
                        await TranslateAsync();
                    });
                }
            }
            catch (TaskCanceledException)
            {
            }
        }, token);
    }

    [RelayCommand]
    private async Task TranslateAsync()
    {
        var text = SourceText;
        if (string.IsNullOrWhiteSpace(text))
        {
            TranslatedText = string.Empty;
            return;
        }

        IsTranslating = true;

        try
        {
            _translationCts?.Cancel();
            _translationCts?.Dispose();
            _translationCts = new CancellationTokenSource();
            _translationGeneration++;

            int myGeneration = _translationGeneration;
            var token = _translationCts.Token;

            string? sourceLang = AutoDetectSource ? null : (SourceLanguage == "auto" ? null : SourceLanguage);

            var result = await _translationService.TranslateAsync(text, TargetLanguage, sourceLang, token);

            if (myGeneration != _translationGeneration)
                return;

            if (result.IsSuccess)
            {
                TranslatedText = result.MainTranslation;
                if (AutoDetectSource && result.SourceLanguageCode != "auto")
                {
                    _lastDetectedSourceCode = result.SourceLanguageCode;
                    DetectedSourceLanguage = GetLanguageDisplayName(result.SourceLanguageCode);
                }
                if (result.SourceLanguageCode != "auto")
                {
                    RecordLanguagePair(result.SourceLanguageCode, TargetLanguage);
                }
            }
            else
            {
                TranslatedText = $"[Error] {result.ErrorMessage}";
            }
        }
        catch (TaskCanceledException)
        {
        }
        catch (Exception ex)
        {
            if (_translationGeneration > 0)
                TranslatedText = $"[Error] {ex.Message}";
        }
        finally
        {
            IsTranslating = false;
        }
    }

    [RelayCommand]
    private void SwapLanguages()
    {
        if (AutoDetectSource && string.IsNullOrEmpty(_lastDetectedSourceCode))
            return;

        var tempText = SourceText;
        var swapSource = AutoDetectSource ? _lastDetectedSourceCode : SourceLanguage;

        SourceText = TranslatedText;
        if (!AutoDetectSource)
        {
            SourceLanguage = TargetLanguage;
        }
        TargetLanguage = swapSource;
        TranslatedText = tempText;
    }

    [RelayCommand]
    private void ClearSourceText()
    {
        SourceText = string.Empty;
        TranslatedText = string.Empty;
    }

    [RelayCommand]
    private async Task CopySourceTextAsync()
    {
        if (string.IsNullOrWhiteSpace(SourceText)) return;
        _clipboardService.SetText(SourceText);
        IsSourceCopied = true;
        await Task.Delay(2000);
        IsSourceCopied = false;
    }

    [RelayCommand]
    private async Task CopyTranslatedTextAsync()
    {
        if (string.IsNullOrWhiteSpace(TranslatedText)) return;
        _clipboardService.SetText(TranslatedText);
        IsTargetCopied = true;
        await Task.Delay(2000);
        IsTargetCopied = false;
    }

    [RelayCommand]
    private async Task PlaySourcePronunciationAsync()
    {
        if (string.IsNullOrWhiteSpace(SourceText)) return;
        var langCode = SourceLanguage;
        if (langCode == "auto" || string.IsNullOrEmpty(langCode)) langCode = "en";
        _sourcePlayer = await PlayPronunciation(SourceText, langCode, _sourcePlayer);
    }

    [RelayCommand]
    private async Task PlayTargetPronunciationAsync()
    {
        if (string.IsNullOrWhiteSpace(TranslatedText)) return;
        _targetPlayer = await PlayPronunciation(TranslatedText, TargetLanguage, _targetPlayer);
    }

    private async Task<IStreamingAudioPlayer?> PlayPronunciation(string text, string languageCode, IStreamingAudioPlayer? player)
    {
        try
        {
            player?.Stop();
            player?.Dispose();
            player = _audioPlayerFactory.CreatePlayer();

            player.BeginStreaming();
            var result = await _pronunciationService.StreamAudioAsync(text, languageCode, false, player);
            player.EndStreaming();

            if (!result.IsSuccess) return player;

            while (player != null && player.IsPlaying)
            {
                await Task.Delay(100);
            }

            return player;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Pronunciation error: {ex.Message}");
            return player;
        }
    }

    [RelayCommand]
    private void ToggleBottomPanel()
    {
        IsBottomPanelExpanded = !IsBottomPanelExpanded;
    }

    [RelayCommand]
    private async Task SearchDictionary(string word)
    {
        if (string.IsNullOrWhiteSpace(word)) return;

        IsDictionaryLoading = true;
        HasDictionaryResult = false;
        IsBottomPanelExpanded = true;
        ActiveBottomTab = "dictionary";
        DictionarySearchText = word;

        try
        {
            var result = await _dictionaryService.LookupAsync(word.Trim(), TargetLanguage);

            DictionaryWord = result.OriginalText;
            DictionaryPhonetic = result.Phonetic;
            DictionaryPartOfSpeech = result.DictionaryEntries.Count > 0
                ? result.DictionaryEntries[0].PartOfSpeech
                : string.Empty;
            DictionaryDefinition = result.DictionaryEntries
                .FirstOrDefault(e => e.EntryType == DictionaryEntryType.Definition)
                ?.Definitions.FirstOrDefault()?.MainTerm ?? string.Empty;

            DictionarySynonyms.Clear();
            var translationEntry = result.DictionaryEntries
                .FirstOrDefault(e => e.EntryType == DictionaryEntryType.Translation);
            if (translationEntry != null)
            {
                foreach (var def in translationEntry.Definitions)
                    DictionarySynonyms.Add(def.MainTerm);
            }

            DictionaryEntries.Clear();
            TranslationEntries.Clear();
            DefinitionEntries.Clear();
            ExampleEntries.Clear();
            foreach (var entry in result.DictionaryEntries)
            {
                DictionaryEntries.Add(entry);
                switch (entry.EntryType)
                {
                    case DictionaryEntryType.Translation:
                        TranslationEntries.Add(entry);
                        break;
                    case DictionaryEntryType.Definition:
                        DefinitionEntries.Add(entry);
                        break;
                    case DictionaryEntryType.Example:
                        ExampleEntries.Add(entry);
                        break;
                }
            }

            HasDictionaryResult = true;
            IsTranslationsExpanded = true;
            IsDefinitionsExpanded = false;
            IsExamplesExpanded = false;
        }
        catch (Exception ex)
        {
            DictionaryWord = word;
            DictionaryPhonetic = string.Empty;
            DictionaryPartOfSpeech = string.Empty;
            DictionaryDefinition = $"Error: {ex.Message}";
            DictionarySynonyms.Clear();
            DictionaryEntries.Clear();
            TranslationEntries.Clear();
            DefinitionEntries.Clear();
            ExampleEntries.Clear();
            HasDictionaryResult = true;
        }
        finally
        {
            IsDictionaryLoading = false;
        }
    }

    [RelayCommand]
    private void SetActiveBottomTab(string tab)
    {
        ActiveBottomTab = tab;
        IsBottomPanelExpanded = true;
    }

    [RelayCommand]
    private void ToggleTranslations() => IsTranslationsExpanded = !IsTranslationsExpanded;

    [RelayCommand]
    private void ToggleDefinitions() => IsDefinitionsExpanded = !IsDefinitionsExpanded;

    [RelayCommand]
    private void ToggleExamples() => IsExamplesExpanded = !IsExamplesExpanded;

    [RelayCommand]
    private void ToggleBookmark()
    {
        IsBookmarked = !IsBookmarked;
    }

    [RelayCommand]
    private void ToggleSettingsPopup()
    {
        IsSettingsPopupOpen = !IsSettingsPopupOpen;
    }

    public void LookupWord(string word)
    {
        if (!string.IsNullOrWhiteSpace(word))
        {
            SearchDictionaryCommand.Execute(word);
        }
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

    private string GetLanguageDisplayName(string code)
    {
        return Languages.FirstOrDefault(l => l.Code == code)?.DisplayName ?? code;
    }

    public void Dispose()
    {
        _translationCts?.Cancel();
        _translationCts?.Dispose();
        _debounceCts?.Cancel();
        _debounceCts?.Dispose();
        _sourcePlayer?.Dispose();
        _targetPlayer?.Dispose();
        _settingsService.SettingsChanged -= OnSettingsChanged;
    }
}
