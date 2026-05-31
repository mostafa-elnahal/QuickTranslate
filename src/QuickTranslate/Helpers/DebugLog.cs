using System;
using System.IO;

namespace QuickTranslate.Helpers;

internal static class DebugLog
{
    private static readonly string LogPath;
    private static readonly object Lock = new();

    static DebugLog()
    {
        LogPath = Path.Combine(Path.GetTempPath(), "QuickTranslate", "qt_debug.log");
        Directory.CreateDirectory(Path.GetDirectoryName(LogPath)!);
    }

    public static void Write(string message)
    {
        lock (Lock)
        {
            try
            {
                File.AppendAllText(LogPath, $"[{DateTime.Now:HH:mm:ss.fff}] {message}{Environment.NewLine}");
            }
            catch
            {
            }
        }
    }
}
