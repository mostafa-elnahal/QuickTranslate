using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Threading;

namespace QuickTranslate.Services.Input;

public class ForegroundWindowMonitorService : IForegroundWindowMonitorService
{
    private const uint EVENT_SYSTEM_FOREGROUND = 0x0003;
    private const uint WINEVENT_OUTOFCONTEXT = 0x0000;

    private IntelligentWinEventDelegate? _winEventDelegate;
    private IntPtr _hookId = IntPtr.Zero;
    private readonly Dispatcher _dispatcher;

    public event Action<IntPtr>? ForegroundWindowChanged;

    public ForegroundWindowMonitorService()
    {
        _dispatcher = Dispatcher.CurrentDispatcher;
    }

    public void Start()
    {
        if (_hookId != IntPtr.Zero) return;

        _winEventDelegate = WinEventProc;
        _hookId = SetWinEventHook(
            EVENT_SYSTEM_FOREGROUND, EVENT_SYSTEM_FOREGROUND,
            IntPtr.Zero, _winEventDelegate,
            0, 0, WINEVENT_OUTOFCONTEXT);
    }

    public void Stop()
    {
        if (_hookId != IntPtr.Zero)
        {
            UnhookWinEvent(_hookId);
            _hookId = IntPtr.Zero;
        }
        _winEventDelegate = null;
    }

    private void WinEventProc(IntPtr hWinEventHook, uint eventType, IntPtr hwnd, int idObject, int idChild, uint dwEventThread, uint dwmsEventTime)
    {
        if (eventType == EVENT_SYSTEM_FOREGROUND)
        {
            // Invoke on the UI thread to ensure listeners (like ViewModels) can act safely
            _dispatcher.InvokeAsync(() => ForegroundWindowChanged?.Invoke(hwnd));
        }
    }

    public void Dispose()
    {
        Stop();
    }

    // Win32 Interop
    private delegate void IntelligentWinEventDelegate(IntPtr hWinEventHook, uint eventType, IntPtr hwnd, int idObject, int idChild, uint dwEventThread, uint dwmsEventTime);

    [DllImport("user32.dll")]
    private static extern IntPtr SetWinEventHook(uint eventMin, uint eventMax, IntPtr hmodWinEventProc, IntelligentWinEventDelegate lpfnWinEventProc, uint idProcess, uint idThread, uint dwFlags);

    [DllImport("user32.dll")]
    private static extern bool UnhookWinEvent(IntPtr hWinEventHook);
}
