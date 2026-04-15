using System;
using System.Collections.Specialized;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Media.Imaging;
using System.Windows.Input;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.Input.KeyboardAndMouse;

namespace QuickTranslate.Helpers;

/// <summary>
/// Provides clipboard preservation and restoration with history exclusion.
/// </summary>
internal static class ClipboardHelper
{

    /// <summary>
    /// Stores all supported clipboard formats for preservation.
    /// </summary>
    internal sealed class ClipboardSnapshot : IDisposable
    {
        public string? Text { get; set; }
        public string? Html { get; set; }
        public string? Rtf { get; set; }
        public StringCollection? Files { get; set; }
        public BitmapSource? Image { get; set; }

        public bool HasContent => Text != null || Html != null || Rtf != null || Files != null || Image != null;

        public void Dispose()
        {
            // BitmapSource doesn't need explicit disposal, but clear references
            Image = null;
            Files = null;
        }
    }

    /// <summary>
    /// Saves a snapshot of the current clipboard state.
    /// </summary>
    public static ClipboardSnapshot SaveSnapshot()
    {
        var snapshot = new ClipboardSnapshot();

        try
        {
            // Text (most common)
            if (Clipboard.ContainsText(TextDataFormat.UnicodeText))
                snapshot.Text = Clipboard.GetText(TextDataFormat.UnicodeText);
            else if (Clipboard.ContainsText())
                snapshot.Text = Clipboard.GetText();

            // HTML
            if (Clipboard.ContainsText(TextDataFormat.Html))
                snapshot.Html = Clipboard.GetText(TextDataFormat.Html);

            // RTF
            if (Clipboard.ContainsText(TextDataFormat.Rtf))
                snapshot.Rtf = Clipboard.GetText(TextDataFormat.Rtf);

            // Files (just paths, not actual file content)
            if (Clipboard.ContainsFileDropList())
                snapshot.Files = Clipboard.GetFileDropList();

            // Image
            if (Clipboard.ContainsImage())
                snapshot.Image = Clipboard.GetImage();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error saving clipboard snapshot: {ex.Message}");
        }

        return snapshot;
    }

    /// <summary>
    /// Restores a clipboard snapshot WITHOUT adding to clipboard history.
    /// </summary>
    public static void RestoreWithoutHistory(ClipboardSnapshot snapshot)
    {
        try
        {
            if (!PInvoke.OpenClipboard(new HWND(System.IntPtr.Zero)))
            {
                System.Diagnostics.Debug.WriteLine("Failed to open clipboard for restore, using fallback");
                RestoreFallback(snapshot);
                return;
            }

            try
            {
                PInvoke.EmptyClipboard();

                // Set exclusion flags FIRST
                SetClipboardExclusionFlags();

                // Restore text
                if (snapshot.Text != null)
                {
                    SetClipboardText(snapshot.Text);
                }
            }
            finally
            {
                PInvoke.CloseClipboard();
            }

            // Files and images are easier to restore via .NET after setting exclusion flags
            if (snapshot.Files != null && snapshot.Files.Count > 0)
            {
                try
                {
                    Clipboard.SetFileDropList(snapshot.Files);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Failed to restore files: {ex.Message}");
                }
            }
            else if (snapshot.Image != null)
            {
                try
                {
                    Clipboard.SetImage(snapshot.Image);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Failed to restore image: {ex.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Native restore failed: {ex.Message}, using fallback");
            RestoreFallback(snapshot);
        }
    }

    /// <summary>
    /// Clears the clipboard WITHOUT adding to clipboard history.
    /// </summary>
    public static void ClearWithoutHistory()
    {
        try
        {
            if (!PInvoke.OpenClipboard(new HWND(System.IntPtr.Zero)))
            {
                try
                {
                    PInvoke.EmptyClipboard();
                    SetClipboardExclusionFlags();
                }
                finally
                {
                    PInvoke.CloseClipboard();
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Clear without history failed: {ex.Message}");
        }
    }

    // Format names for clipboard history exclusion
    private const string EXCLUDE_FROM_HISTORY = "ExcludeClipboardContentFromMonitorProcessing";
    private const string CAN_INCLUDE_IN_HISTORY = "CanIncludeInClipboardHistory";

    /// <summary>
    /// Sets clipboard exclusion flags to prevent history entries.
    /// </summary>
    private static void SetClipboardExclusionFlags()
    {
        uint excludeFormat = PInvoke.RegisterClipboardFormat(EXCLUDE_FROM_HISTORY);
        uint canIncludeFormat = PInvoke.RegisterClipboardFormat(CAN_INCLUDE_IN_HISTORY);

        // ExcludeClipboardContentFromMonitorProcessing
        Windows.Win32.Foundation.HGLOBAL excludeData = PInvoke.GlobalAlloc(Windows.Win32.System.Memory.GLOBAL_ALLOC_FLAGS.GMEM_MOVEABLE, (nuint)sizeof(int));
        if (excludeData != IntPtr.Zero)
        {
            unsafe
            {
                void* excludePtr = PInvoke.GlobalLock(excludeData);
                if (excludePtr != null)
                {
                    Marshal.WriteInt32((IntPtr)excludePtr, 0);
                    PInvoke.GlobalUnlock(excludeData);
                }
            }
            PInvoke.SetClipboardData(excludeFormat, (Windows.Win32.Foundation.HANDLE)(IntPtr)excludeData);
        }

        // CanIncludeInClipboardHistory = 0 (false)
        Windows.Win32.Foundation.HGLOBAL canIncludeData = PInvoke.GlobalAlloc(Windows.Win32.System.Memory.GLOBAL_ALLOC_FLAGS.GMEM_MOVEABLE, (nuint)sizeof(int));
        if (canIncludeData != IntPtr.Zero)
        {
            unsafe
            {
                void* canIncludePtr = PInvoke.GlobalLock(canIncludeData);
                if (canIncludePtr != null)
                {
                    Marshal.WriteInt32((IntPtr)canIncludePtr, 0);
                    PInvoke.GlobalUnlock(canIncludeData);
                }
            }
            PInvoke.SetClipboardData(canIncludeFormat, (Windows.Win32.Foundation.HANDLE)(IntPtr)canIncludeData);
        }
    }

    /// <summary>
    /// Sets Unicode text to clipboard using Win32 API.
    /// </summary>
    private static void SetClipboardText(string text)
    {
        byte[] textBytes = Encoding.Unicode.GetBytes(text + "\0");
        Windows.Win32.Foundation.HGLOBAL hGlobal = PInvoke.GlobalAlloc(Windows.Win32.System.Memory.GLOBAL_ALLOC_FLAGS.GMEM_MOVEABLE, (nuint)textBytes.Length);

        if (hGlobal != IntPtr.Zero)
        {
            unsafe
            {
                void* pGlobal = PInvoke.GlobalLock(hGlobal);
                if (pGlobal != null)
                {
                    Marshal.Copy(textBytes, 0, (IntPtr)pGlobal, textBytes.Length);
                    PInvoke.GlobalUnlock(hGlobal);
                }
            }
            PInvoke.SetClipboardData(13u /* CF_UNICODETEXT */, (Windows.Win32.Foundation.HANDLE)(IntPtr)hGlobal);
        }
    }

    /// <summary>
    /// Fallback restoration using .NET Clipboard (may add to history).
    /// </summary>
    private static void RestoreFallback(ClipboardSnapshot snapshot)
    {
        try
        {
            if (snapshot.Files != null && snapshot.Files.Count > 0)
            {
                Clipboard.SetFileDropList(snapshot.Files);
            }
            else if (snapshot.Image != null)
            {
                Clipboard.SetImage(snapshot.Image);
            }
            else if (snapshot.Text != null)
            {
                Clipboard.SetText(snapshot.Text);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Fallback restore failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Safely clears the clipboard with retries.
    /// </summary>
    public static void ClearSafe()
    {
        for (int i = 0; i < 5; i++)
        {
            try
            {
                Clipboard.Clear();
                return;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Clipboard clear failed (attempt {i + 1}): {ex.Message}");
                System.Threading.Thread.Sleep(50);
            }
        }
    }

    /// <summary>
    /// Waits for text to appear in the clipboard and returns it, handling concurrency exceptions.
    /// </summary>
    public static string GetTextWithTimeout(int retryAttempts = 10, int retryInterval = 50)
    {
        for (int i = 0; i < retryAttempts; i++)
        {
            try
            {
                // Check if text exists
                if (Clipboard.ContainsText())
                {
                    return Clipboard.GetText();
                }
            }
            catch (Exception ex)
            {
                // Swallowing COMException (0x800401D0 - CLIPBRD_E_CANT_OPEN) and ExternalException
                // which happen when clipboard is locked by another process
                System.Diagnostics.Debug.WriteLine($"Clipboard read failed (attempt {i + 1}): {ex.Message}");
            }

            System.Threading.Thread.Sleep(retryInterval);
        }
        return string.Empty;
    }

    /// <summary>
    /// Sends Ctrl+C using SendInput API for reliable copying.
    /// </summary>
    public static void SendCopyCommand()
    {
        // 1. Prepare inputs

        // Release Shift and Alt (modifiers that might interfere with Ctrl+C)
        var inputShiftUp = new INPUT();
        inputShiftUp.type = INPUT_TYPE.INPUT_KEYBOARD;
        inputShiftUp.Anonymous.ki.wVk = Windows.Win32.UI.Input.KeyboardAndMouse.VIRTUAL_KEY.VK_SHIFT;
        inputShiftUp.Anonymous.ki.dwFlags = KEYBD_EVENT_FLAGS.KEYEVENTF_KEYUP;

        // Ctrl Down
        var inputCtrlDown = new INPUT();
        inputCtrlDown.type = INPUT_TYPE.INPUT_KEYBOARD;
        inputCtrlDown.Anonymous.ki.wVk = Windows.Win32.UI.Input.KeyboardAndMouse.VIRTUAL_KEY.VK_CONTROL;
        inputCtrlDown.Anonymous.ki.dwFlags = 0; // KeyDown

        // C Down
        var inputCDown = new INPUT();
        inputCDown.type = INPUT_TYPE.INPUT_KEYBOARD;
        inputCDown.Anonymous.ki.wVk = Windows.Win32.UI.Input.KeyboardAndMouse.VIRTUAL_KEY.VK_C;
        inputCDown.Anonymous.ki.dwFlags = 0; // KeyDown

        // C Up
        var inputCUp = new INPUT();
        inputCUp.type = INPUT_TYPE.INPUT_KEYBOARD;
        inputCUp.Anonymous.ki.wVk = Windows.Win32.UI.Input.KeyboardAndMouse.VIRTUAL_KEY.VK_C;
        inputCUp.Anonymous.ki.dwFlags = KEYBD_EVENT_FLAGS.KEYEVENTF_KEYUP;

        // Ctrl Up
        var inputCtrlUp = new INPUT();
        inputCtrlUp.type = INPUT_TYPE.INPUT_KEYBOARD;
        inputCtrlUp.Anonymous.ki.wVk = Windows.Win32.UI.Input.KeyboardAndMouse.VIRTUAL_KEY.VK_CONTROL;
        inputCtrlUp.Anonymous.ki.dwFlags = KEYBD_EVENT_FLAGS.KEYEVENTF_KEYUP;

        // 2. Send inputs
        // Release modifiers first in a separate batch to ensure state is clean
        Span<INPUT> inputsRelease = stackalloc INPUT[] { inputShiftUp };

        Span<INPUT> inputsDown = stackalloc INPUT[] { inputCtrlDown, inputCDown };
        Span<INPUT> inputsUp = stackalloc INPUT[] { inputCUp, inputCtrlUp };

        unsafe
        {
            // Release modifiers
            fixed (INPUT* pInputsRelease = inputsRelease)
            {
                PInvoke.SendInput((uint)inputsRelease.Length, pInputsRelease, Marshal.SizeOf<INPUT>());
            }

            // Short delay
            System.Threading.Thread.Sleep(10);

            // Press keys
            fixed (INPUT* pInputsDown = inputsDown)
            {
                uint successful = PInvoke.SendInput((uint)inputsDown.Length, pInputsDown, Marshal.SizeOf<INPUT>());
                if (successful != inputsDown.Length)
                {
                    System.Diagnostics.Debug.WriteLine($"SendInput Down failed. Sent {successful}/{inputsDown.Length}");
                }
            }

            // Small delay to ensure apps register the key press
            System.Threading.Thread.Sleep(50);

            // Release keys
            fixed (INPUT* pInputsUp = inputsUp)
            {
                uint successful = PInvoke.SendInput((uint)inputsUp.Length, pInputsUp, Marshal.SizeOf<INPUT>());
                if (successful != inputsUp.Length)
                {
                    System.Diagnostics.Debug.WriteLine($"SendInput Up failed. Sent {successful}/{inputsUp.Length}");
                }
            }
        }
    }
}

