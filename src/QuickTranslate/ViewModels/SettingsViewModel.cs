using System;
using System.Threading.Tasks;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows.Input;
using QuickTranslate.Models;
using QuickTranslate.Services;
using QuickTranslate.Services.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace QuickTranslate.ViewModels;

/// <summary>
/// ViewModel for the Settings window.
/// </summary>
public partial class SettingsViewModel : ObservableObject
{
    private readonly ISettingsService _settingsService;
    private readonly IDialogService _dialogService;
    private readonly ITranslationService _translationService;
    private readonly IOcrService _ocrService;

    [ObservableProperty]
    private string _selectedCategory = "Basics";

    [ObservableProperty]
    private bool _startWithWindows;

    [ObservableProperty]
    private double _windowOpacity;

    [ObservableProperty]
    private string _defaultSourceLanguage = "auto";

    [ObservableProperty]
    private string _defaultTargetLanguage = Constants.Defaults.TargetLanguage;

    [ObservableProperty]
    private string _defaultProvider = Constants.TranslationProviders.Google;

    [ObservableProperty]
    private string _hotkey = "Ctrl+Q";

    [ObservableProperty]
    private string _pronunciationHotkey = "Ctrl+Shift+P";

    [ObservableProperty]
    private string _ocrHotkey = Constants.Defaults.OcrHotkey;

    [ObservableProperty]
    private OcrLanguage? _selectedOcrLanguage;

    [ObservableProperty]
    private double _fontSize = 18;

    [ObservableProperty]
    private string _fontFamily = "Segoe UI";

    [ObservableProperty]
    private string _fontWeight = "Medium";

    [ObservableProperty]
    private bool _showPronunciation = true;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsGeminiApiKeyInputEnabled))]
    [NotifyPropertyChangedFor(nameof(IsGcpApiKeyInputEnabled))]
    [NotifyPropertyChangedFor(nameof(IsElevenLabsApiKeyInputEnabled))]
    [NotifyPropertyChangedFor(nameof(IsElevenLabsVoiceIdInputEnabled))]
    private string _pronunciationProvider = Constants.PronunciationProviders.Google;

    [ObservableProperty]
    private string _geminiApiKey = string.Empty;

    [ObservableProperty]
    private string _gcpApiKey = string.Empty;

    [ObservableProperty]
    private string _elevenLabsApiKey = string.Empty;

    [ObservableProperty]
    private string _elevenLabsVoiceId = "21m00Tcm4TlvDq8ikWAM";

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    private bool _isDirty = false;

    private static readonly ObservableCollection<string> StaticCategories = new() { "Basics", "Hotkeys", "Languages", "Appearance", "Pronunciation", "OCR", "About" };
    private static readonly ObservableCollection<string> StaticProviders = new() { Constants.TranslationProviders.Google, Constants.TranslationProviders.Bing, Constants.TranslationProviders.Yandex };
    private static readonly ObservableCollection<string> StaticFontFamilies = new() { "Segoe UI", "Calibri", "Arial", "Consolas", "Georgia" };
    private static readonly ObservableCollection<string> StaticFontWeights = new() { "Light", "Normal", "Medium", "SemiBold", "Bold" };
    private static readonly ObservableCollection<PronunciationProviderInfo> StaticPronunciationProviders = new()
    {
        PronunciationProviderInfo.Create(Constants.PronunciationProviders.Google),
        PronunciationProviderInfo.Create(Constants.PronunciationProviders.Gemini),
        PronunciationProviderInfo.Create(Constants.PronunciationProviders.Gcp),
        PronunciationProviderInfo.Create(Constants.PronunciationProviders.ElevenLabs)
    };

    public SettingsViewModel(ISettingsService settingsService, IDialogService dialogService, ITranslationService translationService, IOcrService ocrService)
    {
        _settingsService = settingsService;
        _dialogService = dialogService;
        _translationService = translationService;
        _ocrService = ocrService;

        Categories = StaticCategories;
        AvailableProviders = StaticProviders;
        AvailablePronunciationProviders = StaticPronunciationProviders;
        AvailableFontFamilies = StaticFontFamilies;
        AvailableFontWeights = StaticFontWeights;
        AvailableLanguages = new ObservableCollection<LanguageOption>(_translationService.GetSupportedLanguages());
        AvailableOcrLanguages = new ObservableCollection<OcrLanguage>(_ocrService.GetAvailableLanguages());
        
        LoadFromSettings();
    }

    #region Properties

    public ObservableCollection<string> Categories { get; }
    public ObservableCollection<LanguageOption> AvailableLanguages { get; }
    public ObservableCollection<OcrLanguage> AvailableOcrLanguages { get; }
    public ObservableCollection<string> AvailableProviders { get; }
    public ObservableCollection<string> AvailableFontFamilies { get; }
    public ObservableCollection<string> AvailableFontWeights { get; }
    public ObservableCollection<PronunciationProviderInfo> AvailablePronunciationProviders { get; }

    public bool IsGeminiApiKeyInputEnabled => PronunciationProvider == Constants.PronunciationProviders.Gemini;
    public bool IsGcpApiKeyInputEnabled => PronunciationProvider == Constants.PronunciationProviders.Gcp;
    public bool IsElevenLabsApiKeyInputEnabled => PronunciationProvider == Constants.PronunciationProviders.ElevenLabs;
    public bool IsElevenLabsVoiceIdInputEnabled => PronunciationProvider == Constants.PronunciationProviders.ElevenLabs;

    #endregion

    #region Property Change Handlers

    protected override void OnPropertyChanged(PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);

        // Mark dirty for any setting property change (except IsDirty itself)
        if (e.PropertyName != nameof(IsDirty) && 
            e.PropertyName != nameof(SelectedCategory) && 
            e.PropertyName != nameof(IsGeminiApiKeyInputEnabled) &&
            e.PropertyName != nameof(IsGcpApiKeyInputEnabled) &&
            e.PropertyName != nameof(IsElevenLabsApiKeyInputEnabled) &&
            e.PropertyName != nameof(IsElevenLabsVoiceIdInputEnabled))
        {
            IsDirty = true;
        }
    }

    #endregion

    #region Commands

    public event EventHandler<bool>? RequestClose;

    [RelayCommand(CanExecute = nameof(IsDirty))]
    private async Task Save()
    {
        if (PronunciationProvider == Constants.PronunciationProviders.Gemini && string.IsNullOrWhiteSpace(GeminiApiKey))
        {
            _dialogService.ShowWarning(
                "Gemini API Key is required when Gemini is selected as the pronunciation provider.",
                "Missing API Key");
            return;
        }

        if (PronunciationProvider == Constants.PronunciationProviders.Gcp && string.IsNullOrWhiteSpace(GcpApiKey))
        {
            _dialogService.ShowWarning(
                "GCP API Key is required when GCP is selected as the pronunciation provider.",
                "Missing API Key");
            return;
        }

        if (PronunciationProvider == Constants.PronunciationProviders.ElevenLabs && string.IsNullOrWhiteSpace(ElevenLabsApiKey))
        {
            _dialogService.ShowWarning(
                "ElevenLabs API Key is required when ElevenLabs is selected as the pronunciation provider.",
                "Missing API Key");
            return;
        }

        _settingsService.Settings.StartWithWindows = StartWithWindows;
        _settingsService.Settings.WindowOpacity = WindowOpacity;
        _settingsService.Settings.DefaultSourceLanguage = DefaultSourceLanguage;
        _settingsService.Settings.DefaultTargetLanguage = DefaultTargetLanguage;
        _settingsService.Settings.DefaultProvider = DefaultProvider;
        _settingsService.Settings.Hotkey = Hotkey;
        _settingsService.Settings.PronunciationHotkey = PronunciationHotkey;
        _settingsService.Settings.OcrHotkey = OcrHotkey;
        _settingsService.Settings.OcrLanguage = SelectedOcrLanguage?.Code ?? Constants.Defaults.OcrLanguage;
        _settingsService.Settings.FontSize = FontSize;
        _settingsService.Settings.FontFamily = FontFamily;
        _settingsService.Settings.FontWeight = FontWeight;
        _settingsService.Settings.ShowPronunciation = ShowPronunciation;
        _settingsService.Settings.PronunciationProvider = PronunciationProvider;
        _settingsService.Settings.GeminiApiKey = GeminiApiKey;
        _settingsService.Settings.GcpApiKey = GcpApiKey;
        _settingsService.Settings.ElevenLabsApiKey = ElevenLabsApiKey;
        _settingsService.Settings.ElevenLabsVoiceId = ElevenLabsVoiceId;

        await _settingsService.SaveAsync();
        IsDirty = false;
    }

    [RelayCommand]
    private void Close()
    {
        RequestClose?.Invoke(this, false);
    }

    #endregion

    private void LoadFromSettings()
    {
        var settings = _settingsService.Settings;
        StartWithWindows = settings.StartWithWindows;
        WindowOpacity = settings.WindowOpacity;
        DefaultSourceLanguage = settings.DefaultSourceLanguage;
        DefaultTargetLanguage = settings.DefaultTargetLanguage;
        DefaultProvider = settings.DefaultProvider;
        Hotkey = settings.Hotkey;
        PronunciationHotkey = settings.PronunciationHotkey;
        OcrHotkey = settings.OcrHotkey;
        SelectedOcrLanguage = AvailableOcrLanguages.FirstOrDefault(l => l.Code == settings.OcrLanguage);
        FontSize = settings.FontSize;
        FontFamily = settings.FontFamily;
        FontWeight = settings.FontWeight;
        ShowPronunciation = settings.ShowPronunciation;
        PronunciationProvider = settings.PronunciationProvider;
        GeminiApiKey = settings.GeminiApiKey;
        GcpApiKey = settings.GcpApiKey;
        ElevenLabsApiKey = settings.ElevenLabsApiKey;
        ElevenLabsVoiceId = string.IsNullOrEmpty(settings.ElevenLabsVoiceId) ? "21m00Tcm4TlvDq8ikWAM" : settings.ElevenLabsVoiceId;
        IsDirty = false;
    }
}
