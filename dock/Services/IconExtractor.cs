using System.Drawing;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Pier.Native;

namespace Pier.Services;

internal static class IconExtractor
{
    private static readonly Dictionary<string, ImageSource> Cache = new(StringComparer.OrdinalIgnoreCase);

    public static ImageSource? FromFile(string path)
    {
        if (Cache.TryGetValue(path, out var cached)) return cached;

        try
        {
            using var icon = Icon.ExtractAssociatedIcon(path);
            if (icon is null) return null;

            var source = ToBitmapSource(icon.Handle);
            if (source is not null) Cache[path] = source;
            return source;
        }
        catch
        {
            return null;
        }
    }

    // iconLocation is the raw "path,index" string from a .lnk's IconLocation
    // property (see LnkResolver) - falls back to FromFile(path) when there's
    // no usable index (blank IconLocation, or index 0 pointing at path itself).
    public static ImageSource? FromIconLocation(string iconLocation, string fallbackPath)
    {
        var parts = iconLocation.Split(',');
        var path = parts.Length > 0 && parts[0].Length > 0 ? parts[0] : fallbackPath;
        var index = parts.Length > 1 && int.TryParse(parts[1], out var i) ? i : 0;

        var cacheKey = $"{path}|{index}";
        if (Cache.TryGetValue(cacheKey, out var cached)) return cached;

        if (index == 0) return FromFile(path);

        var large = new nint[1];
        try
        {
            var extracted = Win32.ExtractIconEx(path, index, large, null, 1);
            if (extracted == 0 || large[0] == 0) return FromFile(fallbackPath);

            var source = ToBitmapSource(large[0]);
            if (source is not null) Cache[cacheKey] = source;
            return source ?? FromFile(fallbackPath);
        }
        finally
        {
            if (large[0] != 0) Win32.DestroyIcon(large[0]);
        }
    }

    /// <summary>
    /// Icon straight from the window, for processes whose executable can't be
    /// read or whose file carries no icon. Cached per window handle.
    /// </summary>
    public static ImageSource? FromWindow(nint hWnd)
    {
        var cacheKey = $"hwnd:{hWnd}";
        if (Cache.TryGetValue(cacheKey, out var cached)) return cached;

        var handle = Win32.GetWindowIconHandle(hWnd);
        if (handle == 0) return null;

        var source = ToBitmapSource(handle);
        if (source is not null) Cache[cacheKey] = source;
        return source;
    }

    private static ImageSource? ToBitmapSource(nint hIcon)
    {
        try
        {
            var source = Imaging.CreateBitmapSourceFromHIcon(
                hIcon,
                Int32Rect.Empty,
                BitmapSizeOptions.FromEmptyOptions());
            source.Freeze();
            return source;
        }
        catch
        {
            return null;
        }
    }
}
