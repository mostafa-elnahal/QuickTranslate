using System;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows;
using QuickTranslate.Helpers;

namespace QuickTranslate.Services.Input;

/// <summary>
/// Monitors for text selection across the OS using IGlobalInputHookService.
/// When the user performs a drag-select (mouse down → move → mouse up), it captures
/// the selected text via clipboard and raises the TextSelected event.
/// </summary>
public class TextSelectionMonitorService : ITextSelectionMonitorService
{
    private const int MIN_DRAG_DISTANCE = 5;
    private const int CAPTURE_DELAY_MS = 20;

    private const int VK_SHIFT = 0x10;
    private const int VK_CONTROL = 0x11;
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
    private bool _isDoubleClick;
    private readonly uint _doubleClickTimeMs;

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
        _doubleClickTimeMs = unchecked((uint)System.Windows.Forms.SystemInformation.DoubleClickTime);

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
            _isDoubleClick = Math.Sqrt(dx * dx + dy * dy) <= 10;
        }
        else
        {
            _isDoubleClick = false;
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

        if (distance >= MIN_DRAG_DISTANCE || _isDoubleClick)
        {
            _isDoubleClick = false;
            DebugLog.Write($"Mouse selection detected. Distance: {distance:F1}, firing CaptureSelectionAsync...");
            _ = CaptureSelectionAsync();
        }
    }

    private void OnKeyDown(int vk)
    {
        if (vk == VK_SHIFT) _shiftDown = true;
        else if (vk == VK_CONTROL) _ctrlDown = true;

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

        if (vk == VK_SHIFT && _pendingKeyboardSelection)
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

        if (vk == VK_SHIFT) _shiftDown = false;
        if (vk == VK_CONTROL) _ctrlDown = false;
    }

    private async Task CaptureSelectionAsync()
    {
        _hookService.Suppress();

        try
        {
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
                _hookService.Unsuppress();
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
            _hookService.Unsuppress();
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
