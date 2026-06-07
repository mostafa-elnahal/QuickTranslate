using System;
using System.Drawing;
using System.Drawing.Imaging;
using PixelFormat = System.Drawing.Imaging.PixelFormat;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using QuickTranslate.Models;
using Point = System.Windows.Point;

namespace QuickTranslate.Views;

/// <summary>
/// A fullscreen overlay that captures the screen, freezes it, and allows the user
/// to draw a rectangle to select a screen region for OCR.
/// Follows Text Grab's approach for DPI-aware screen capture and selection.
/// After selection, the overlay stays visible until CompleteCapture is called.
/// </summary>
public partial class ScreenSelectionOverlay : Window
{
    private Point _clickedPoint;
    private bool _isSelecting;
    private TaskCompletionSource<ScreenCaptureResult?>? _tcs;
    private CancellationToken? _cancellationToken;
    private Bitmap? _screenBitmap;
    private DpiScale _dpiScale;
    private Point _absoluteWindowPosition;
    private Point _captureOrigin;
    private ScreenCaptureResult? _capturedResult;

    /// <summary>Fired when the user has drawn a valid selection rectangle.</summary>
    public event Action<System.Drawing.Bitmap, System.Drawing.Rectangle>? RegionCaptured;

    public ScreenSelectionOverlay()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Shows the overlay and waits for the user to select a region.
    /// The overlay stays visible after selection until CompleteCapture is called.
    /// </summary>
    public Task<ScreenCaptureResult?> CaptureAsync(CancellationToken cancellationToken = default)
    {
        _cancellationToken = cancellationToken;
        _tcs = new TaskCompletionSource<ScreenCaptureResult?>();

        // Register cancellation
        cancellationToken.Register(() =>
        {
            Dispatcher.Invoke(() =>
            {
                _tcs?.TrySetResult(null);
                CleanupResources();
                Close();
                Cursor = Cursors.Arrow;
                UnClipCursor();
            });
        });

        // Wire up events
        Closing += (_, _) => CleanupResources();

        // Capture the entire virtual screen BEFORE showing the overlay window,
        // so that any popup menus (right-click, dropdown) in the foreground
        // app are still visible — they would be dismissed once this window
        // steals focus via Show/Activate below.
        _screenBitmap = CaptureVirtualScreen();

        // Show window maximized on the current screen
        WindowState = WindowState.Maximized;
        Show();
        Activate();

        // Get DPI and absolute position after window is shown
        _dpiScale = VisualTreeHelper.GetDpi(this);
        _absoluteWindowPosition = GetAbsolutePosition();

        // Set up the clipping geometry for the full window
        FullWindow.Rect = new Rect(0, 0, Width, Height);

        // Display the pre-captured screenshot as the frozen background
        BackgroundImage.Source = BitmapToImageSource(_screenBitmap);

        // Set cursor to crosshair
        Cursor = Cursors.Cross;

        return _tcs.Task;
    }

    /// <summary>
    /// Completes the capture session: closes the overlay and returns the result.
    /// </summary>
    public void CompleteCapture()
    {
        var result = _capturedResult;
        CleanupResources();
        _tcs?.TrySetResult(result);
        Close();
        Cursor = Cursors.Arrow;
        UnClipCursor();
    }

    /// <summary>
    /// Cancels the capture session without a result.
    /// </summary>
    public void CancelCapture()
    {
        _capturedResult = null;
        CleanupResources();
        _tcs?.TrySetResult(null);
        Close();
        Cursor = Cursors.Arrow;
        UnClipCursor();
    }

    /// <summary>
    /// Captures the screen region behind this window and sets it as the background image.
    /// Follows Text Grab's ImageMethods.GetWindowBoundsImage pattern.
    /// </summary>
    private void SetImageToBackground()
    {
        // Dispose old image if it exists
        DisposeBitmapSource();

        // Capture the screen using GDI+ (same as Text Grab)
        _screenBitmap = GetWindowsBoundsBitmap();
        BackgroundImage.Source = BitmapToImageSource(_screenBitmap);
    }

    /// <summary>
    /// Captures the screen region under this window using native GDI BitBlt.
    /// Uses CaptureBlt flag to capture layered/transparent windows correctly.
    /// Handles DPI scaling properly.
    /// </summary>
    private Bitmap GetWindowsBoundsBitmap()
    {
        int windowWidth = (int)(ActualWidth * _dpiScale.DpiScaleX);
        int windowHeight = (int)(ActualHeight * _dpiScale.DpiScaleY);

        int correctedLeft = (int)_absoluteWindowPosition.X;
        int correctedTop = (int)_absoluteWindowPosition.Y;

        IntPtr hdcSrc = NativeMethods.GetWindowDC(IntPtr.Zero);
        IntPtr hdcDest = NativeMethods.CreateCompatibleDC(hdcSrc);
        IntPtr hBitmap = NativeMethods.CreateCompatibleBitmap(hdcSrc, windowWidth, windowHeight);
        IntPtr hOld = NativeMethods.SelectObject(hdcDest, hBitmap);

        NativeMethods.BitBlt(hdcDest, 0, 0, windowWidth, windowHeight, hdcSrc,
            correctedLeft, correctedTop, NativeMethods.SRCCOPY_CAPTUREBLT);

        NativeMethods.SelectObject(hdcDest, hOld);
        NativeMethods.DeleteDC(hdcDest);
        NativeMethods.ReleaseDC(IntPtr.Zero, hdcSrc);

        Bitmap bmp = System.Drawing.Image.FromHbitmap(hBitmap);
        NativeMethods.DeleteObject(hBitmap);

        return bmp;
    }

    /// <summary>
    /// Captures the entire virtual desktop (all monitors) silently, without
    /// requiring any window to be shown. Used before the overlay window is
    /// displayed so that transient UI (popup menus, dropdowns) is still visible.
    /// </summary>
    private Bitmap CaptureVirtualScreen()
    {
        int screenLeft = NativeMethods.GetSystemMetrics(NativeMethods.SM_XVIRTUALSCREEN);
        int screenTop = NativeMethods.GetSystemMetrics(NativeMethods.SM_YVIRTUALSCREEN);
        int screenWidth = NativeMethods.GetSystemMetrics(NativeMethods.SM_CXVIRTUALSCREEN);
        int screenHeight = NativeMethods.GetSystemMetrics(NativeMethods.SM_CYVIRTUALSCREEN);

        _captureOrigin = new Point(screenLeft, screenTop);

        IntPtr hdcSrc = NativeMethods.GetWindowDC(IntPtr.Zero);
        IntPtr hdcDest = NativeMethods.CreateCompatibleDC(hdcSrc);
        IntPtr hBitmap = NativeMethods.CreateCompatibleBitmap(hdcSrc, screenWidth, screenHeight);
        IntPtr hOld = NativeMethods.SelectObject(hdcDest, hBitmap);

        NativeMethods.BitBlt(hdcDest, 0, 0, screenWidth, screenHeight, hdcSrc,
            screenLeft, screenTop, NativeMethods.SRCCOPY_CAPTUREBLT);

        NativeMethods.SelectObject(hdcDest, hOld);
        NativeMethods.DeleteDC(hdcDest);
        NativeMethods.ReleaseDC(IntPtr.Zero, hdcSrc);

        Bitmap bmp = System.Drawing.Image.FromHbitmap(hBitmap);
        NativeMethods.DeleteObject(hBitmap);

        return bmp;
    }

    /// <summary>
    /// Converts a System.Drawing.Bitmap to a WPF BitmapSource using HBitmap interop.
    /// This avoids the memory overhead of serializing the entire bitmap to a BMP MemoryStream.
    /// </summary>
    private static BitmapSource BitmapToImageSource(Bitmap bitmap)
    {
        IntPtr hBitmap = bitmap.GetHbitmap();
        try
        {
            var source = System.Windows.Interop.Imaging.CreateBitmapSourceFromHBitmap(
                hBitmap,
                IntPtr.Zero,
                Int32Rect.Empty,
                BitmapSizeOptions.FromEmptyOptions());
            source.Freeze();
            return source;
        }
        finally
        {
            NativeMethods.DeleteObject(hBitmap);
        }
    }

    /// <summary>
    /// Gets the absolute screen position of this window.
    /// Handles maximized windows correctly using MonitorFromWindow.
    /// </summary>
    private Point GetAbsolutePosition()
    {
        if (WindowState != WindowState.Maximized)
            return new Point(Left, Top);

        // For maximized windows, use MonitorFromWindow to get the correct screen bounds
        var helper = new WindowInteropHelper(this);
        IntPtr hmonitor = NativeMethods.MonitorFromWindow(helper.Handle, NativeMethods.MONITOR_DEFAULTTONEAREST);

        var info = new NativeMethods.MONITORINFOEX();
        info.cbSize = Marshal.SizeOf<NativeMethods.MONITORINFOEX>();
        NativeMethods.GetMonitorInfo(hmonitor, ref info);

        return new Point(info.rcMonitor.left, info.rcMonitor.top);
    }

    private void RegionClickCanvas_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.RightButton == MouseButtonState.Pressed) return;
        if (_cancellationToken?.IsCancellationRequested == true) return;

        _clickedPoint = e.GetPosition(RegionClickCanvas);
        _isSelecting = true;

        SelectionBorder.Width = 0;
        SelectionBorder.Height = 0;
        Canvas.SetLeft(SelectionBorder, _clickedPoint.X);
        Canvas.SetTop(SelectionBorder, _clickedPoint.Y);

        if (!RegionClickCanvas.Children.Contains(SelectionBorder))
            RegionClickCanvas.Children.Add(SelectionBorder);

        SelectionBorder.Visibility = Visibility.Visible;
        InstructionText.Visibility = Visibility.Collapsed;

        // Clip cursor to screen bounds (prevents cursor from escaping during drag)
        ClipCursor();

        e.Handled = true;
        RegionClickCanvas.CaptureMouse();
    }

    private void RegionClickCanvas_MouseMove(object sender, MouseEventArgs e)
    {
        if (!_isSelecting) return;

        Point movingPoint = e.GetPosition(RegionClickCanvas);
        UpdateRectangleSelection(movingPoint);
    }

    private void RegionClickCanvas_MouseUp(object sender, MouseButtonEventArgs e)
    {
        if (!_isSelecting) return;
        _isSelecting = false;

        RegionClickCanvas.ReleaseMouseCapture();
        UnClipCursor();

        Point endPoint = e.GetPosition(RegionClickCanvas);
        Rect selectionRect = new(_clickedPoint, endPoint);

        // Minimum size check (10x10 pixels)
        if (selectionRect.Width > 10 && selectionRect.Height > 10)
        {
            // Convert WPF logical coordinates to absolute physical screen pixels
            Point topLeftPhysical = RegionClickCanvas.PointToScreen(selectionRect.TopLeft);
            Point bottomRightPhysical = RegionClickCanvas.PointToScreen(selectionRect.BottomRight);

            // Calculate width and height natively in pixels
            double width = Math.Max(1, Math.Round(bottomRightPhysical.X - topLeftPhysical.X));
            double height = Math.Max(1, Math.Round(bottomRightPhysical.Y - topLeftPhysical.Y));

            // Shift into the coordinate space of the captured full-screen bitmap
            // (The bitmap covers the entire virtual screen starting at _captureOrigin)
            double xOffset = topLeftPhysical.X - _captureOrigin.X;
            double yOffset = topLeftPhysical.Y - _captureOrigin.Y;

            // Create absolute screen rectangle
            Rect absoluteRect = new(xOffset, yOffset, width, height);

            var screenshot = CropScreenRegion(absoluteRect);

            // Physical screen selection rectangle (for toolbar positioning)
            var selectionPhysical = new Rectangle(
                (int)topLeftPhysical.X, (int)topLeftPhysical.Y,
                (int)width, (int)height);

            _capturedResult = new ScreenCaptureResult(screenshot, selectionPhysical);

            // Keep overlay visible and fire event so toolbar can appear
            RegionCaptured?.Invoke(screenshot, selectionPhysical);
        }
        else
        {
            CancelCapture();
        }
    }

    /// <summary>
    /// Updates the rectangle selection visually during mouse drag.
    /// </summary>
    private void UpdateRectangleSelection(Point movingPoint)
    {
        double left = Math.Min(_clickedPoint.X, movingPoint.X);
        double top = Math.Min(_clickedPoint.Y, movingPoint.Y);
        double width = Math.Abs(movingPoint.X - _clickedPoint.X);
        double height = Math.Abs(movingPoint.Y - _clickedPoint.Y);

        Rect rect = new(left, top, width, height);

        SelectionBorder.Width = Math.Max(0, rect.Width);
        SelectionBorder.Height = Math.Max(0, rect.Height);
        Canvas.SetLeft(SelectionBorder, rect.Left);
        Canvas.SetTop(SelectionBorder, rect.Top);

        // Update clipping geometry for "cutout" effect
        ClippingGeometry.Rect = rect;
    }

    /// <summary>
    /// Crops the screen region from the captured bitmap.
    /// </summary>
    private Bitmap CropScreenRegion(Rect absoluteRect)
    {
        if (_screenBitmap == null)
            throw new InvalidOperationException("No screen capture available.");

        // Clamp to bitmap bounds
        int x = Math.Max(0, (int)absoluteRect.X);
        int y = Math.Max(0, (int)absoluteRect.Y);
        int width = Math.Max(1, Math.Min((int)absoluteRect.Width, _screenBitmap.Width - x));
        int height = Math.Max(1, Math.Min((int)absoluteRect.Height, _screenBitmap.Height - y));

        // Crop the bitmap
        Rectangle cropRect = new(x, y, width, height);
        return _screenBitmap.Clone(cropRect, _screenBitmap.PixelFormat);
    }

    /// <summary>
    /// Clips the cursor to the window bounds during drag.
    /// </summary>
    private void ClipCursor()
    {
        var helper = new WindowInteropHelper(this);
        var rect = new NativeMethods.RECT
        {
            left = (int)_absoluteWindowPosition.X,
            top = (int)_absoluteWindowPosition.Y,
            right = (int)(_absoluteWindowPosition.X + ActualWidth * _dpiScale.DpiScaleX),
            bottom = (int)(_absoluteWindowPosition.Y + ActualHeight * _dpiScale.DpiScaleY)
        };
        NativeMethods.ClipCursor(ref rect);
    }

    /// <summary>
    /// Releases the cursor clip.
    /// </summary>
    private void UnClipCursor()
    {
        NativeMethods.ClipCursor(IntPtr.Zero);
    }

    private void CleanupResources()
    {
        _screenBitmap?.Dispose();
        _screenBitmap = null;
        DisposeBitmapSource();
    }

    private void DisposeBitmapSource()
    {
        if (BackgroundImage.Source is BitmapSource)
        {
            BackgroundImage.Source = null;
            BackgroundImage.UpdateLayout();
        }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            _isSelecting = false;
            RegionClickCanvas?.ReleaseMouseCapture();
            _capturedResult = null;
            CancelCapture();
        }
    }

    #region Win32 Interop

    private static class NativeMethods
    {
        public const uint MONITOR_DEFAULTTONEAREST = 2;

        // Virtual desktop bounds for pre-capture (before overlay is shown)
        public const int SM_XVIRTUALSCREEN = 76;
        public const int SM_YVIRTUALSCREEN = 77;
        public const int SM_CXVIRTUALSCREEN = 78;
        public const int SM_CYVIRTUALSCREEN = 79;

        [DllImport("user32.dll")]
        public static extern int GetSystemMetrics(int nIndex);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool ClipCursor(ref RECT lpRect);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool ClipCursor(IntPtr lpRect);

        [DllImport("user32.dll")]
        public static extern IntPtr MonitorFromWindow(IntPtr hWnd, uint dwFlags);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFOEX lpmi);

        [DllImport("user32.dll")]
        public static extern IntPtr GetWindowDC(IntPtr hWnd);

        [DllImport("user32.dll")]
        public static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);

        [DllImport("gdi32.dll")]
        public static extern IntPtr CreateCompatibleDC(IntPtr hDC);

        [DllImport("gdi32.dll")]
        public static extern IntPtr CreateCompatibleBitmap(IntPtr hDC, int nWidth, int nHeight);

        [DllImport("gdi32.dll")]
        public static extern IntPtr SelectObject(IntPtr hDC, IntPtr hObject);

        [DllImport("gdi32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool BitBlt(IntPtr hdc, int nXDest, int nYDest, int nWidth, int nHeight,
            IntPtr hdcSrc, int nXSrc, int nYSrc, uint dwRop);

        [DllImport("gdi32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool DeleteDC(IntPtr hDC);

        [DllImport("gdi32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool DeleteObject(IntPtr hObject);

        public const uint SRCCOPY_CAPTUREBLT = 0xCC0020 | 0x40000000;

        [StructLayout(LayoutKind.Sequential)]
        public struct RECT
        {
            public int left;
            public int top;
            public int right;
            public int bottom;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        public struct MONITORINFOEX
        {
            public int cbSize;
            public RECT rcMonitor;
            public RECT rcWork;
            public uint dwFlags;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
            public string szDevice;
        }
    }

    #endregion
}
