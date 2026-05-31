using System;
using System.Windows;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using QuickTranslate.Models;
using QuickTranslate.Services;
using QuickTranslate.Services.Input;

namespace QuickTranslate.ViewModels;

/// <summary>
/// ViewModel for the floating toolbar that appears after text capture.
/// Supports collapsed (icon) and expanded (toolbar) visual states.
/// </summary>
public partial class FloatingToolbarViewModel : ObservableObject
{
    private readonly IClipboardService _clipboardService;
    private readonly DispatcherTimer _autoDismissTimer;
    private long _lastDismissTime;

    /// <summary>Raised when a pointer down occurs anywhere on screen.</summary>
    public event Action<Point>? GlobalPointerDown;

    /// <summary>Raised when the user clicks Translate. Payload is the captured text.</summary>
    public event Action<string>? TranslateRequested;

    /// <summary>Raised when the user clicks Pronounce. Payload is the captured text.</summary>
    public event Action<string>? PronounceRequested;

    /// <summary>Raised when the toolbar should be hidden (dismiss, action taken, etc.).</summary>
    public event Action? DismissRequested;

    [ObservableProperty]
    private bool _isVisible;

    [ObservableProperty]
    private bool _isExpanded;

    [ObservableProperty]
    private string _capturedText = string.Empty;

    [ObservableProperty]
    private ToolbarDisplayMode _mode = ToolbarDisplayMode.Selection;

    public FloatingToolbarViewModel(IClipboardService clipboardService, IGlobalInputHookService inputHookService)
    {
        _clipboardService = clipboardService;

        inputHookService.MouseDownDetected += p => GlobalPointerDown?.Invoke(p);

        _autoDismissTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(3)
        };
        _autoDismissTimer.Tick += (_, _) => Dismiss();
    }

    /// <summary>
    /// Shows the toolbar with the given captured text.
    /// </summary>
    public void Show(string text, ToolbarDisplayMode mode)
    {
        if (Environment.TickCount64 - _lastDismissTime < 100)
            return;

        CapturedText = text;
        Mode = mode;
        IsExpanded = mode == ToolbarDisplayMode.Ocr;
        IsVisible = true;

        RestartAutoDismissTimer();
    }

    /// <summary>
    /// Called when the mouse enters the toolbar area.
    /// </summary>
    public void OnMouseEnter()
    {
        _autoDismissTimer.Stop();

        if (Mode == ToolbarDisplayMode.Selection)
        {
            IsExpanded = true;
        }
    }

    /// <summary>
    /// Called when the mouse leaves the toolbar area.
    /// </summary>
    public void OnMouseLeave()
    {
        if (Mode == ToolbarDisplayMode.Selection)
        {
            IsExpanded = false;
        }

        RestartAutoDismissTimer();
    }

    [RelayCommand]
    private void Translate()
    {
        string text = CapturedText;
        Dismiss();
        TranslateRequested?.Invoke(text);
    }

    [RelayCommand]
    private void Pronounce()
    {
        string text = CapturedText;
        Dismiss();
        PronounceRequested?.Invoke(text);
    }

    [RelayCommand]
    private void Copy()
    {
        if (!string.IsNullOrEmpty(CapturedText))
        {
            _clipboardService.SetText(CapturedText);
        }
        Dismiss();
    }

    [RelayCommand]
    private void Dismiss()
    {
        _lastDismissTime = Environment.TickCount64;
        _autoDismissTimer.Stop();
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
