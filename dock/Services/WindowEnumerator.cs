using Pier.Native;

namespace Pier.Services;

/// <summary>
/// ExePath is null when the process image can't be read at all; the window is
/// still listed, keyed and iconed from the window handle instead of dropped.
/// </summary>
internal record RunningWindow(nint Handle, string Title, string? ExePath);

internal static class WindowEnumerator
{
    private static readonly uint OwnProcessId = (uint)Environment.ProcessId;

    public static List<RunningWindow> GetDockableWindows()
    {
        var results = new List<RunningWindow>();

        Win32.EnumWindows((hWnd, _) =>
        {
            // Never list our own windows: a taskbar doesn't show itself, and an
            // entry for the dock let its own context menu close the dock (WPF
            // shuts down with the last window).
            Win32.GetWindowThreadProcessId(hWnd, out var pid);
            if (pid == OwnProcessId) return true;

            if (!Win32.IsAltTabWindow(hWnd)) return true;

            // A null path is no longer a reason to skip the window: it just
            // means the process is protected or elevated, which is exactly the
            // case for many games. They still belong in the dock.
            var exePath = Win32.GetProcessImagePath(pid);

            results.Add(new RunningWindow(hWnd, Win32.GetWindowTitle(hWnd), exePath));
            return true;
        }, 0);

        return results;
    }
}
