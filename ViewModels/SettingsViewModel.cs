using System;
using System.Collections.ObjectModel;
using System.Windows.Input;
using QuickTranslate.Models;
using QuickTranslate.Services;

namespace QuickTranslate.ViewModels;

/// <summary>
/// ViewModel for the Settings window.
/// </summary>
public class SettingsViewModel : ViewModelBase
{
    private readonly ISettingsService _settingsService;
    private readonly IDialogService _dialogService;
    private readonly ITranslationService _translationService;
    private string _selectedCategory = "Basics";

    // Settings properties (bound to UI)
    private bool _startWithWindows;
    private double _windowOpacity;
    private string _defaultSourceLanguage = "auto";
    private string _defaultTargetLanguage = "en";
    private string _defaultProvider = "Google";
    private string _hotkey = "Ctrl+Q";
    private string _pronunciationHotkey = "Ctrl+Shift+P";
    private double _fontSize = 18;
    private string _fontFamily = "Segoe UI";
    private string _fontWeight = "Medium";
    private bool _showPronunciation = true;
    private string _pronunciationProvider = "Google";
    private string _geminiApiKey = string.Empty;
    private bool _isDirty = false;

    private static readonly ObservableCollection<string> StaticCategories = new() { "Basics", "Hotkeys", "Languages", "Appearance", "Pronunciation", "About" };
    private static readonly ObservableCollection<string> StaticProviders = new() { "Google", "Bing", "Yandex" };
    private static readonly ObservableCollection<string> StaticFontFamilies = new() { "Segoe UI", "Calibri", "Arial", "Consolas", "Georgia" };
    private static readonly ObservableCollection<string> StaticFontWeights = new() { "Light", "Normal", "Medium", "SemiBold", "Bold" };
    private static readonly ObservableCollection<string> StaticPronunciationProviders = new() { Constants.PronunciationProviders.Google, Constants.PronunciationProviders.Gemini };

    public SettingsViewModel(ISettingsService settingsService, IDialogService dialogService, ITranslationService translationService)
    {
        _settingsService = settingsService;
        _dialogService = dialogService;
        _translationService = translationService;

        // Load current settings into properties
        LoadFromSettings();

        // Initialize commands
        SaveCommand = new RelayCommand(Save, () => IsDirty);
        CloseCommand = new RelayCommand(Close);

        // Initialized from static properties to save allocations
        Categories = StaticCategories;
        AvailableProviders = StaticProviders;
        AvailablePronunciationProviders = StaticPronunciationProviders;
        AvailableFontFamilies = StaticFontFamilies;
        AvailableFontWeights = StaticFontWeights;

        // Available languages dynamically loaded from provider
        AvailableLanguages = new ObservableCollection<LanguageOption>(_translationService.GetSupportedLanguages());
    }

    #region Properties

    public ObservableCollection<string> Categories { get; }
    public ObservableCollection<LanguageOption> AvailableLanguages { get; }
    public ObservableCollection<string> AvailableProviders { get; }
    public ObservableCollection<string> AvailableFontFamilies { get; }
    public ObservableCollection<string> AvailableFontWeights { get; }
    public ObservableCollection<string> AvailablePronunciationProviders { get; }

    public string SelectedCategory
    {
        get => _selectedCategory;
        set => SetProperty(ref _selectedCategory, value);
    }

    public bool StartWithWindows
    {
        get => _startWithWindows;
        set { if (SetProperty(ref _startWithWindows, value)) IsDirty = true; }
    }

    public double WindowOpacity
    {
        get => _windowOpacity;
        set { if (SetProperty(ref _windowOpacity, value)) IsDirty = true; }
    }

    public string DefaultSourceLanguage
    {
        get => _defaultSourceLanguage;
        set { if (SetProperty(ref _defaultSourceLanguage, value)) IsDirty = true; }
    }

    public string DefaultTargetLanguage
    {
        get => _defaultTargetLanguage;
        set { if (SetProperty(ref _defaultTargetLanguage, value)) IsDirty = true; }
    }

    public string DefaultProvider
    {
        get => _defaultProvider;
        set { if (SetProperty(ref _defaultProvider, value)) IsDirty = true; }
    }

    public string Hotkey
    {
        get => _hotkey;
        set { if (SetProperty(ref _hotkey, value)) IsDirty = true; }
    }

    public string PronunciationHotkey
    {
        get => _pronunciationHotkey;
        set { if (SetProperty(ref _pronunciationHotkey, value)) IsDirty = true; }
    }

    public double FontSize
    {
        get => _fontSize;
        set { if (SetProperty(ref _fontSize, value)) IsDirty = true; }
    }

    public string FontFamily
    {
        get => _fontFamily;
        set { if (SetProperty(ref _fontFamily, value)) IsDirty = true; }
    }

    public string FontWeight
    {
        get => _fontWeight;
        set { if (SetProperty(ref _fontWeight, value)) IsDirty = true; }
    }

    public bool ShowPronunciation
    {
        get => _showPronunciation;
        set { if (SetProperty(ref _showPronunciation, value)) IsDirty = true; }
    }

    public string PronunciationProvider
    {
        get => _pronunciationProvider;
        set
        {
            if (SetProperty(ref _pronunciationProvider, value))
            {
                IsDirty = true;
                OnPropertyChanged(nameof(IsApiKeyInputEnabled));
            }
        }
    }

    public string GeminiApiKey
    {
        get => _geminiApiKey;
        set { if (SetProperty(ref _geminiApiKey, value)) IsDirty = true; }
    }

    public bool IsApiKeyInputEnabled => PronunciationProvider == "Gemini";

    public bool IsDirty
    {
        get => _isDirty;
        private set => SetProperty(ref _isDirty, value);
    }

    #endregion

    #region Commands

    public ICommand SaveCommand { get; }
    public ICommand CloseCommand { get; }

    /// <summary>
    /// Event raised when the window should close.
    /// </summary>
    public event EventHandler<bool>? RequestClose;

    private void Save()
    {
        // Validation
        if (PronunciationProvider == "Gemini" && string.IsNullOrWhiteSpace(GeminiApiKey))
        {
            _dialogService.ShowWarning(
                "Gemini API Key is required when Gemini is selected as the pronunciation provider.",
                "Missing API Key");
            return;
        }

        // Apply settings
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

        _settingsService.Save();
        IsDirty = false;
    }

    private void Close()
    {
        // Close without saving - discard any unsaved changes
        RequestClose?.Invoke(this, false);
    }

    #endregion

    private void LoadFromSettings()
    {
        var settings = _settingsService.Settings;
        _startWithWindows = settings.StartWithWindows;
        _windowOpacity = settings.WindowOpacity;
        _defaultSourceLanguage = settings.DefaultSourceLanguage;
        _defaultTargetLanguage = settings.DefaultTargetLanguage;
        _defaultProvider = settings.DefaultProvider;
        _hotkey = settings.Hotkey;
        _pronunciationHotkey = settings.PronunciationHotkey;
        _fontSize = settings.FontSize;
        _fontFamily = settings.FontFamily;
        _fontWeight = settings.FontWeight;
        _showPronunciation = settings.ShowPronunciation;
        _pronunciationProvider = settings.PronunciationProvider;
        _geminiApiKey = settings.GeminiApiKey;
    }
}

/// <summary>
/// Simple relay command implementation.
/// </summary>
public class RelayCommand : ICommand
{
    private readonly Action _execute;
    private readonly Func<bool>? _canExecute;

    public RelayCommand(Action execute, Func<bool>? canExecute = null)
    {
        _execute = execute;
        _canExecute = canExecute;
    }

    public event EventHandler? CanExecuteChanged
    {
        add => CommandManager.RequerySuggested += value;
        remove => CommandManager.RequerySuggested -= value;
    }

    public bool CanExecute(object? parameter) => _canExecute?.Invoke() ?? true;
    public void Execute(object? parameter) => _execute();
}
