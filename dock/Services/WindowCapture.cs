using System.Drawing;
using System.Drawing.Imaging;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using OmarchyDock.Native;

namespace OmarchyDock.Services;

internal record CapturedWindow(BitmapSource Image, Win32.RECT Bounds);

internal static class WindowCapture
{
    /// <summary>
    /// Grabs a one-shot bitmap of a window via PrintWindow. Used as the texture
    /// for the genie animation, so it only has to be correct at the instant the
    /// user clicks - no need for the complexity of a live capture session.
    /// </summary>
    public static CapturedWindow? Capture(nint hwnd)
    {
        if (!Win32.GetWindowRect(hwnd, out var rect)) return null;
        if (rect.Width <= 0 || rect.Height <= 0) return null;

        // Very large windows would make a needlessly heavy texture; the genie
        // shrinks it to icon size anyway, so cap the captured resolution.
        try
        {
            using var bitmap = new Bitmap(rect.Width, rect.Height, PixelFormat.Format32bppArgb);
            using (var graphics = Graphics.FromImage(bitmap))
            {
                var hdc = graphics.GetHdc();
                try
                {
                    if (!Win32.PrintWindow(hwnd, hdc, Win32.PW_RENDERFULLCONTENT)) return null;
                }
                finally
                {
                    graphics.ReleaseHdc(hdc);
                }
            }

            var hBitmap = bitmap.GetHbitmap();
            try
            {
                BitmapSource source = Imaging.CreateBitmapSourceFromHBitmap(
                    hBitmap,
                    IntPtr.Zero,
                    Int32Rect.Empty,
                    BitmapSizeOptions.FromEmptyOptions());

                source = Downscale(source);
                source.Freeze();
                return new CapturedWindow(source, rect);
            }
            finally
            {
                DeleteObject(hBitmap);
            }
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Caps the texture's longest side. A full 2560x1440 capture is a ~15MB
    /// texture that gets bilinear-sampled every frame while shrinking down to
    /// icon size - pure bandwidth cost on an iGPU sharing system memory, for
    /// detail that's never visible at that scale.
    /// </summary>
    private static BitmapSource Downscale(BitmapSource source)
    {
        const int maxSide = 1280;

        var longest = Math.Max(source.PixelWidth, source.PixelHeight);
        if (longest <= maxSide) return source;

        var scale = (double)maxSide / longest;
        return new TransformedBitmap(source, new System.Windows.Media.ScaleTransform(scale, scale));
    }

    [System.Runtime.InteropServices.DllImport("gdi32.dll")]
    private static extern bool DeleteObject(nint hObject);
}
