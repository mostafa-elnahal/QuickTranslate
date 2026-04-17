using System;
using System.IO;
using System.Windows;

namespace QuickTranslate.Helpers;

public static class CrashLogger
{
    private const string CrashLogFile = "crash_log.txt";

    public static void LogException(Exception ex)
    {
        try
        {
            string errorMessage = $"Exception detected: {ex.Message}\n\nStack Trace:\n{ex.StackTrace}";
            if (ex.InnerException != null)
            {
                errorMessage += $"\n\nInner Exception: {ex.InnerException.Message}";
            }

            File.WriteAllText(CrashLogFile, errorMessage);
            
            MessageBox.Show(
                $"Application Crashed. Log saved to {CrashLogFile}.\n{ex.Message}", 
                "QuickTranslate Error", 
                MessageBoxButton.OK, 
                MessageBoxImage.Error);
        }
        catch
        {
            // Fallback if logging fails
            MessageBox.Show($"Multiple errors detected. Application will close.\n{ex.Message}", "QuickTranslate Fatal Error");
        }
    }
}
