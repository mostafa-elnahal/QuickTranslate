using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using QuickTranslate.Models;

namespace QuickTranslate.Services.Input;

/// <summary>
/// OCR service implementation using the built-in Windows.Media.Ocr API.
/// Requires Windows 10 1809+ and package identity (MSIX).
/// Follows Text Grab's approach for image scaling and conversion.
/// </summary>
public class WindowsMediaOcrService : IOcrService
{
    private readonly IScreenCaptureService _screenCaptureService;
    private string _currentLanguageCode = "en-US";

    public WindowsMediaOcrService(IScreenCaptureService screenCaptureService)
    {
        _screenCaptureService = screenCaptureService;
    }

    public string CurrentLanguageCode
    {
        get => _currentLanguageCode;
        set
        {
            try
            {
                var language = new Windows.Globalization.Language(value);
                if (Windows.Media.Ocr.OcrEngine.IsLanguageSupported(language))
                {
                    _currentLanguageCode = value;
                }
            }
            catch
            {
                // Language not supported, keep current
            }
        }
    }

    public IReadOnlyList<OcrLanguage> GetAvailableLanguages()
    {
        return Windows.Media.Ocr.OcrEngine.AvailableRecognizerLanguages
            .Select(l => new OcrLanguage(l.LanguageTag, l.DisplayName))
            .ToList();
    }

    public async Task<string?> CaptureAndRecognizeAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var selectedRegion = await _screenCaptureService.CaptureRegionAsync(cancellationToken);
            if (selectedRegion == null)
            {
                return null;
            }

            return await RecognizeFromBitmapAsync(selectedRegion, cancellationToken: cancellationToken);
        }
        catch (OperationCanceledException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            System.Diagnostics.Debug.WriteLine("Screen capture permission denied for OCR.");
            return null;
        }
        catch (InvalidOperationException ex)
        {
            System.Diagnostics.Debug.WriteLine($"OCR engine not available: {ex.Message}");
            return null;
        }
    }

    public async Task<string> RecognizeFromBitmapAsync(Bitmap bitmap, string? languageCode = null, bool singleLine = false, CancellationToken cancellationToken = default)
    {
        var language = new Windows.Globalization.Language(languageCode ?? _currentLanguageCode);
        var ocrEngine = Windows.Media.Ocr.OcrEngine.TryCreateFromLanguage(language);

        // Fallback to system language if preferred language not available
        ocrEngine ??= Windows.Media.Ocr.OcrEngine.TryCreateFromUserProfileLanguages();

        if (ocrEngine == null)
        {
            throw new InvalidOperationException($"OCR engine not available for language: {languageCode ?? _currentLanguageCode}");
        }

        // Scale the image for optimal OCR accuracy using a lightweight dimension heuristic
        var scaleFactor = GetScaleFactorFromDimensions(bitmap);
        using var scaledBitmap = ScaleBitmapUniform(bitmap, scaleFactor);

        // Convert to SoftwareBitmap for Windows.Media.Ocr
        using var softwareBitmap = await CreateSoftwareBitmapAsync(scaledBitmap);

        // Perform OCR
        var result = await ocrEngine.RecognizeAsync(softwareBitmap);

        // Post-process per-word: CJK space removal, RTL word reversal, single-line mode
        string separator = singleLine ? " " : Environment.NewLine;

        IEnumerable<string> lines;

        if (language.LanguageTag.StartsWith("zh", StringComparison.OrdinalIgnoreCase) ||
            language.LanguageTag.StartsWith("ja", StringComparison.OrdinalIgnoreCase))
        {
            lines = result.Lines.Select(line => string.Concat(line.Words.Select(w => w.Text)));
        }
        else if (language.LayoutDirection == Windows.Globalization.LanguageLayoutDirection.Rtl)
        {
            lines = result.Lines.Select(line => string.Join(" ", line.Words.Reverse().Select(w => w.Text)));
        }
        else
        {
            lines = result.Lines.Select(line => line.Text);
        }

        return string.Join(separator, lines);
    }

    /// <summary>
    /// Converts System.Drawing.Bitmap to Windows.Graphics.Imaging.SoftwareBitmap.
    /// Following Text Grab's SoftwareBitmapExtensions.CreateSoftwareBitmap pattern.
    /// Uses BMP encoding (not PNG) for faster conversion without compression artifacts.
    /// </summary>
    private static async Task<Windows.Graphics.Imaging.SoftwareBitmap> CreateSoftwareBitmapAsync(Bitmap bitmap)
    {
        using var memory = new MemoryStream();
        bitmap.Save(memory, ImageFormat.Bmp);
        memory.Position = 0;

        var stream = memory.AsRandomAccessStream();
        var decoder = await Windows.Graphics.Imaging.BitmapDecoder.CreateAsync(stream);
        return await decoder.GetSoftwareBitmapAsync(
            Windows.Graphics.Imaging.BitmapPixelFormat.Bgra8,
            Windows.Graphics.Imaging.BitmapAlphaMode.Premultiplied);
    }

    /// <summary>
    /// Estimates the ideal scale factor for OCR based on image dimensions.
    /// Uses a lightweight heuristic instead of a full OCR pre-pass to avoid
    /// allocating a SoftwareBitmap + running RecognizeAsync just to measure heights.
    /// </summary>
    private static double GetScaleFactorFromDimensions(Bitmap bitmap)
    {
        // Heuristic: small captures likely contain small text needing enlargement
        double scaleFactor = bitmap.Height switch
        {
            < 80 => 2.0,
            < 200 => 1.5,
            _ => 1.0
        };

        // Guard against exceeding MaxImageDimension (2048px)
        double maxDim = Windows.Media.Ocr.OcrEngine.MaxImageDimension;
        int largerDim = Math.Max(bitmap.Width, bitmap.Height);
        if (largerDim * scaleFactor > maxDim)
        {
            scaleFactor = maxDim / largerDim;
        }

        return Math.Clamp(scaleFactor, 0.5, 2.0);
    }

    /// <summary>
    /// Scales a bitmap uniformly by the given factor.
    /// Following Text Grab's ImageMethods.ScaleBitmapUniform pattern.
    /// </summary>
    private static Bitmap ScaleBitmapUniform(Bitmap bitmap, double scale)
    {
        int newWidth = (int)(bitmap.Width * scale);
        int newHeight = (int)(bitmap.Height * scale);

        var newBitmap = new Bitmap(newWidth, newHeight, bitmap.PixelFormat);
        using var graphics = Graphics.FromImage(newBitmap);
        graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
        graphics.DrawImage(bitmap, 0, 0, newWidth, newHeight);

        return newBitmap;
    }
}
