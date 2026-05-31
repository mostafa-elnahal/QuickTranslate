using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using QuickTranslate.Helpers;

namespace QuickTranslate.Services.Input;

public class GlobalInputHookService : IGlobalInputHookService
{
    private const int WH_MOUSE_LL = 14;
    private const int WH_KEYBOARD_LL = 13;
    private const int WM_LBUTTONDOWN = 0x0201;
    private const int WM_LBUTTONUP = 0x0202;
    private const int WM_RBUTTONDOWN = 0x0204;
    private const int WM_KEYDOWN = 0x0100;
    private const int WM_KEYUP = 0x0101;
    private const int WM_SYSKEYDOWN = 0x0104;
    private const int WM_SYSKEYUP = 0x0105;

    private IntPtr _mouseHookId = IntPtr.Zero;
    private LowLevelHookProc? _mouseHookProc;

    private IntPtr _keyboardHookId = IntPtr.Zero;
    private LowLevelHookProc? _keyboardHookProc;

    private bool _isSuppressed;

    public event Action<Point>? MouseDownDetected;
    public event Action<Point>? MouseLeftButtonDown;
    public event Action<Point>? MouseLeftButtonUp;
    public event Action<int>? KeyDown;
    public event Action<int>? KeyUp;

    public bool IsEnabled { get; set; } = true;

    public void Start()
    {
        if (_mouseHookId != IntPtr.Zero) return;

        _mouseHookProc = MouseHookCallback;
        _mouseHookId = SetWindowsHookEx(WH_MOUSE_LL, _mouseHookProc, GetModuleHandle(Process.GetCurrentProcess().MainModule!.ModuleName), 0);

        _keyboardHookProc = KeyboardHookCallback;
        _keyboardHookId = SetWindowsHookEx(WH_KEYBOARD_LL, _keyboardHookProc, GetModuleHandle(Process.GetCurrentProcess().MainModule!.ModuleName), 0);
    }

    public void Stop()
    {
        UninstallHook(ref _mouseHookId);
        UninstallHook(ref _keyboardHookId);
    }

    private static void UninstallHook(ref IntPtr hookId)
    {
        if (hookId != IntPtr.Zero)
        {
            UnhookWindowsHookEx(hookId);
            hookId = IntPtr.Zero;
        }
    }

    public void Suppress() => _isSuppressed = true;
    public void Unsuppress() => _isSuppressed = false;

    private IntPtr MouseHookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0 && IsEnabled && !_isSuppressed)
        {
            var hookStruct = Marshal.PtrToStructure<MSLLHOOKSTRUCT>(lParam);

            if (hookStruct.dwExtraInfo == (IntPtr)ClipboardHelper.QTAG_EXTRA_INFO)
                return CallNextHookEx(_mouseHookId, nCode, wParam, lParam);

            int msg = wParam.ToInt32();
            var point = new Point(hookStruct.pt.x, hookStruct.pt.y);

            if (msg == WM_LBUTTONDOWN || msg == WM_RBUTTONDOWN)
                MouseDownDetected?.Invoke(point);

            if (msg == WM_LBUTTONDOWN)
                MouseLeftButtonDown?.Invoke(point);
            else if (msg == WM_LBUTTONUP)
                MouseLeftButtonUp?.Invoke(point);
        }

        return CallNextHookEx(_mouseHookId, nCode, wParam, lParam);
    }

    private IntPtr KeyboardHookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0 && IsEnabled && !_isSuppressed)
        {
            var hookStruct = Marshal.PtrToStructure<KBDLLHOOKSTRUCT>(lParam);

            if (hookStruct.dwExtraInfo == (IntPtr)ClipboardHelper.QTAG_EXTRA_INFO)
                return CallNextHookEx(_keyboardHookId, nCode, wParam, lParam);

            int msg = wParam.ToInt32();
            int vkCode = (int)hookStruct.vkCode;

            if (msg == WM_KEYDOWN || msg == WM_SYSKEYDOWN)
                KeyDown?.Invoke(vkCode);
            else if (msg == WM_KEYUP || msg == WM_SYSKEYUP)
                KeyUp?.Invoke(vkCode);
        }

        return CallNextHookEx(_keyboardHookId, nCode, wParam, lParam);
    }

    public void Dispose()
    {
        Stop();
    }

    // Win32 Interop
    private delegate IntPtr LowLevelHookProc(int nCode, IntPtr wParam, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT { public int x; public int y; }

    [StructLayout(LayoutKind.Sequential)]
    private struct MSLLHOOKSTRUCT
    {
        public POINT pt;
        public uint mouseData;
        public uint flags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct KBDLLHOOKSTRUCT
    {
        public uint vkCode;
        public uint scanCode;
        public uint flags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelHookProc lpfn, IntPtr hMod, uint dwThreadId);

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnhookWindowsHookEx(IntPtr hhk);

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr GetModuleHandle(string lpModuleName);
}
