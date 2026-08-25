using OmarchyDock.Native;

namespace OmarchyDock.Services;

/// <summary>
/// Keeps the Windows taskbar hidden.
///
/// Hiding it once isn't enough: Windows re-shows Shell_TrayWnd on its own in
/// plenty of situations - pressing the Win key, leaving a fullscreen app, or the
/// shell recalculating its work area when an appbar (YASB) registers. Observed
/// directly: the taskbar came back with the same explorer process and the same
/// window handle, so nothing had restarted, it was simply shown again.
///
/// This only toggles the taskbar window's visibility. explorer.exe keeps running
/// as the shell - replacing it would break notifications and crash recovery.
/// </summary>
internal static class TaskbarController
{
    public static void Hide() => Apply(visible: false);

    public static void Show() => Apply(visible: true);

    /// <summary>Re-hides only if something made it visible again; cheap enough to poll.</summary>
    public static void EnforceHidden()
    {
        var primary = Win32.FindWindow("Shell_TrayWnd", null);
        if (primary != 0 && Win32.IsWindowVisible(primary)) Apply(visible: false);
    }

    private static void Apply(bool visible)
    {
        var command = visible ? Win32.SW_SHOW : Win32.SW_HIDE;

        var primary = Win32.FindWindow("Shell_TrayWnd", null);
        if (primary != 0) Win32.ShowWindow(primary, command);

        // One secondary taskbar per additional monitor.
        nint secondary = 0;
        while ((secondary = Win32.FindWindowEx(0, secondary, "Shell_SecondaryTrayWnd", null)) != 0)
        {
            Win32.ShowWindow(secondary, command);
        }
    }
}
