using System;
using System.Threading;
using System.Runtime.InteropServices;
using QuickTranslate.Helpers;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;

namespace QuickTranslate.Services;

public class ClipboardService : IClipboardService
{
    public string CaptureSelection()
    {
        // Tier 1: Win32 Messages (instant 0ms, safe for standard Win32 / Edit controls)
        string capturedText = CaptureViaWin32();
        if (!string.IsNullOrEmpty(capturedText))
        {
            return capturedText;
        }

        // Tier 2: Direct Win32 Clipboard (SendInput Ctrl+C, safe and universal across all apps)
        capturedText = CaptureViaClipboard();

        return capturedText ?? string.Empty;
    }

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern int GetClassName(IntPtr hWnd, System.Text.StringBuilder lpClassName, int nMaxCount);

    private static bool IsWin32EditControl(HWND hwnd)
    {
        if (hwnd == IntPtr.Zero) return false;
        var sb = new System.Text.StringBuilder(64);
        if (GetClassName(hwnd, sb, sb.Capacity) == 0) return false;
        string className = sb.ToString();
        return className.Equals("Edit", StringComparison.OrdinalIgnoreCase)
            || className.StartsWith("RichEdit", StringComparison.OrdinalIgnoreCase)
            || className.StartsWith("RICHEDIT", StringComparison.OrdinalIgnoreCase);
    }

    unsafe private string CaptureViaWin32()
    {
        try
        {
            HWND fgWindow = PInvoke.GetForegroundWindow();
            if (fgWindow == IntPtr.Zero) return string.Empty;

            uint fgThread = PInvoke.GetWindowThreadProcessId(fgWindow, null);

            var guiThreadInfo = new GUITHREADINFO();
            guiThreadInfo.cbSize = (uint)Marshal.SizeOf(guiThreadInfo);

            if (PInvoke.GetGUIThreadInfo(fgThread, ref guiThreadInfo))
            {
                HWND focusHwnd = guiThreadInfo.hwndFocus;
                // Only send EM_GETSEL to verified native Win32 Edit/RichEdit controls.
                // Sending EM_GETSEL to Chromium/Electron render widgets or WPF/custom controls
                // is a synchronous cross-process call that causes stalls and returns nothing.
                if (focusHwnd != IntPtr.Zero && IsWin32EditControl(focusHwnd))
                {
                    int start = 0, end = 0;
                    PInvoke.SendMessage(focusHwnd, PInvoke.EM_GETSEL, new WPARAM((nuint)(void*)&start), new LPARAM((nint)(void*)&end));
                    
                    if (start == end) return string.Empty;

                    if (start > end)
                    {
                        int temp = start;
                        start = end;
                        end = temp;
                    }

                    LRESULT lenRes = PInvoke.SendMessage(focusHwnd, PInvoke.WM_GETTEXTLENGTH, default, default);
                    int textLength = (int)lenRes.Value;
                    if (textLength <= 0) return string.Empty;

                    char[] buffer = new char[textLength + 1];
                    fixed (char* pBuffer = buffer)
                    {
                        PInvoke.SendMessage(focusHwnd, PInvoke.WM_GETTEXT, new WPARAM((nuint)(textLength + 1)), new LPARAM((nint)pBuffer));
                    }

                    string fullText = new string(buffer, 0, textLength);
                    
                    if (start >= 0 && end <= fullText.Length)
                    {
                        return fullText.Substring(start, end - start);
                    }
                }
            }
        }
        catch
        {
        }
        return string.Empty;
    }

    private string CaptureViaClipboard()
    {
        // 1. Preserve ALL clipboard formats
        ClipboardHelper.ClipboardSnapshot? snapshot = null;
        try
        {
            snapshot = ClipboardHelper.SaveSnapshot();
        }
        catch
        {
        }

        Thread.Sleep(10);

        // 2. Clear & Copy selection
        ClipboardHelper.ClearSafe();
        
        // Failsafe wait
        Thread.Sleep(10);
        
        ClipboardHelper.SendCopyCommand();

        // 3. Wait for clipboard to populate
        string capturedText = ClipboardHelper.GetTextWithTimeout();

        // 4. Restore original clipboard WITHOUT adding to history
        try
        {
            if (snapshot != null && snapshot.HasContent)
            {
                ClipboardHelper.RestoreWithoutHistory(snapshot);
            }
            else
            {
                ClipboardHelper.ClearWithoutHistory();
            }
        }
        finally
        {
            snapshot?.Dispose();
        }

        return capturedText ?? string.Empty;
    }

    public async System.Threading.Tasks.Task<string> CaptureSelectionAsync()
    {
        var tcs = new System.Threading.Tasks.TaskCompletionSource<string>();
        var thread = new Thread(() =>
        {
            try
            {
                tcs.SetResult(CaptureSelection());
            }
            catch (Exception ex)
            {
                tcs.SetException(ex);
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.IsBackground = true;
        thread.Start();

        return await tcs.Task;
    }

    public void SetText(string text)
    {
        if (string.IsNullOrEmpty(text)) return;
        
        for (int i = 0; i < 5; i++)
        {
            try
            {
                System.Windows.Clipboard.SetText(text);
                return;
            }
            catch
            {
                Thread.Sleep(10);
            }
        }
    }
}
