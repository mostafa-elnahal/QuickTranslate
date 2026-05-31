using System;
using System.Windows;

namespace QuickTranslate.Services.Input;

/// <summary>
/// Service that abstracts low-level WH_MOUSE_LL and WH_KEYBOARD_LL Win32 hooks.
/// </summary>
public interface IGlobalInputHookService : IDisposable
{
    /// <summary>Raised on WM_LBUTTONDOWN or WM_RBUTTONDOWN</summary>
    event Action<Point>? MouseDownDetected;

    /// <summary>Raised on WM_LBUTTONDOWN</summary>
    event Action<Point>? MouseLeftButtonDown;

    /// <summary>Raised on WM_LBUTTONUP</summary>
    event Action<Point>? MouseLeftButtonUp;

    /// <summary>Raised on WM_KEYDOWN or WM_SYSKEYDOWN with virtual key code</summary>
    event Action<int>? KeyDown;

    /// <summary>Raised on WM_KEYUP or WM_SYSKEYUP with virtual key code</summary>
    event Action<int>? KeyUp;

    bool IsEnabled { get; set; }
    
    void Start();
    void Stop();
    
    void Suppress();
    void Unsuppress();
}
