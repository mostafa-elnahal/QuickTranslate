using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using Windows.Win32;

namespace QuickTranslate.Services.Input;

/// <summary>
/// Monitors for text selection gestures across the OS using IGlobalInputHookService.
/// When the user performs a selection gesture (drag-release, multi-click, or keyboard selection),
/// it fires the SelectionDetected event with the cursor coordinates without performing eager capture.
/// </summary>
public class TextSelectionMonitorService : ITextSelectionMonitorService
{
    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern uint GetDoubleClickTime();

    private const int MIN_DRAG_DISTANCE = 5;
    private const int SELECTION_DEBOUNCE_MS = 50;

    private const int VK_SHIFT = 0x10;
    private const int VK_LSHIFT = 0xA0;
    private const int VK_RSHIFT = 0xA1;
    private const int VK_CONTROL = 0x11;
    private const int VK_LCONTROL = 0xA2;
    private const int VK_RCONTROL = 0xA3;
    private const int VK_A = 0x41;
    private const int VK_ESCAPE = 0x1B;
    private static readonly int[] NavigationKeys = [0x21, 0x22, 0x23, 0x24, 0x25, 0x26, 0x27, 0x28]; // PgUp, PgDn, End, Home, ←, ↑, →, ↓

    private readonly IGlobalInputHookService _hookService;

    private Point _mouseDownPoint;
    private bool _isMouseDown;

    private bool _shiftDown;
    private bool _ctrlDown;
    private bool _pendingKeyboardSelection;

    private long _lastMouseDownTime;
    private int _clickCount;
    private readonly uint _doubleClickTimeMs;

    private int _selectionGeneration;

    public event Action<Point>? SelectionDetected;

    public bool IsEnabled 
    { 
        get => _hookService.IsEnabled; 
        set => _hookService.IsEnabled = value; 
    }

    public TextSelectionMonitorService(IGlobalInputHookService hookService)
    {
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
            TriggerSelectionDetected(physicalPoint);
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
            TriggerSelectionDetected(null);
            return;
        }

        if (vk == VK_A && _pendingKeyboardSelection)
        {
            _pendingKeyboardSelection = false;
            TriggerSelectionDetected(null);
            return;
        }

        if (isShift) _shiftDown = false;
        if (isCtrl) _ctrlDown = false;
    }

    private void TriggerSelectionDetected(Point? point)
    {
        int currentGen = Interlocked.Increment(ref _selectionGeneration);
        _ = Task.Run(async () =>
        {
            await Task.Delay(SELECTION_DEBOUNCE_MS).ConfigureAwait(false);
            if (currentGen != _selectionGeneration) return;

            Point pt;
            if (point.HasValue)
            {
                pt = point.Value;
            }
            else
            {
                if (PInvoke.GetCursorPos(out var cursorPos))
                {
                    pt = new Point(cursorPos.X, cursorPos.Y);
                }
                else
                {
                    pt = new Point(0, 0);
                }
            }

            Application.Current?.Dispatcher.BeginInvoke(() => SelectionDetected?.Invoke(pt));
        });
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
