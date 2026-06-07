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
    private string _dictionarySyllables = string.Empty;

    [ObservableProperty]
    private string _dictionaryPartOfSpeech = string.Empty;

    [ObservableProperty]
    private string _dictionaryDefinition = string.Empty;

    [ObservableProperty]
    private string _dictionaryExample = string.Empty;

    [ObservableProperty]
    private string _dictionarySearchText = string.Empty;

    [ObservableProperty]
    private bool _isDictionaryLoading;

    [ObservableProperty]
    private bool _hasDictionaryResult;

    [ObservableProperty]
    private bool _isSourceCopied;

    [ObservableProperty]
    private bool _isTargetCopied;

    [ObservableProperty]
    private bool _isBookmarked;

    public ObservableCollection<string> DictionarySynonyms { get; } = new();
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
        ISettingsService settingsService,
        IPronunciationService pronunciationService,
        IClipboardService clipboardService,
        IStreamingAudioPlayerFactory audioPlayerFactory)
    {
        _translationService = translationService;
        _settingsService = settingsService;
        _pronunciationService = pronunciationService;
        _clipboardService = clipboardService;
        _audioPlayerFactory = audioPlayerFactory;

        _sourceLanguage = "auto";
        _targetLanguage = _settingsService.Settings.DefaultTargetLanguage;

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
        PronunciationProviders.Add(PronunciationProviderInfo.Create(Constants.PronunciationProviders.Gemini));
        PronunciationProviders.Add(PronunciationProviderInfo.Create(Constants.PronunciationProviders.Gcp));
        PronunciationProviders.Add(PronunciationProviderInfo.Create(Constants.PronunciationProviders.ElevenLabs));

        SelectedTranslationProvider = _translationService.ProviderName;
        SelectedPronunciationProvider = _settingsService.Settings.PronunciationProvider;
    }

    private void OnSettingsChanged(object? sender, EventArgs e)
    {
        OnPropertyChanged(nameof(TranslationFontSize));
        OnPropertyChanged(nameof(TranslationFontFamily));
    }

    partial void OnSourceTextChanged(string value)
    {
        StartDebounceTranslate();
    }

    partial void OnSourceLanguageChanged(string value)
    {
        AutoDetectSource = value == "auto";
        StartDebounceTranslate();
    }

    partial void OnTargetLanguageChanged(string value)
    {
        StartDebounceTranslate();
    }

    partial void OnAutoTranslateEnabledChanged(bool value)
    {
        if (value && !string.IsNullOrWhiteSpace(SourceText))
            StartDebounceTranslate();
    }

    partial void OnAutoDetectSourceChanged(bool value)
    {
        if (value && SourceLanguage != "auto")
            SourceLanguage = "auto";
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
                    SourceLanguage = result.SourceLanguageCode;
                    AutoDetectSource = false;
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
        if (AutoDetectSource) return;

        var tempText = SourceText;
        var tempLang = SourceLanguage;

        SourceText = TranslatedText;
        SourceLanguage = TargetLanguage;
        TargetLanguage = tempLang;
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
    private void SearchDictionary(string word)
    {
        if (string.IsNullOrWhiteSpace(word)) return;

        IsDictionaryLoading = true;
        HasDictionaryResult = false;
        IsBottomPanelExpanded = true;
        ActiveBottomTab = "dictionary";
        DictionarySearchText = word;

        Task.Run(async () =>
        {
            await Task.Delay(300);

            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                var entry = GetLocalDictionaryEntry(word.Trim().ToLower());
                if (entry.HasValue)
                {
                    var e = entry.Value;
                    DictionaryWord = e.Word;
                    DictionaryPhonetic = e.Phonetic;
                    DictionarySyllables = e.Syllables;
                    DictionaryPartOfSpeech = e.PartOfSpeech;
                    DictionaryDefinition = e.Definition;
                    DictionaryExample = e.Example;
                    DictionarySynonyms.Clear();
                    foreach (var s in e.Synonyms)
                        DictionarySynonyms.Add(s);
                    HasDictionaryResult = true;
                }
                else
                {
                    DictionaryWord = word;
                    DictionaryPhonetic = $"/{word}/";
                    DictionarySyllables = string.Join(".", word.ToCharArray());
                    DictionaryPartOfSpeech = "unknown";
                    DictionaryDefinition = $"[Offline] Definition for '{word}'.";
                    DictionaryExample = $"The word '{word}' was looked up in local dictionary.";
                    DictionarySynonyms.Clear();
                    DictionarySynonyms.Add("example");
                    DictionarySynonyms.Add("demo");
                    HasDictionaryResult = true;
                }

                IsDictionaryLoading = false;
            });
        });
    }

    [RelayCommand]
    private void SetActiveBottomTab(string tab)
    {
        ActiveBottomTab = tab;
        IsBottomPanelExpanded = true;
    }

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

    private static (string Word, string Syllables, string Phonetic, string PartOfSpeech, string Definition, string Example, string[] Synonyms)? GetLocalDictionaryEntry(string word)
    {
        var entries = new Dictionary<string, (string, string, string, string, string, string, string[])>
        {
            ["lightweight"] = ("lightweight", "light.weight", "/ˈlaɪtweɪt/", "adjective",
                "Having thin construction or low weight; designed to be highly efficient and consume minimal resources.",
                "The new translator is lightweight, loading in under 50ms.",
                new[] { "compact", "portable", "nimble", "slimline" }),
            ["translator"] = ("translator", "trans.la.tor", "/trænsˈleɪtər/", "noun",
                "A program or person that converts text from one language into another.",
                "This WPF tool operates as an instant translator.",
                new[] { "interpreter", "converter", "linguist" }),
            ["compact"] = ("compact", "com.pact", "/kəmˈpækt/", "adjective",
                "Closely and neatly packed together; dense; small in size.",
                "The dual-panel design is very compact, leaving desktop space free.",
                new[] { "dense", "compressed", "concise", "small" }),
        };

        return entries.TryGetValue(word, out var e) ? e : null;
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
