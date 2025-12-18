using System;
using System.Windows;
using System.Windows.Interop;
using QuickTranslate.Interop;

namespace QuickTranslate.Services;

public class HotkeyService : IHotkeyService
{
    private const int HOTKEY_ID = 9000;
    private const int WM_HOTKEY = 0x0312;
    private const int VK_F1 = 0x70;

    private HwndSource? _hwndSource;
    private IntPtr _windowHandle;

    public event EventHandler? HotkeyPressed;

    public void Register(Window window)
    {
        var helper = new WindowInteropHelper(window);
        _windowHandle = helper.EnsureHandle();

        _hwndSource = HwndSource.FromHwnd(_windowHandle);
        _hwndSource?.AddHook(WndProc);

        bool success = NativeMethods.RegisterHotKey(
            _windowHandle,
            HOTKEY_ID,
            0, // No modifiers
            VK_F1);

        if (!success)
        {
            MessageBox.Show(
                "Failed to register hotkey F1. It may be in use by another application.",
                "QuickTranslate",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }

    public void Unregister()
    {
        if (_windowHandle != IntPtr.Zero)
        {
            NativeMethods.UnregisterHotKey(_windowHandle, HOTKEY_ID);
        }
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WM_HOTKEY && wParam.ToInt32() == HOTKEY_ID)
        {
            HotkeyPressed?.Invoke(this, EventArgs.Empty);
            handled = true;
        }

        return IntPtr.Zero;
    }

    public void Dispose()
    {
        Unregister();
        _hwndSource?.RemoveHook(WndProc);
        _hwndSource?.Dispose();
    }
}
