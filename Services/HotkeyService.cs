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

    public void Register(int id, string hotkeyString, Window window)
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

        if (string.IsNullOrWhiteSpace(hotkeyString)) return;

        (uint fsModifiers, uint vk) = ParseHotkey(hotkeyString);
        if (vk == 0) return; // invalid key

        bool success = PInvoke.RegisterHotKey(
            new HWND(_windowHandle),
            id,
            (HOT_KEY_MODIFIERS)fsModifiers,
            vk);

        if (!success)
        {
            MessageBox.Show(
                $"Failed to register hotkey '{hotkeyString}'. It may be in use by another application.",
                "QuickTranslate",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
        else
        {
            _registeredIds.Add(id);
        }
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

    private (uint modifiers, uint vk) ParseHotkey(string hotkey)
    {
        uint modifiers = 0;
        // Constants for modifiers (MOD_ALT=1, MOD_CONTROL=2, MOD_SHIFT=4, MOD_WIN=8)
        const uint MOD_ALT = 0x0001;
        const uint MOD_CONTROL = 0x0002;
        const uint MOD_SHIFT = 0x0004;
        const uint MOD_WIN = 0x0008;

        var parts = hotkey.Split('+');
        System.Windows.Input.Key keyToMap = Key.None;

        foreach (var part in parts)
        {
            var trimmed = part.Trim();
            if (trimmed.Equals("Ctrl", StringComparison.OrdinalIgnoreCase))
                modifiers |= MOD_CONTROL;
            else if (trimmed.Equals("Alt", StringComparison.OrdinalIgnoreCase))
                modifiers |= MOD_ALT;
            else if (trimmed.Equals("Shift", StringComparison.OrdinalIgnoreCase))
                modifiers |= MOD_SHIFT;
            else if (trimmed.Equals("Win", StringComparison.OrdinalIgnoreCase))
                modifiers |= MOD_WIN;
            else
            {
                keyToMap = ParseKeyString(trimmed);
            }
        }

        int virtualKey = KeyInterop.VirtualKeyFromKey(keyToMap);
        return (modifiers, (uint)virtualKey);
    }

    private Key ParseKeyString(string keyStr)
    {
        // Handle special custom formatting from HotkeyEditorDialog
        if (keyStr.StartsWith("Num "))
        {
            string numPart = keyStr.Substring(4);
            if (int.TryParse(numPart, out int digit))
                return (Key)(Key.NumPad0 + digit);

            return numPart switch
            {
                "*" => Key.Multiply,
                "+" => Key.Add,
                "-" => Key.Subtract,
                "/" => Key.Divide,
                "." => Key.Decimal,
                _ => Key.None
            };
        }

        // Handle simple mappings
        return keyStr switch
        {
            "," => Key.OemComma,
            "." => Key.OemPeriod,
            "+" => Key.OemPlus,
            "-" => Key.OemMinus,
            _ => Enum.TryParse<Key>(keyStr, true, out var result) ? result : Key.None
        };
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
