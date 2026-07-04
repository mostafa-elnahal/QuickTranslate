using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using QuickTranslate.Helpers;
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

    [ObservableProperty]
    private bool _isVisible;

    [ObservableProperty]
    private bool _isExpanded;

    [ObservableProperty]
    private string _capturedText = string.Empty;

    [ObservableProperty]
    private ToolbarDisplayMode _mode = ToolbarDisplayMode.Selection;

    [ObservableProperty]
    private string _ocrLanguage;

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
        _autoDismissTimer.Tick += (_, _) => { DebugLog.Write("AutoDismissTimer fired"); Dismiss(); };

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
        DebugLog.Write($"OnForegroundWindowChanged: IsVisible={IsVisible}, hwnd={hwnd}");
        if (!IsVisible) return;

        if (Environment.TickCount64 - _lastInteractionTime < 500)
        {
            DebugLog.Write($"OnForegroundWindowChanged: skipped (recent interaction)");
            return;
        }

        DebugLog.Write($"OnForegroundWindowChanged: calling Dismiss");
        Dismiss();
    }

    public void Show(string text, ToolbarDisplayMode mode)
    {
        DebugLog.Write($"Show: text='{text}', mode={mode}");

        CapturedText = text;
        Mode = mode;
        IsExpanded = mode == ToolbarDisplayMode.Ocr;
        IsVisible = true;

        RestartAutoDismissTimer();
    }

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
        string text = CapturedText;

        if (Mode == ToolbarDisplayMode.Ocr && _ocrBitmap != null)
        {
            text = await _ocrService.RecognizeFromBitmapAsync(_ocrBitmap, OcrLanguage);
        }

        DebugLog.Write($"TranslateCommand: text='{text}', raising TranslateRequested");
        Dismiss();
        TranslateRequested?.Invoke(text);
    }

    [RelayCommand]
    private async Task Pronounce()
    {
        string text = CapturedText;

        if (Mode == ToolbarDisplayMode.Ocr && _ocrBitmap != null)
        {
            text = await _ocrService.RecognizeFromBitmapAsync(_ocrBitmap, OcrLanguage);
        }

        DebugLog.Write($"PronounceCommand: text='{text}', raising PronounceRequested");
        Dismiss();
        PronounceRequested?.Invoke(text);
    }

    [RelayCommand]
    private async Task Copy()
    {
        string text = CapturedText;

        if (Mode == ToolbarDisplayMode.Ocr && _ocrBitmap != null)
        {
            text = await _ocrService.RecognizeFromBitmapAsync(_ocrBitmap, OcrLanguage);
        }

        DebugLog.Write($"CopyCommand: text='{text}', copying then dismissing");
        if (!string.IsNullOrEmpty(text))
        {
            _clipboardService.SetText(text);
        }
        Dismiss();
    }

    [RelayCommand]
    private void Dismiss()
    {
        DebugLog.Write($"Dismiss: IsVisible={IsVisible}, IsExpanded={IsExpanded}");
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
