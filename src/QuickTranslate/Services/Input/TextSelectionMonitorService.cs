using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using QuickTranslate.Helpers;

namespace QuickTranslate.Services.Input;

/// <summary>
/// Monitors for text selection across the OS using IGlobalInputHookService.
/// When the user performs a drag-select (mouse down → move → mouse up), it captures
/// the selected text via clipboard and raises the TextSelected event.
/// </summary>
public partial class TextSelectionMonitorService : ITextSelectionMonitorService
{
    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern uint GetDoubleClickTime();

    private const int MIN_DRAG_DISTANCE = 5;
    private const int CAPTURE_DELAY_MS = 20;

    private const int VK_SHIFT = 0x10;
    private const int VK_LSHIFT = 0xA0;
    private const int VK_RSHIFT = 0xA1;
    private const int VK_CONTROL = 0x11;
    private const int VK_LCONTROL = 0xA2;
    private const int VK_RCONTROL = 0xA3;
    private const int VK_A = 0x41;
    private const int VK_ESCAPE = 0x1B;
    private static readonly int[] NavigationKeys = [0x21, 0x22, 0x23, 0x24, 0x25, 0x26, 0x27, 0x28]; // PgUp, PgDn, End, Home, ←, ↑, →, ↓

    private readonly IUiAutomationService _uiAutomationService;
    private readonly IClipboardService _clipboardService;
    private readonly IGlobalInputHookService _hookService;

    private Point _mouseDownPoint;
    private bool _isMouseDown;

    private bool _shiftDown;
    private bool _ctrlDown;
    private bool _pendingKeyboardSelection;

    private long _lastMouseDownTime;
    private int _clickCount;
    private readonly uint _doubleClickTimeMs;

    private readonly SemaphoreSlim _captureLock = new(1, 1);
    private int _captureGeneration;

    public event Action<string>? TextSelected;

    public bool IsEnabled 
    { 
        get => _hookService.IsEnabled; 
        set => _hookService.IsEnabled = value; 
    }

    public TextSelectionMonitorService(
        IUiAutomationService uiAutomationService, 
        IClipboardService clipboardService,
        IGlobalInputHookService hookService)
    {
        _uiAutomationService = uiAutomationService;
        _clipboardService = clipboardService;
        _hookService = hookService;
        _doubleClickTimeMs = GetDoubleClickTime();

        _hookService.MouseLeftButtonDown += OnMouseLeftButtonDown;
        _hookService.MouseLeftButtonUp += OnMouseLeftButtonUp;
        _hookService.KeyDown += OnKeyDown;
        _hookService.KeyUp += OnKeyUp;
    }

    public void Start() => _hookService.Start();
    public void Stop() => _hookService.Stop();

    private void OnMouseLeftButtonDown(Point physicalPoint)
    {
        _isMouseDown = true;

        long currentTime = Environment.TickCount64;
        if (currentTime - _lastMouseDownTime <= _doubleClickTimeMs)
        {
            double dx = physicalPoint.X - _mouseDownPoint.X;
            double dy = physicalPoint.Y - _mouseDownPoint.Y;
            if (Math.Sqrt(dx * dx + dy * dy) <= 10)
            {
                _clickCount++;
            }
            else
            {
                _clickCount = 1;
            }
        }
        else
        {
            _clickCount = 1;
        }

        _mouseDownPoint = physicalPoint;
        _lastMouseDownTime = currentTime;
    }

    private void OnMouseLeftButtonUp(Point physicalPoint)
    {
        if (!_isMouseDown) return;
        _isMouseDown = false;

        double dx = physicalPoint.X - _mouseDownPoint.X;
        double dy = physicalPoint.Y - _mouseDownPoint.Y;
        double distance = Math.Sqrt(dx * dx + dy * dy);

        if (distance >= MIN_DRAG_DISTANCE || _clickCount >= 2 || _shiftDown)
        {
            DebugLog.Write($"Mouse selection detected. Distance: {distance:F1}, Clicks: {_clickCount}, Shift: {_shiftDown}. Firing CaptureSelectionAsync...");
            _ = CaptureSelectionAsync();
        }
    }

    private void OnKeyDown(int vk)
    {
        bool isShift = vk == VK_SHIFT || vk == VK_LSHIFT || vk == VK_RSHIFT;
        bool isCtrl = vk == VK_CONTROL || vk == VK_LCONTROL || vk == VK_RCONTROL;

        if (isShift) _shiftDown = true;
        else if (isCtrl) _ctrlDown = true;

        if (_shiftDown && Array.IndexOf(NavigationKeys, vk) >= 0)
            _pendingKeyboardSelection = true;

        if (_ctrlDown && vk == VK_A)
            _pendingKeyboardSelection = true;
    }

    private void OnKeyUp(int vk)
    {
        if (vk == VK_ESCAPE)
        {
            _pendingKeyboardSelection = false;
        }

        bool isShift = vk == VK_SHIFT || vk == VK_LSHIFT || vk == VK_RSHIFT;
        bool isCtrl = vk == VK_CONTROL || vk == VK_LCONTROL || vk == VK_RCONTROL;

        if (isShift && _pendingKeyboardSelection)
        {
            _pendingKeyboardSelection = false;
            _shiftDown = false;
            DebugLog.Write("Keyboard selection detected (Shift released), firing CaptureSelectionAsync...");
            _ = CaptureSelectionAsync();
            return;
        }

        if (vk == VK_A && _pendingKeyboardSelection)
        {
            _pendingKeyboardSelection = false;
            DebugLog.Write("Keyboard selection detected (Ctrl+A), firing CaptureSelectionAsync...");
            _ = CaptureSelectionAsync();
            return;
        }

        if (isShift) _shiftDown = false;
        if (isCtrl) _ctrlDown = false;
    }

    private async Task CaptureSelectionAsync()
    {
        int myGen = Interlocked.Increment(ref _captureGeneration);

        await _captureLock.WaitAsync();
        try
        {
            // If another capture was queued while we were waiting, skip this one
            if (myGen < _captureGeneration)
            {
                DebugLog.Write("Skipping stale capture request in favor of a newer one.");
                return;
            }

            await Task.Delay(CAPTURE_DELAY_MS);

            string? text = _uiAutomationService.TryGetSelectedText();

            if (string.IsNullOrEmpty(text))
            {
                DebugLog.Write("UIA returned empty, falling back to clipboard...");
                text = await _clipboardService.CaptureSelectionAsync();
            }
            else
            {
                DebugLog.Write("UIA succeeded, clipboard not touched.");
            }

            DebugLog.Write($"Captured text: '{text}' (Length: {text?.Length})");

            if (!string.IsNullOrWhiteSpace(text))
            {
                Application.Current?.Dispatcher.Invoke(() => TextSelected?.Invoke(text));
            }
        }
        catch (Exception ex)
        {
            DebugLog.Write($"Exception: {ex}");
            Debug.WriteLine($"TextSelectionMonitor capture failed: {ex.Message}");
        }
        finally
        {
            _captureLock.Release();
        }
    }

    public void Dispose()
    {
        _hookService.MouseLeftButtonDown -= OnMouseLeftButtonDown;
        _hookService.MouseLeftButtonUp -= OnMouseLeftButtonUp;
        _hookService.KeyDown -= OnKeyDown;
        _hookService.KeyUp -= OnKeyUp;
        Stop();
    }
}
