using System;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Input;
using System.Linq;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.Input.KeyboardAndMouse;

namespace QuickTranslate.Services;

public class HotkeyService : IHotkeyService
{
    private const int WM_HOTKEY = 0x0312;

    private HwndSource? _hwndSource;
    private IntPtr _windowHandle;

    // Set of registered hotkey IDs to track what we need to unregister
    private readonly System.Collections.Generic.HashSet<int> _registeredIds = new();

    public event EventHandler<int>? HotkeyPressed;

    public bool Register(int id, string hotkeyString, Window window)
    {
        // First ensure we have the window handle hook (only need to do this once)
        if (_windowHandle == IntPtr.Zero)
        {
            var helper = new WindowInteropHelper(window);
            _windowHandle = helper.EnsureHandle();
            _hwndSource = HwndSource.FromHwnd(_windowHandle);
            _hwndSource?.AddHook(WndProc);
        }

        // If this ID is already registered, unregister it first (update behavior)
        if (_registeredIds.Contains(id))
        {
            Unregister(id);
        }

        if (string.IsNullOrWhiteSpace(hotkeyString)) return false;

        (HOT_KEY_MODIFIERS fsModifiers, uint vk) = Helpers.HotkeyHelper.ParseHotkey(hotkeyString);
        if (vk == 0) return false; // invalid key

        bool success = PInvoke.RegisterHotKey(
            new HWND(_windowHandle),
            id,
            fsModifiers,
            vk);

        if (success)
        {
            _registeredIds.Add(id);
        }

        return success;
    }

    public void Unregister(int id)
    {
        if (_windowHandle != IntPtr.Zero && _registeredIds.Contains(id))
        {
            try
            {
                PInvoke.UnregisterHotKey(new HWND(_windowHandle), id);
                _registeredIds.Remove(id);
            }
            catch { /* Ignore errors on unregister */ }
        }
    }

    public void UnregisterAll()
    {
        if (_windowHandle == IntPtr.Zero) return;

        // Create a copy to iterate
        foreach (var id in _registeredIds.ToArray())
        {
            Unregister(id);
        }
    }



    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WM_HOTKEY)
        {
            int id = wParam.ToInt32();
            if (_registeredIds.Contains(id))
            {
                HotkeyPressed?.Invoke(this, id);
                handled = true;
            }
        }

        return IntPtr.Zero;
    }

    public void Dispose()
    {
        UnregisterAll();
        _hwndSource?.RemoveHook(WndProc);
        _hwndSource?.Dispose();
    }
}
