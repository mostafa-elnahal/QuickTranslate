using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows.Input;
using QuickTranslate.Services;
using QuickTranslate.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

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
    private string _targetLanguage = Constants.Defaults.TargetLanguage;

    public PopupHeaderViewModel Header { get; }

    // Pronunciation State
    [ObservableProperty]
    private bool _isPronunciationLoading;

    [ObservableProperty]
    private Uri? _pronunciationAudioUri;

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

        // Initialize from settings
        _targetLanguage = _settingsService.Settings.DefaultTargetLanguage;

        _settingsService.SettingsChanged += OnSettingsChanged;

        InitializeProviders();
    }

    [RelayCommand]
    private async Task PlayPronunciationAsync()
    {
        if (CurrentTranslation == null || !CurrentTranslation.IsSingleWord) return;

        // Prevent concurrent requests
        if (IsPronunciationLoading) return;

        IsPronunciationLoading = true;
        PronunciationAudioUri = null;

        try
        {
            var text = CurrentTranslation.OriginalText.Trim();
            var langCode = CurrentTranslation.SourceLanguageCode;

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
        // Update languages if they haven't been manually overridden for this session
        // (For now, we just sync with the new defaults)
        TargetLanguage = _settingsService.Settings.DefaultTargetLanguage;

        OnPropertyChanged(nameof(TranslationFontSize));
        OnPropertyChanged(nameof(TranslationFontFamily));
        OnPropertyChanged(nameof(TranslationFontWeight));
        OnPropertyChanged(nameof(ShowPronunciation));
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
            }

            if (string.IsNullOrWhiteSpace(sourceText))
            {
                CurrentTranslation = await _translationService.TranslateAsync(sourceText, _targetLanguage, null, _translationCts.Token);
                return;
            }

            // Use DefaultSourceLanguage from settings if not specified
            string? sourceLang = _settingsService.Settings.DefaultSourceLanguage == "auto" 
                ? null 
                : _settingsService.Settings.DefaultSourceLanguage;

            CurrentTranslation = await _translationService.TranslateAsync(sourceText, _targetLanguage, sourceLang, _translationCts.Token);
        }
        catch (TaskCanceledException) { }
        catch (Exception ex)
        {
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

    public void HideWindow()
    {
        _translationCts?.Cancel();
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
