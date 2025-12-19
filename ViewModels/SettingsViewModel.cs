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
    private string _selectedCategory = "Basics";

    // Settings properties (bound to UI)
    private bool _startWithWindows;
    private double _windowOpacity;
    private string _defaultSourceLanguage = "auto";
    private string _defaultTargetLanguage = "en";
    private string _defaultProvider = "Google";
    private string _hotkey = "F1";

    public SettingsViewModel(ISettingsService settingsService)
    {
        _settingsService = settingsService;
        
        // Load current settings into properties
        LoadFromSettings();

        // Initialize commands
        SaveCommand = new RelayCommand(Save);
        CancelCommand = new RelayCommand(Cancel);

        // Initialize categories
        Categories = new ObservableCollection<string>
        {
            "Basics",
            "Hotkeys",
            "Languages",
            "Appearance",
            "About"
        };

        // Available languages for dropdowns
        AvailableLanguages = new ObservableCollection<LanguageOption>
        {
            new("Auto-detect", "auto"),
            new("English", "en"),
            new("Arabic", "ar"),
            new("Spanish", "es"),
            new("French", "fr"),
            new("German", "de"),
            new("Italian", "it"),
            new("Portuguese", "pt"),
            new("Russian", "ru"),
            new("Chinese", "zh"),
            new("Japanese", "ja"),
            new("Korean", "ko")
        };

        // Available providers
        AvailableProviders = new ObservableCollection<string>
        {
            "Google",
            "Bing",
            "Yandex"
        };
    }

    #region Properties

    public ObservableCollection<string> Categories { get; }
    public ObservableCollection<LanguageOption> AvailableLanguages { get; }
    public ObservableCollection<string> AvailableProviders { get; }

    public string SelectedCategory
    {
        get => _selectedCategory;
        set => SetProperty(ref _selectedCategory, value);
    }

    public bool StartWithWindows
    {
        get => _startWithWindows;
        set => SetProperty(ref _startWithWindows, value);
    }

    public double WindowOpacity
    {
        get => _windowOpacity;
        set => SetProperty(ref _windowOpacity, value);
    }

    public string DefaultSourceLanguage
    {
        get => _defaultSourceLanguage;
        set => SetProperty(ref _defaultSourceLanguage, value);
    }

    public string DefaultTargetLanguage
    {
        get => _defaultTargetLanguage;
        set => SetProperty(ref _defaultTargetLanguage, value);
    }

    public string DefaultProvider
    {
        get => _defaultProvider;
        set => SetProperty(ref _defaultProvider, value);
    }

    public string Hotkey
    {
        get => _hotkey;
        set => SetProperty(ref _hotkey, value);
    }

    #endregion

    #region Commands

    public ICommand SaveCommand { get; }
    public ICommand CancelCommand { get; }

    /// <summary>
    /// Event raised when the window should close.
    /// </summary>
    public event EventHandler<bool>? RequestClose;

    private void Save()
    {
        // Apply settings
        _settingsService.Settings.StartWithWindows = StartWithWindows;
        _settingsService.Settings.WindowOpacity = WindowOpacity;
        _settingsService.Settings.DefaultSourceLanguage = DefaultSourceLanguage;
        _settingsService.Settings.DefaultTargetLanguage = DefaultTargetLanguage;
        _settingsService.Settings.DefaultProvider = DefaultProvider;
        _settingsService.Settings.Hotkey = Hotkey;

        _settingsService.Save();
        RequestClose?.Invoke(this, true);
    }

    private void Cancel()
    {
        // Revert to saved settings
        LoadFromSettings();
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
    }
}

/// <summary>
/// Represents a language option for dropdowns.
/// </summary>
public record LanguageOption(string DisplayName, string Code);

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
