using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using QuickTranslate.Services;
using QuickTranslate.Models;

namespace QuickTranslate;

/// <summary>
/// ViewModel for the main translation window
/// </summary>
public class MainViewModel : INotifyPropertyChanged, IDisposable
{
    private readonly ITranslationService _translationService;
    private bool _isLoading;
    private TranslationModel? _currentTranslation;
    private Visibility _windowVisibility = Visibility.Collapsed;
    private string _targetLanguage = "ar"; // Default to Arabic

    public event PropertyChangedEventHandler? PropertyChanged;

    public MainViewModel() : this(new GTranslateService())
    {
    }

    public MainViewModel(ITranslationService translationService)
    {
        _translationService = translationService;
        InitializeProviders();
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

    public bool IsLoading
    {
        get => _isLoading;
        set
        {
            if (_isLoading != value)
            {
                _isLoading = value;
                OnPropertyChanged();
            }
        }
    }

    public TranslationModel? CurrentTranslation
    {
        get => _currentTranslation;
        set
        {
            if (_currentTranslation != value)
            {
                _currentTranslation = value;
                OnPropertyChanged();
            }
        }
    }

    public Visibility WindowVisibility
    {
        get => _windowVisibility;
        set
        {
            if (_windowVisibility != value)
            {
                _windowVisibility = value;
                OnPropertyChanged();
            }
        }
    }

    public string TargetLanguage
    {
        get => _targetLanguage;
        set
        {
            if (_targetLanguage != value)
            {
                _targetLanguage = value;
                OnPropertyChanged();
            }
        }
    }

    public string CurrentProviderName => _translationService.ProviderName;

    public string[] AvailableProviders => _translationService.GetAvailableProviders();

    public ObservableCollection<ProviderInfo> Providers { get; } = new();

    #endregion

    #region Public Methods

    /// <summary>
    /// Main workflow: translates the provided source text
    /// </summary>
    public async Task TranslateAsync(string sourceText)
    {
        try
        {
            // Show window and set loading state
            WindowVisibility = Visibility.Visible;
            IsLoading = true;
            CurrentTranslation = null;

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
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// Changes the translation provider.
    /// </summary>
    public void SetProvider(string providerName)
    {
        _translationService.SetProvider(providerName);
        
        // Update selection state in Providers
        foreach (var p in Providers)
        {
            p.IsSelected = p.Name == providerName;
        }
        
        OnPropertyChanged(nameof(CurrentProviderName));
        OnPropertyChanged(nameof(Providers));
    }

    /// <summary>
    /// Hides the translation window
    /// </summary>
    public void HideWindow()
    {
        WindowVisibility = Visibility.Collapsed;
        CurrentTranslation = null;
        IsLoading = false;
    }

    #endregion

    #region INotifyPropertyChanged

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
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
