# Hides or shows the Windows taskbar window itself.
#
# This hides the Shell_TrayWnd window; it does NOT replace explorer.exe as the
# shell (which the project rules forbid, and which breaks notifications and
# crash recovery). explorer keeps running normally - only its taskbar window is
# hidden, and nothing about the setup is destructive or hard to undo.
#
# Caveat: the taskbar reappears if explorer restarts (crash, or an settings
# change that recycles it). Re-run this script, or let the autostart entry do it
# at next sign-in.
#
# Usage:
#   pwsh -File 05-taskbar.ps1 -Hide
#   pwsh -File 05-taskbar.ps1 -Show

[CmdletBinding(DefaultParameterSetName = 'Hide')]
param(
    [Parameter(ParameterSetName = 'Hide')][switch]$Hide,
    [Parameter(ParameterSetName = 'Show')][switch]$Show
)

$ErrorActionPreference = 'Stop'

Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;
using System.Text;

public static class TaskbarVisibility
{
    private delegate bool EnumProc(IntPtr hWnd, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern bool EnumWindows(EnumProc callback, IntPtr lParam);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    private const int SW_HIDE = 0;
    private const int SW_SHOW = 5;

    // Matching on class name alone is not enough: YASB creates a window of
    // class Shell_TrayWnd too, and FindWindow would happily return that one,
    // leaving the real taskbar visible. Only windows owned by explorer.exe
    // (the shell) count.
    public static int Apply(bool visible)
    {
        var command = visible ? SW_SHOW : SW_HIDE;
        var affected = 0;
        var classes = new[] { "Shell_TrayWnd", "Shell_SecondaryTrayWnd" };

        EnumWindows((hWnd, _) =>
        {
            var className = new StringBuilder(256);
            GetClassName(hWnd, className, className.Capacity);
            if (Array.IndexOf(classes, className.ToString()) < 0) return true;

            uint pid;
            GetWindowThreadProcessId(hWnd, out pid);
            try
            {
                using (var process = System.Diagnostics.Process.GetProcessById((int)pid))
                {
                    if (!process.ProcessName.Equals("explorer", StringComparison.OrdinalIgnoreCase)) return true;
                }
            }
            catch
            {
                return true;
            }

            ShowWindow(hWnd, command);
            affected++;
            return true;
        }, IntPtr.Zero);

        return affected;
    }
}
'@

if ($Show) {
    $count = [TaskbarVisibility]::Apply($true)
    Write-Host "Taskbar shown ($count window(s))."
} else {
    $count = [TaskbarVisibility]::Apply($false)
    Write-Host "Taskbar hidden ($count window(s)). Run with -Show to bring it back."
}
