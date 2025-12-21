using System;
using System.Windows;
using System.Runtime.InteropServices;
using Windows.Win32;

namespace QuickTranslate.Services;

public class WindowPositioningService : IWindowPositioningService
{
    public void PositionNearCursor(Window window)
    {
        if (PInvoke.GetCursorPos(out System.Drawing.Point cursorPos))
        {
            // Get DPI scaling factors
            var presentationSource = PresentationSource.FromVisual(window);
            double dpiScaleX = 1.0;
            double dpiScaleY = 1.0;

            if (presentationSource?.CompositionTarget != null)
            {
                dpiScaleX = presentationSource.CompositionTarget.TransformToDevice.M11;
                dpiScaleY = presentationSource.CompositionTarget.TransformToDevice.M22;
            }

            // Convert cursor position from pixels to DIPs
            double cursorX = cursorPos.X / dpiScaleX;
            double cursorY = cursorPos.Y / dpiScaleY;

            // Get screen info (in pixels) and convert to DIPs
            var screen = System.Windows.Forms.Screen.FromPoint(
                new System.Drawing.Point(cursorPos.X, cursorPos.Y));

            double screenLeft = screen.WorkingArea.Left / dpiScaleX;
            double screenTop = screen.WorkingArea.Top / dpiScaleY;
            double screenRight = screen.WorkingArea.Right / dpiScaleX;
            double screenBottom = screen.WorkingArea.Bottom / dpiScaleY;

            // Determine window dimensions (use Actual if available, otherwise default/estimated)
            // If window is currently hidden, Actual sizes might be 0, so fallback to Width/Height or reasonable defaults
            double windowWidth = window.ActualWidth > 0 ? window.ActualWidth : (double.IsNaN(window.Width) ? 400 : window.Width);
            double windowHeight = window.ActualHeight > 0 ? window.ActualHeight : (double.IsNaN(window.Height) ? 300 : window.Height);

            // Default position: Bottom-Right of cursor
            // Add small offset to not cover the exact click point
            double left = cursorX + 10;
            double top = cursorY + 10;

            // Smart positioning logic (Menu behavior)

            // Check Right boundary
            if (left + windowWidth > screenRight)
            {
                // Flip to Left side of cursor
                left = cursorX - windowWidth - 10;
            }

            // Check Bottom boundary
            if (top + windowHeight > screenBottom)
            {
                // Flip to Top side of cursor
                top = cursorY - windowHeight - 10;
            }

            // Final safety clamp to ensuring it's always on screen
            // (e.g. if it's too big to fit on either side, prioritize Top/Left alignment)
            if (left < screenLeft) left = screenLeft;
            if (left + windowWidth > screenRight) left = screenRight - windowWidth;

            if (top < screenTop) top = screenTop;
            if (top + windowHeight > screenBottom) top = screenBottom - windowHeight;

            window.Left = left;
            window.Top = top;
        }
    }
}
