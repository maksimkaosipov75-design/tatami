using System.IO;
using Pier.Native;

namespace Pier.Services;

/// <summary>
/// Keeps the Windows taskbar hidden.
///
/// Hiding it once isn't enough: Windows re-shows the taskbar on its own in
/// plenty of situations - pressing the Win key, leaving a fullscreen app, or the
/// shell recalculating its work area when an appbar registers. Observed
/// directly: the taskbar came back with the same explorer process and the same
/// window handle, so nothing had restarted, it was simply shown again.
///
/// This only toggles the taskbar window's visibility. explorer.exe keeps running
/// as the shell - replacing it would break notifications and crash recovery.
/// </summary>
internal static class TaskbarController
{
    private static readonly string[] TaskbarClasses = ["Shell_TrayWnd", "Shell_SecondaryTrayWnd"];

    public static void Hide() => Apply(visible: false);

    public static void Show() => Apply(visible: true);

    /// <summary>Re-hides only if something made it visible again; cheap enough to poll.</summary>
    public static void EnforceHidden()
    {
        var windows = FindTaskbarWindows();
        if (!windows.Any(Win32.IsWindowVisible)) return;

        Diagnostics.Log("taskbar reappeared - hiding it again");
        Apply(visible: false);
    }

    private static void Apply(bool visible)
    {
        var command = visible ? Win32.SW_SHOW : Win32.SW_HIDE;
        foreach (var window in FindTaskbarWindows())
        {
            Win32.ShowWindow(window, command);
        }
    }

    /// <summary>
    /// Enumerates taskbar windows that actually belong to the shell.
    ///
    /// FindWindow("Shell_TrayWnd") is not usable here: YASB creates a window of
    /// that very class (presumably so Windows treats it as a taskbar), so
    /// FindWindow returned YASB's window and the real taskbar was left alone -
    /// which is why hiding silently stopped working after a YASB restart, e.g.
    /// on a theme change. Matching on the owning process removes the ambiguity.
    /// </summary>
    private static List<nint> FindTaskbarWindows()
    {
        var results = new List<nint>();

        Win32.EnumWindows((hWnd, _) =>
        {
            if (!TaskbarClasses.Contains(Win32.GetClassNameOf(hWnd))) return true;

            Win32.GetWindowThreadProcessId(hWnd, out var pid);
            var image = Win32.GetProcessImagePath(pid);
            if (image is null) return true;

            if (Path.GetFileName(image).Equals("explorer.exe", StringComparison.OrdinalIgnoreCase))
            {
                results.Add(hWnd);
            }

            return true;
        }, 0);

        return results;
    }
}
