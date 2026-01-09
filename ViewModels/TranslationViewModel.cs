using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows;
using QuickTranslate.Services;
using QuickTranslate.Models;

namespace QuickTranslate.ViewModels;

/// <summary>
/// ViewModel for the popup translation window
/// </summary>
public class TranslationViewModel : ViewModelBase, IDisposable
{
    private readonly ITranslationService _translationService;
    private readonly ISettingsService _settingsService;

    private TranslationModel? _currentTranslation;
    private Visibility _windowVisibility = Visibility.Collapsed;
    private string _targetLanguage = "ar"; // Default to Arabic
    private bool _isPronunciationMode = false;

    /// <summary>
    /// Generation counter to track translation sessions.
    /// Incremented on each new translation to detect stale async callbacks.
    /// </summary>
    private int _translationGeneration = 0;

    /// <summary>
    /// Gets the current translation generation. Used to guard against race conditions.
    /// </summary>
    public int TranslationGeneration => _translationGeneration;

    public TranslationViewModel(ITranslationService translationService, ISettingsService settingsService)
    {
        _translationService = translationService;
        _settingsService = settingsService;

        _settingsService.SettingsChanged += OnSettingsChanged;

        InitializeProviders();
    }

    private void OnSettingsChanged(object? sender, EventArgs e)
    {
        OnPropertyChanged(nameof(TranslationFontSize));
        OnPropertyChanged(nameof(TranslationFontFamily));
        OnPropertyChanged(nameof(TranslationFontWeight));
        OnPropertyChanged(nameof(DictionaryTermFontSize));
        OnPropertyChanged(nameof(DictionarySmallFontSize));
        OnPropertyChanged(nameof(ChevronHeight));
        OnPropertyChanged(nameof(ChevronWidth));
        OnPropertyChanged(nameof(ChevronStrokeThickness));
    }

    private void InitializeProviders()
    {
        Providers.Clear();
        foreach (var name in _translationService.GetAvailableProviders())
        {
            var isSelected = name == _translationService.ProviderName;
            Providers.Add(ProviderInfo.Create(name, isSelected));
        }
    }

    #region Properties



    public TranslationModel? CurrentTranslation
    {
        get => _currentTranslation;
        set => SetProperty(ref _currentTranslation, value);
    }

    public Visibility WindowVisibility
    {
        get => _windowVisibility;
        set => SetProperty(ref _windowVisibility, value);
    }

    public bool IsPronunciationMode
    {
        get => _isPronunciationMode;
        set => SetProperty(ref _isPronunciationMode, value);
    }

    public double TranslationFontSize => _settingsService.Settings.FontSize;

    public string TranslationFontFamily => _settingsService.Settings.FontFamily;

    public string TranslationFontWeight => _settingsService.Settings.FontWeight;

    // Proportional font sizes for dictionary entries
    public double DictionaryTermFontSize => TranslationFontSize * 0.80; // ~12px when main is 18px
    public double DictionarySmallFontSize => TranslationFontSize * 0.75; // ~11px when main is 18px

    // Scaled dimensions for the chevron icon (proportional to small font)
    public double ChevronHeight => DictionarySmallFontSize * 0.75; // ~9px when small font is 12px
    public double ChevronWidth => ChevronHeight * 0.66; // Aspect ratio ~2:3
    public double ChevronStrokeThickness => Math.Max(1.0, DictionarySmallFontSize * 0.12); // ~1.5px base

    public string TargetLanguage
    {
        get => _targetLanguage;
        set => SetProperty(ref _targetLanguage, value);
    }

    public string CurrentProviderName => _translationService.ProviderName;

    public string[] AvailableProviders => _translationService.GetAvailableProviders();

    public ObservableCollection<ProviderInfo> Providers { get; } = new();

    #endregion

    #region Public Methods

    /// <summary>
    /// Main workflow: translates the provided source text
    /// </summary>

    public async Task TranslateAsync(string sourceText, bool isReTranslation = false, bool isPronunciation = false)
    {
        try
        {
            // Increment generation to invalidate any stale callbacks from previous translations
            _translationGeneration++;

            // Don't show window yet to avoid flicker
            if (!isReTranslation)
            {
                WindowVisibility = Visibility.Collapsed;
                CurrentTranslation = null;
                IsPronunciationMode = isPronunciation;
            }

            if (string.IsNullOrWhiteSpace(sourceText))
            {
                CurrentTranslation = new TranslationModel
                {
                    OriginalText = string.Empty,
                    MainTranslation = "[No text selected]",
                    ProviderName = _translationService.ProviderName
                };
                return;
            }

            // Perform real translation
            CurrentTranslation = await _translationService.TranslateAsync(sourceText, _targetLanguage);

            // Note: View will handle making window visible after layout update
        }
        catch (Exception ex)
        {
            CurrentTranslation = new TranslationModel
            {
                OriginalText = string.Empty,
                MainTranslation = $"[Error: {ex.Message}]",
                ProviderName = _translationService.ProviderName
            };
            System.Diagnostics.Debug.WriteLine($"Translation Error: {ex.Message}");
        }
    }

    /// <summary>
    /// Translates text but sets mode to Pronunciation.
    /// </summary>
    public async Task ShowPronounceAsync(string sourceText)
    {
        // Translate to force data fetching (audio/phonetic)
        // We reuse TranslateAsync with isPronunciation flag
        await TranslateAsync(sourceText, isPronunciation: true);
    }

    /// <summary>
    /// Changes the translation provider.
    /// </summary>
    public async Task SetProviderAsync(string providerName)
    {
        if (_translationService.ProviderName == providerName) return;

        _translationService.SetProvider(providerName);

        // Update selection state in Providers
        foreach (var p in Providers)
        {
            p.IsSelected = p.Name == providerName;
        }

        OnPropertyChanged(nameof(CurrentProviderName));
        OnPropertyChanged(nameof(Providers));

        // If we have a current translation, re-translate with the new provider
        if (CurrentTranslation != null && !string.IsNullOrEmpty(CurrentTranslation.OriginalText))
        {
            await TranslateAsync(CurrentTranslation.OriginalText, isReTranslation: true);
        }
    }

    /// <summary>
    /// Hides the translation window
    /// </summary>
    public void HideWindow()
    {
        // Increment generation to invalidate any in-flight translation callbacks
        _translationGeneration++;
        WindowVisibility = Visibility.Collapsed;
        CurrentTranslation = null;
    }

    #endregion

    #region IDisposable

    public void Dispose()
    {
        if (_translationService is IDisposable disposable)
        {
            disposable.Dispose();
        }
    }

    #endregion
}
