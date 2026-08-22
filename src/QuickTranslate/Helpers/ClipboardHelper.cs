using System;
using System.Collections.Specialized;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media.Imaging;
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
    /// Unique tag set in dwExtraInfo on all injected keystrokes so hooks
    /// can distinguish our SendInput calls from real user input.
    /// ASCII "QTRN" = QuickTranslate Remote Notification.
    /// </summary>
    internal const nuint QTAG_EXTRA_INFO = 0x5154524E;

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

    private const string EXCLUDE_FROM_HISTORY = "ExcludeClipboardContentFromMonitorProcessing";
    private const string CAN_INCLUDE_IN_HISTORY = "CanIncludeInClipboardHistory";
    private const string CAN_UPLOAD_TO_CLOUD = "CanUploadToCloudClipboard";

    /// <summary>
    /// Restores a clipboard snapshot WITHOUT adding to clipboard history.
    /// </summary>
    public static void RestoreWithoutHistory(ClipboardSnapshot snapshot)
    {
        try
        {
            var dataObject = new DataObject();

            // Set content if present
            if (snapshot.Text != null)
                dataObject.SetText(snapshot.Text);
            if (snapshot.Html != null)
                dataObject.SetData(DataFormats.Html, snapshot.Html);
            if (snapshot.Rtf != null)
                dataObject.SetData(DataFormats.Rtf, snapshot.Rtf);
            if (snapshot.Files != null && snapshot.Files.Count > 0)
                dataObject.SetFileDropList(snapshot.Files);
            if (snapshot.Image != null)
                dataObject.SetImage(snapshot.Image);

            // Add exclusion flags (DWORD = 4 bytes)
            byte[] zeroDword = BitConverter.GetBytes(0);
            dataObject.SetData(EXCLUDE_FROM_HISTORY, zeroDword);
            dataObject.SetData(CAN_INCLUDE_IN_HISTORY, zeroDword);
            dataObject.SetData(CAN_UPLOAD_TO_CLOUD, zeroDword);

            SetDataObjectWithRetries(dataObject, true);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Restore without history failed: {ex.Message}, trying fallback");
            RestoreFallback(snapshot);
        }
    }

    /// <summary>
    /// Clears the clipboard WITHOUT adding to clipboard history.
    /// </summary>
    public static void ClearWithoutHistory()
    {
        ClearSafe();
    }

    private static void SetDataObjectWithRetries(DataObject dataObject, bool copy)
    {
        for (int i = 0; i < 10; i++)
        {
            try
            {
                Clipboard.SetDataObject(dataObject, copy);
                return;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"SetDataObject failed (attempt {i + 1}): {ex.Message}");
                System.Threading.Thread.Sleep(10);
            }
        }
        throw new System.Runtime.InteropServices.ExternalException("Failed to set data object after retries.");
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
                System.Threading.Thread.Sleep(10);
            }
        }
    }

    /// <summary>
    /// Waits for text to appear in the clipboard and returns it, handling concurrency exceptions.
    /// </summary>
    public static string GetTextWithTimeout(int retryAttempts = 20, int retryInterval = 10)
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
    /// All injected inputs carry QTAG_EXTRA_INFO so hooks can ignore them.
    /// </summary>
    public static void SendCopyCommand()
    {
        // 1. Prepare inputs

        // Release Shift and Alt (modifiers that might interfere with Ctrl+C)
        var inputShiftUp = new INPUT();
        inputShiftUp.type = INPUT_TYPE.INPUT_KEYBOARD;
        inputShiftUp.Anonymous.ki.wVk = Windows.Win32.UI.Input.KeyboardAndMouse.VIRTUAL_KEY.VK_SHIFT;
        inputShiftUp.Anonymous.ki.dwFlags = KEYBD_EVENT_FLAGS.KEYEVENTF_KEYUP;
        inputShiftUp.Anonymous.ki.dwExtraInfo = QTAG_EXTRA_INFO;

        var inputAltUp = new INPUT();
        inputAltUp.type = INPUT_TYPE.INPUT_KEYBOARD;
        inputAltUp.Anonymous.ki.wVk = Windows.Win32.UI.Input.KeyboardAndMouse.VIRTUAL_KEY.VK_MENU;
        inputAltUp.Anonymous.ki.dwFlags = KEYBD_EVENT_FLAGS.KEYEVENTF_KEYUP;
        inputAltUp.Anonymous.ki.dwExtraInfo = QTAG_EXTRA_INFO;

        // Ctrl Down
        var inputCtrlDown = new INPUT();
        inputCtrlDown.type = INPUT_TYPE.INPUT_KEYBOARD;
        inputCtrlDown.Anonymous.ki.wVk = Windows.Win32.UI.Input.KeyboardAndMouse.VIRTUAL_KEY.VK_CONTROL;
        inputCtrlDown.Anonymous.ki.dwFlags = 0; // KeyDown
        inputCtrlDown.Anonymous.ki.dwExtraInfo = QTAG_EXTRA_INFO;

        // C Down
        var inputCDown = new INPUT();
        inputCDown.type = INPUT_TYPE.INPUT_KEYBOARD;
        inputCDown.Anonymous.ki.wVk = Windows.Win32.UI.Input.KeyboardAndMouse.VIRTUAL_KEY.VK_C;
        inputCDown.Anonymous.ki.dwFlags = 0; // KeyDown
        inputCDown.Anonymous.ki.dwExtraInfo = QTAG_EXTRA_INFO;

        // C Up
        var inputCUp = new INPUT();
        inputCUp.type = INPUT_TYPE.INPUT_KEYBOARD;
        inputCUp.Anonymous.ki.wVk = Windows.Win32.UI.Input.KeyboardAndMouse.VIRTUAL_KEY.VK_C;
        inputCUp.Anonymous.ki.dwFlags = KEYBD_EVENT_FLAGS.KEYEVENTF_KEYUP;
        inputCUp.Anonymous.ki.dwExtraInfo = QTAG_EXTRA_INFO;

        // Ctrl Up
        var inputCtrlUp = new INPUT();
        inputCtrlUp.type = INPUT_TYPE.INPUT_KEYBOARD;
        inputCtrlUp.Anonymous.ki.wVk = Windows.Win32.UI.Input.KeyboardAndMouse.VIRTUAL_KEY.VK_CONTROL;
        inputCtrlUp.Anonymous.ki.dwFlags = KEYBD_EVENT_FLAGS.KEYEVENTF_KEYUP;
        inputCtrlUp.Anonymous.ki.dwExtraInfo = QTAG_EXTRA_INFO;

        // 2. Send inputs
        // Release modifiers first in a separate batch to ensure state is clean
        Span<INPUT> inputsRelease = stackalloc INPUT[] { inputShiftUp, inputAltUp };

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
            System.Threading.Thread.Sleep(2);

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
            System.Threading.Thread.Sleep(10);

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

