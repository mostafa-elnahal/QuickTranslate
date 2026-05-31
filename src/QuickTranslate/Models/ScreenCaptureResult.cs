using System.Drawing;

namespace QuickTranslate.Models;

/// <summary>
/// Result of a screen region capture operation.
/// Contains the captured bitmap and the selection bounds in physical screen pixels.
/// </summary>
public record ScreenCaptureResult(Bitmap Bitmap, Rectangle SelectionBounds);
