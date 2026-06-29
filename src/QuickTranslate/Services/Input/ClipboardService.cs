using System;
using System.Threading;
using System.Windows.Forms;
using System.Windows.Automation;
using System.Runtime.InteropServices;
using QuickTranslate.Helpers;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;
using System.Text;

namespace QuickTranslate.Services;

public class ClipboardService : IClipboardService
{
    public string CaptureSelection()
    {
        Log("Starting Capture Strategy...");

        // Tier 1: UI Automation
        string capturedText = CaptureViaUIA();
        if (!string.IsNullOrEmpty(capturedText))
        {
            Log($"Captured via UI Automation");
            return capturedText;
        }

        // Tier 2: Win32 Messages
        capturedText = CaptureViaWin32();
        if (!string.IsNullOrEmpty(capturedText))
        {
            Log($"Captured via Win32 Messages");
            return capturedText;
        }

        // Tier 3: Clipboard Fallback
        Log("Falling back to Clipboard injection.");
        capturedText = CaptureViaClipboard();
        if (!string.IsNullOrEmpty(capturedText))
        {
            Log($"Captured via Clipboard");
        }
        else
        {
            Log("Clipboard capture failed or was empty.");
        }

        return capturedText ?? string.Empty;
    }

    private string CaptureViaUIA()
    {
        try
        {
            var focusedElement = AutomationElement.FocusedElement;
            if (focusedElement != null && focusedElement.TryGetCurrentPattern(TextPattern.Pattern, out object patternObj))
            {
                var textPattern = (TextPattern)patternObj;
                var selections = textPattern.GetSelection();
                if (selections != null && selections.Length > 0)
                {
                    return selections[0].GetText(-1);
                }
            }
        }
        catch (Exception ex)
        {
            Log($"UIA Capture failed: {ex.Message}");
        }
        return string.Empty;
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
                if (focusHwnd != IntPtr.Zero)
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
        catch (Exception ex)
        {
            Log($"Win32 Capture failed: {ex.Message}");
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
            if (snapshot.HasContent)
            {
                Log($"Saved clipboard snapshot - Text: {snapshot.Text != null}, Files: {snapshot.Files?.Count ?? 0}, Image: {snapshot.Image != null}, HTML: {snapshot.Html != null}, RTF: {snapshot.Rtf != null}");
            }
            else
            {
                Log("Clipboard was empty.");
            }
        }
        catch (Exception ex)
        {
            Log($"Failed to save clipboard: {ex.Message}");
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
                Log("Restoring clipboard without history...");
                ClipboardHelper.RestoreWithoutHistory(snapshot);
            }
            else
            {
                Log("Clearing clipboard without history...");
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

    private void Log(string message)
    {
        DebugLog.Write($"[ClipboardService] {message}");
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
            catch (Exception ex)
            {
                Log($"Clipboard set text failed (attempt {i + 1}): {ex.Message}");
                Thread.Sleep(10);
            }
        }
    }
}
