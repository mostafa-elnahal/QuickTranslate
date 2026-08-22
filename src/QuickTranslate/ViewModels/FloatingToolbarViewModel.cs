using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using QuickTranslate.Models;
using QuickTranslate.Services;
using QuickTranslate.Services.Input;

namespace QuickTranslate.ViewModels;

public partial class FloatingToolbarViewModel : ObservableObject
{
    private readonly IClipboardService _clipboardService;
    private readonly IOcrService _ocrService;
    private readonly ISettingsService _settingsService;
    private readonly DispatcherTimer _autoDismissTimer;
    private long _lastInteractionTime;
    private System.Drawing.Bitmap? _ocrBitmap;

    public event Action<Point>? GlobalPointerDown;

    public event Action<string>? TranslateRequested;

    public event Action<string>? PronounceRequested;

    public event Action? DismissRequested;

    [ObservableProperty] private bool _isVisible;
    [ObservableProperty] private bool _isExpanded;
    [ObservableProperty] private string _capturedText = string.Empty;
    [ObservableProperty] private ToolbarDisplayMode _mode = ToolbarDisplayMode.Selection;
    [ObservableProperty] private string _ocrLanguage;

    public ObservableCollection<OcrLanguage> OcrAvailableLanguages { get; } = new();

    public FloatingToolbarViewModel(
        IClipboardService clipboardService,
        IGlobalInputHookService inputHookService,
        IForegroundWindowMonitorService foregroundMonitor,
        IOcrService ocrService,
        ISettingsService settingsService)
    {
        _clipboardService = clipboardService;
        _ocrService = ocrService;
        _settingsService = settingsService;

        inputHookService.MouseDownDetected += p => GlobalPointerDown?.Invoke(p);

        foregroundMonitor.ForegroundWindowChanged += OnForegroundWindowChanged;

        _autoDismissTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(3)
        };
        _autoDismissTimer.Tick += (_, _) => { Dismiss(); };

        _ocrLanguage = _settingsService.Settings.OcrLanguage;
        LoadOcrLanguages();
    }

    partial void OnOcrLanguageChanged(string value)
    {
        _ocrService.CurrentLanguageCode = value;
        _settingsService.Settings.OcrLanguage = value;
        _ = _settingsService.SaveAsync();
    }

    private void LoadOcrLanguages()
    {
        var languages = _ocrService.GetAvailableLanguages();
        OcrAvailableLanguages.Clear();
        foreach (var lang in languages)
        {
            OcrAvailableLanguages.Add(lang);
        }
    }

    public void SetOcrBitmap(System.Drawing.Bitmap bitmap)
    {
        DisposeOcrBitmap();
        _ocrBitmap = bitmap;
    }

    private void DisposeOcrBitmap()
    {
        _ocrBitmap?.Dispose();
        _ocrBitmap = null;
    }

    private void OnForegroundWindowChanged(IntPtr hwnd)
    {
        if (!IsVisible) return;

        if (Environment.TickCount64 - _lastInteractionTime < 500)
        {
            return;
        }

        Dismiss();
    }

    public void Show(string text, ToolbarDisplayMode mode)
    {
        CapturedText = text;
        Mode = mode;
        IsExpanded = mode == ToolbarDisplayMode.Ocr;
        IsVisible = true;

        RestartAutoDismissTimer();
    }

    public void Show(ToolbarDisplayMode mode) => Show(string.Empty, mode);

    public void OnMouseEnter()
    {
        _autoDismissTimer.Stop();

        if (Mode == ToolbarDisplayMode.Selection)
        {
            IsExpanded = true;
        }
    }

    public void OnMouseLeave()
    {
        if (Mode == ToolbarDisplayMode.Selection)
        {
            IsExpanded = false;
        }

        RestartAutoDismissTimer();
    }

    public void OnPointerDownInsideToolbar()
    {
        _lastInteractionTime = Environment.TickCount64;
    }

    [RelayCommand]
    private async Task Translate()
    {
        string text = await ResolveTargetTextAsync();

        Dismiss();

        if (!string.IsNullOrWhiteSpace(text))
        {
            TranslateRequested?.Invoke(text);
        }
    }

    [RelayCommand]
    private async Task Pronounce()
    {
        string text = await ResolveTargetTextAsync();

        Dismiss();

        if (!string.IsNullOrWhiteSpace(text))
        {
            PronounceRequested?.Invoke(text);
        }
    }

    [RelayCommand]
    private async Task Copy()
    {
        string text = await ResolveTargetTextAsync();

        Dismiss();

        if (!string.IsNullOrEmpty(text))
        {
            _clipboardService.SetText(text);
        }
    }

    private async Task<string> ResolveTargetTextAsync()
    {
        if (Mode == ToolbarDisplayMode.Ocr && _ocrBitmap != null)
        {
            return await _ocrService.RecognizeFromBitmapAsync(_ocrBitmap, OcrLanguage);
        }

        if (!string.IsNullOrEmpty(CapturedText))
        {
            return CapturedText;
        }

        // On-demand lazy capture for Selection mode
        try
        {
            return await _clipboardService.CaptureSelectionAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"On-demand capture failed: {ex.Message}");
            return string.Empty;
        }
    }

    [RelayCommand]
    private void Dismiss()
    {
        _autoDismissTimer.Stop();
        DisposeOcrBitmap();
        IsVisible = false;
        IsExpanded = false;
        CapturedText = string.Empty;
        DismissRequested?.Invoke();
    }

    private void RestartAutoDismissTimer()
    {
        _autoDismissTimer.Stop();
        _autoDismissTimer.Start();
    }
}
