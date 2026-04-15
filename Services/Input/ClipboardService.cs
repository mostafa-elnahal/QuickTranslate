using System;
using System.Threading;
using System.Windows.Forms;
using QuickTranslate.Helpers;

namespace QuickTranslate.Services;

public class ClipboardService : IClipboardService
{
    public string CaptureSelection()
    {
        Log("Starting Clipboard Strategy...");

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

        Thread.Sleep(50);

        // 2. Clear & Copy selection
        ClipboardHelper.ClearSafe();
        ClipboardHelper.SendCopyCommand();

        // 3. Wait for clipboard to populate
        // GetTextWithTimeout handles retries and exceptions internally
        string capturedText = ClipboardHelper.GetTextWithTimeout();
        if (!string.IsNullOrEmpty(capturedText))
        {
            Log($"Captured: {capturedText}");
        }

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

        if (string.IsNullOrEmpty(capturedText))
        {
            Log("Clipboard capture failed or was empty.");
        }

        return capturedText;
    }

    private void Log(string message)
    {
        try
        {
            System.IO.File.AppendAllText("debug.log", $"{DateTime.Now}: {message}{Environment.NewLine}");
        }
        catch { }
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
                Thread.Sleep(50);
            }
        }
    }
}
