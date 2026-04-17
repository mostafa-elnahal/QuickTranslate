using System;
using System.Collections.Generic;
using System.Windows.Input;
using System.Windows.Interop;
using Windows.Win32.UI.Input.KeyboardAndMouse;
using Windows.Win32;

namespace QuickTranslate.Helpers;

/// <summary>
/// Helper class for parsing and formatting hotkey strings.
/// </summary>
internal static class HotkeyHelper
{
    /// <summary>
    /// Parses a hotkey string (e.g., "Ctrl+Q") into the native modifiers and virtual key code.
    /// </summary>
    public static (HOT_KEY_MODIFIERS modifiers, uint vk) ParseHotkey(string hotkey)
    {
        HOT_KEY_MODIFIERS modifiers = HOT_KEY_MODIFIERS.MOD_NOREPEAT;

        if (string.IsNullOrWhiteSpace(hotkey))
            return (modifiers, 0);

        var parts = hotkey.Split('+');
        Key keyToMap = Key.None;

        foreach (var part in parts)
        {
            var trimmed = part.Trim();
            if (trimmed.Equals("Ctrl", StringComparison.OrdinalIgnoreCase))
                modifiers |= HOT_KEY_MODIFIERS.MOD_CONTROL;
            else if (trimmed.Equals("Alt", StringComparison.OrdinalIgnoreCase))
                modifiers |= HOT_KEY_MODIFIERS.MOD_ALT;
            else if (trimmed.Equals("Shift", StringComparison.OrdinalIgnoreCase))
                modifiers |= HOT_KEY_MODIFIERS.MOD_SHIFT;
            else if (trimmed.Equals("Win", StringComparison.OrdinalIgnoreCase))
                modifiers |= HOT_KEY_MODIFIERS.MOD_WIN;
            else
            {
                keyToMap = ParseKeyString(trimmed);
            }
        }

        int virtualKey = KeyInterop.VirtualKeyFromKey(keyToMap);
        return (modifiers, (uint)virtualKey);
    }

    /// <summary>
    /// Parses a key string representation into a WPF Key enum.
    /// </summary>
    public static Key ParseKeyString(string keyStr)
    {
        // Handle special custom formatting
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

    /// <summary>
    /// Formats a key and modifiers into a hotkey string (e.g., "Ctrl+Alt+T").
    /// </summary>
    public static string FormatHotkey(Key key, ModifierKeys modifiers)
    {
        var hotkeyParts = new List<string>();

        if (modifiers.HasFlag(ModifierKeys.Control))
            hotkeyParts.Add("Ctrl");
        if (modifiers.HasFlag(ModifierKeys.Alt))
            hotkeyParts.Add("Alt");
        if (modifiers.HasFlag(ModifierKeys.Shift))
            hotkeyParts.Add("Shift");
        if (modifiers.HasFlag(ModifierKeys.Windows))
            hotkeyParts.Add("Win");

        // Convert key to readable string
        string keyName = FormatKeyName(key);
        hotkeyParts.Add(keyName);

        return string.Join("+", hotkeyParts);
    }

    private static string FormatKeyName(Key key)
    {
        if (key >= Key.D0 && key <= Key.D9)
            return ((int)key - (int)Key.D0).ToString();

        if (key >= Key.NumPad0 && key <= Key.NumPad9)
            return "Num " + ((int)key - (int)Key.NumPad0);

        return key switch
        {
            Key.Multiply => "Num *",
            Key.Add => "Num +",
            Key.Subtract => "Num -",
            Key.Divide => "Num /",
            Key.Decimal => "Num .",
            Key.OemPlus => "+",
            Key.OemMinus => "-",
            Key.OemPeriod => ".",
            Key.OemComma => ",",
            _ => key.ToString()
        };
    }
}
