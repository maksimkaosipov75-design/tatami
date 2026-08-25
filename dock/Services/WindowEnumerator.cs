using System.Diagnostics;
using OmarchyDock.Native;

namespace OmarchyDock.Services;

internal record RunningWindow(nint Handle, string Title, string ExePath);

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

            var exePath = TryGetExePath(pid);
            if (exePath is null) return true;

            results.Add(new RunningWindow(hWnd, Win32.GetWindowTitle(hWnd), exePath));
            return true;
        }, 0);

        return results;
    }

    private static string? TryGetExePath(uint pid)
    {
        try
        {
            if (pid == 0) return null;
            using var process = Process.GetProcessById((int)pid);
            return process.MainModule?.FileName;
        }
        catch
        {
            // Access denied (elevated process) or process exited mid-enumeration.
            return null;
        }
    }
}
