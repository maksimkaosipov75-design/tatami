using System.Diagnostics;
using OmarchyDock.Native;

namespace OmarchyDock.Services;

internal record RunningWindow(nint Handle, string Title, string ExePath);

internal static class WindowEnumerator
{
    public static List<RunningWindow> GetDockableWindows()
    {
        var results = new List<RunningWindow>();

        Win32.EnumWindows((hWnd, _) =>
        {
            if (!Win32.IsAltTabWindow(hWnd)) return true;

            var exePath = TryGetExePath(hWnd);
            if (exePath is null) return true;

            results.Add(new RunningWindow(hWnd, Win32.GetWindowTitle(hWnd), exePath));
            return true;
        }, 0);

        return results;
    }

    private static string? TryGetExePath(nint hWnd)
    {
        try
        {
            Win32.GetWindowThreadProcessId(hWnd, out var pid);
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
