using System;
using System.Threading.Tasks;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Input;
using QuickTranslate.Models;
using QuickTranslate.Services;
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
    private double _fontSize = 18;

    [ObservableProperty]
    private string _fontFamily = "Segoe UI";

    [ObservableProperty]
    private string _fontWeight = "Medium";

    [ObservableProperty]
    private bool _showPronunciation = true;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsApiKeyInputEnabled))]
    private string _pronunciationProvider = Constants.PronunciationProviders.Google;

    [ObservableProperty]
    private string _geminiApiKey = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    private bool _isDirty = false;

    private static readonly ObservableCollection<string> StaticCategories = new() { "Basics", "Hotkeys", "Languages", "Appearance", "Pronunciation", "About" };
    private static readonly ObservableCollection<string> StaticProviders = new() { Constants.TranslationProviders.Google, Constants.TranslationProviders.Bing, Constants.TranslationProviders.Yandex };
    private static readonly ObservableCollection<string> StaticFontFamilies = new() { "Segoe UI", "Calibri", "Arial", "Consolas", "Georgia" };
    private static readonly ObservableCollection<string> StaticFontWeights = new() { "Light", "Normal", "Medium", "SemiBold", "Bold" };
    private static readonly ObservableCollection<string> StaticPronunciationProviders = new() { Constants.PronunciationProviders.Google, Constants.PronunciationProviders.Gemini };

    public SettingsViewModel(ISettingsService settingsService, IDialogService dialogService, ITranslationService translationService)
    {
        _settingsService = settingsService;
        _dialogService = dialogService;
        _translationService = translationService;

        LoadFromSettings();

        Categories = StaticCategories;
        AvailableProviders = StaticProviders;
        AvailablePronunciationProviders = StaticPronunciationProviders;
        AvailableFontFamilies = StaticFontFamilies;
        AvailableFontWeights = StaticFontWeights;
        AvailableLanguages = new ObservableCollection<LanguageOption>(_translationService.GetSupportedLanguages());
    }

    #region Properties

    public ObservableCollection<string> Categories { get; }
    public ObservableCollection<LanguageOption> AvailableLanguages { get; }
    public ObservableCollection<string> AvailableProviders { get; }
    public ObservableCollection<string> AvailableFontFamilies { get; }
    public ObservableCollection<string> AvailableFontWeights { get; }
    public ObservableCollection<string> AvailablePronunciationProviders { get; }

    public bool IsApiKeyInputEnabled => PronunciationProvider == Constants.PronunciationProviders.Gemini;

    #endregion

    #region Property Change Handlers

    protected override void OnPropertyChanged(PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);

        // Mark dirty for any setting property change (except IsDirty itself)
        if (e.PropertyName != nameof(IsDirty) && 
            e.PropertyName != nameof(SelectedCategory) && 
            e.PropertyName != nameof(IsApiKeyInputEnabled))
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

        _settingsService.Settings.StartWithWindows = StartWithWindows;
        _settingsService.Settings.WindowOpacity = WindowOpacity;
        _settingsService.Settings.DefaultSourceLanguage = DefaultSourceLanguage;
        _settingsService.Settings.DefaultTargetLanguage = DefaultTargetLanguage;
        _settingsService.Settings.DefaultProvider = DefaultProvider;
        _settingsService.Settings.Hotkey = Hotkey;
        _settingsService.Settings.PronunciationHotkey = PronunciationHotkey;
        _settingsService.Settings.FontSize = FontSize;
        _settingsService.Settings.FontFamily = FontFamily;
        _settingsService.Settings.FontWeight = FontWeight;
        _settingsService.Settings.ShowPronunciation = ShowPronunciation;
        _settingsService.Settings.PronunciationProvider = PronunciationProvider;
        _settingsService.Settings.GeminiApiKey = GeminiApiKey;

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
        FontSize = settings.FontSize;
        FontFamily = settings.FontFamily;
        FontWeight = settings.FontWeight;
        ShowPronunciation = settings.ShowPronunciation;
        PronunciationProvider = settings.PronunciationProvider;
        GeminiApiKey = settings.GeminiApiKey;
        IsDirty = false;
    }
}
