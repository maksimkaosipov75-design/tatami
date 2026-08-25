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

public static class TaskbarVisibility
{
    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern IntPtr FindWindowEx(IntPtr parent, IntPtr childAfter, string className, string windowName);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    private const int SW_HIDE = 0;
    private const int SW_SHOW = 5;

    public static int Apply(bool visible)
    {
        var command = visible ? SW_SHOW : SW_HIDE;
        var affected = 0;

        // Primary taskbar.
        var primary = FindWindow("Shell_TrayWnd", null);
        if (primary != IntPtr.Zero)
        {
            ShowWindow(primary, command);
            affected++;
        }

        // Secondary taskbars, one per additional monitor.
        IntPtr secondary = IntPtr.Zero;
        while ((secondary = FindWindowEx(IntPtr.Zero, secondary, "Shell_SecondaryTrayWnd", null)) != IntPtr.Zero)
        {
            ShowWindow(secondary, command);
            affected++;
        }

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
