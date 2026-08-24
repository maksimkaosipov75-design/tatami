using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace OmarchyDock.Services;

// Procedurally-drawn icons for dock buttons that aren't backed by a real
// file (e.g. Launchpad) - avoids shipping a binary icon asset for one glyph.
internal static class GlyphIcons
{
    public static ImageSource CreateGridDots(Color dotColor)
    {
        const int size = 32;
        const int dot = 6;
        const int gap = 4;

        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            var brush = new SolidColorBrush(dotColor);
            var start = (size - (dot * 3 + gap * 2)) / 2.0;
            for (var row = 0; row < 3; row++)
            {
                for (var col = 0; col < 3; col++)
                {
                    var x = start + col * (dot + gap);
                    var y = start + row * (dot + gap);
                    dc.DrawRoundedRectangle(brush, null, new Rect(x, y, dot, dot), 1.5, 1.5);
                }
            }
        }

        var bitmap = new RenderTargetBitmap(size, size, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(visual);
        bitmap.Freeze();
        return bitmap;
    }
}
