using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows.Input;
using QuickTranslate.Services;
using QuickTranslate.Models;

namespace QuickTranslate.ViewModels;

/// <summary>
/// ViewModel for the popup translation window
/// </summary>
public class PopupViewModel : ViewModelBase, IDisposable
{
    private readonly ITranslationService _translationService;
    private readonly ISettingsService _settingsService;
    private readonly IPronunciationService _pronunciationService;

    private TranslationModel? _currentTranslation;
    private bool _isVisible = false;
    private string _targetLanguage = Constants.Defaults.TargetLanguage; // Default to Arabic

    public PopupHeaderViewModel Header { get; }

    // Pronunciation State
    private bool _isPronunciationLoading;
    private Uri? _pronunciationAudioUri;

    public bool IsPronunciationLoading
    {
        get => _isPronunciationLoading;
        set => SetProperty(ref _isPronunciationLoading, value);
    }

    public Uri? PronunciationAudioUri
    {
        get => _pronunciationAudioUri;
        set => SetProperty(ref _pronunciationAudioUri, value);
    }

    public ICommand PlayPronunciationCommand { get; }

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

    public PopupViewModel(
        ITranslationService translationService,
        ISettingsService settingsService,
        IPronunciationService pronunciationService,
        IClipboardService clipboardService)
    {
        _translationService = translationService;
        _settingsService = settingsService;
        _pronunciationService = pronunciationService;

        Header = new PopupHeaderViewModel(clipboardService);

        _settingsService.SettingsChanged += OnSettingsChanged;

        PlayPronunciationCommand = new RelayCommand(PlayPronunciationAsync);

        InitializeProviders();
    }

    private async void PlayPronunciationAsync()
    {
        if (CurrentTranslation == null || !CurrentTranslation.IsSingleWord) return;

        // Prevent concurrent requests
        if (IsPronunciationLoading) return;

        IsPronunciationLoading = true;
        PronunciationAudioUri = null;

        try
        {
            var text = CurrentTranslation.OriginalText.Trim();
            // Language code inference (simple fallback using helper if available, or just use what we have)
            // Ideally we pass the full language name and let service/provider handle mapping via Helper
            var langName = CurrentTranslation.SourceLanguage;
            var langCode = QuickTranslate.Helpers.LanguageHelper.MapToIso6391(langName);

            // Fetch audio URI from the configured provider (Gemini/Google) via Service
            var result = await _pronunciationService.GetAudioUriAsync(text, langCode, false);

            if (result.IsSuccess && result.Data != null)
            {
                PronunciationAudioUri = result.Data;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Pronunciation Play Area Error: {ex.Message}");
        }
        finally
        {
            IsPronunciationLoading = false;
        }
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
        OnPropertyChanged(nameof(ShowPronunciation));
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
        set 
        {
            SetProperty(ref _currentTranslation, value);
            Header.CurrentTranslation = value;
        }
    }

    public bool IsVisible
    {
        get => _isVisible;
        set => SetProperty(ref _isVisible, value);
    }

    public double TranslationFontSize => _settingsService.Settings.FontSize;

    public string TranslationFontFamily => _settingsService.Settings.FontFamily;

    public string TranslationFontWeight => _settingsService.Settings.FontWeight;

    /// <summary>
    /// Whether to show pronunciation section for single-word translations.
    /// </summary>
    public bool ShowPronunciation => _settingsService.Settings.ShowPronunciation;

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

    // Pronunciation properties for single-word translation
    public double PronunciationFontSize => TranslationFontSize * 1.5; // Larger for emphasis
    public double PhoneticsFontSize => TranslationFontSize * 0.85;

    #endregion

    #region Public Methods

    /// <summary>
    /// Main workflow: translates the provided source text
    /// </summary>
    public async Task TranslateAsync(string sourceText, bool isReTranslation = false)
    {
        try
        {
            // Cancel any ongoing translation
            _translationCts?.Cancel();
            _translationCts?.Dispose();
            _translationCts = new System.Threading.CancellationTokenSource();

            // Increment generation to invalidate any stale callbacks from previous translations
            _translationGeneration++;

            // Don't show window yet to avoid flicker
            if (!isReTranslation)
            {
                IsVisible = false;
                CurrentTranslation = null;
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
            CurrentTranslation = await _translationService.TranslateAsync(sourceText, _targetLanguage, null, _translationCts.Token);

            // Note: View will handle making window visible after layout update
        }
        catch (TaskCanceledException)
        {
            // Ignore cancellation exceptions transparently
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
        // Cancel in-flight requests immediately to free resources
        _translationCts?.Cancel();

        // Increment generation to invalidate any in-flight translation callbacks
        _translationGeneration++;
        IsVisible = false;
        CurrentTranslation = null;
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
    }

    #endregion
}
