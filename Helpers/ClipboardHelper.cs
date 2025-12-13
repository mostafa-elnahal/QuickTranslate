using System;
using System.Collections.Specialized;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Media.Imaging;
using QuickTranslate.Interop;

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
            if (!NativeMethods.OpenClipboard(IntPtr.Zero))
            {
                System.Diagnostics.Debug.WriteLine("Failed to open clipboard for restore, using fallback");
                RestoreFallback(snapshot);
                return;
            }

            try
            {
                NativeMethods.EmptyClipboard();

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
                NativeMethods.CloseClipboard();
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
            if (NativeMethods.OpenClipboard(IntPtr.Zero))
            {
                try
                {
                    NativeMethods.EmptyClipboard();
                    SetClipboardExclusionFlags();
                }
                finally
                {
                    NativeMethods.CloseClipboard();
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Clear without history failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Sets clipboard exclusion flags to prevent history entries.
    /// </summary>
    private static void SetClipboardExclusionFlags()
    {
        uint excludeFormat = NativeMethods.RegisterClipboardFormat(NativeMethods.EXCLUDE_FROM_HISTORY);
        uint canIncludeFormat = NativeMethods.RegisterClipboardFormat(NativeMethods.CAN_INCLUDE_IN_HISTORY);

        // ExcludeClipboardContentFromMonitorProcessing
        IntPtr excludeData = NativeMethods.GlobalAlloc(NativeMethods.GMEM_MOVEABLE, (UIntPtr)sizeof(int));
        if (excludeData != IntPtr.Zero)
        {
            IntPtr excludePtr = NativeMethods.GlobalLock(excludeData);
            if (excludePtr != IntPtr.Zero)
            {
                Marshal.WriteInt32(excludePtr, 0);
                NativeMethods.GlobalUnlock(excludeData);
            }
            NativeMethods.SetClipboardData(excludeFormat, excludeData);
        }

        // CanIncludeInClipboardHistory = 0 (false)
        IntPtr canIncludeData = NativeMethods.GlobalAlloc(NativeMethods.GMEM_MOVEABLE, (UIntPtr)sizeof(int));
        if (canIncludeData != IntPtr.Zero)
        {
            IntPtr canIncludePtr = NativeMethods.GlobalLock(canIncludeData);
            if (canIncludePtr != IntPtr.Zero)
            {
                Marshal.WriteInt32(canIncludePtr, 0);
                NativeMethods.GlobalUnlock(canIncludeData);
            }
            NativeMethods.SetClipboardData(canIncludeFormat, canIncludeData);
        }
    }

    /// <summary>
    /// Sets Unicode text to clipboard using Win32 API.
    /// </summary>
    private static void SetClipboardText(string text)
    {
        byte[] textBytes = Encoding.Unicode.GetBytes(text + "\0");
        IntPtr hGlobal = NativeMethods.GlobalAlloc(NativeMethods.GMEM_MOVEABLE, (UIntPtr)textBytes.Length);

        if (hGlobal != IntPtr.Zero)
        {
            IntPtr pGlobal = NativeMethods.GlobalLock(hGlobal);
            if (pGlobal != IntPtr.Zero)
            {
                Marshal.Copy(textBytes, 0, pGlobal, textBytes.Length);
                NativeMethods.GlobalUnlock(hGlobal);
            }
            NativeMethods.SetClipboardData(NativeMethods.CF_UNICODETEXT, hGlobal);
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
                System.Diagnostics.Debug.WriteLine($"Clipboard clear failed (attempt {i+1}): {ex.Message}");
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
                System.Diagnostics.Debug.WriteLine($"Clipboard read failed (attempt {i+1}): {ex.Message}");
            }

            System.Threading.Thread.Sleep(retryInterval);
        }
        return string.Empty;
    }
}
