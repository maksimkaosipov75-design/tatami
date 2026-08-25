# Phase 4: HKCU-only Windows tweaks. Idempotent: re-running with the same
# desired values is a no-op (and does not append duplicate undo entries).
# Every change is recorded to _backup/registry-undo.ps1 BEFORE it is applied,
# restoring either the prior value or (if the value didn't exist) removing it.

$ErrorActionPreference = 'Stop'

$dotfiles = "$env:USERPROFILE\dotfiles"
$undoPath = Join-Path $dotfiles "_backup\registry-undo.ps1"
New-Item -ItemType Directory -Force -Path (Split-Path $undoPath) | Out-Null
if (-not (Test-Path $undoPath)) {
    Set-Content $undoPath "# Registry undo script for Phase 4 Windows tweaks.`n# Run this to revert every HKCU change 03-windows-tweaks.ps1 made.`n" -Encoding UTF8
}

function Set-RegTweak {
    param(
        [Parameter(Mandatory)][string]$Path,
        [Parameter(Mandatory)][string]$Name,
        [Parameter(Mandatory)]$Value,
        [string]$Type = 'DWord'
    )

    if (-not (Test-Path $Path)) {
        New-Item -Path $Path -Force | Out-Null
    }

    $existing = Get-ItemProperty -Path $Path -Name $Name -ErrorAction SilentlyContinue

    if ($existing -and $existing.$Name -eq $Value) {
        Write-Host "Already set: $Path\$Name = $Value"
        return
    }

    if ($existing) {
        $undoLine = "Set-ItemProperty -Path '$Path' -Name '$Name' -Value $($existing.$Name) -Type $Type -ErrorAction SilentlyContinue"
    } else {
        $undoLine = "Remove-ItemProperty -Path '$Path' -Name '$Name' -ErrorAction SilentlyContinue"
    }
    if ((Get-Content $undoPath -Raw) -notmatch [regex]::Escape($undoLine)) {
        Add-Content -Path $undoPath -Value $undoLine
    }

    try {
        New-ItemProperty -Path $Path -Name $Name -Value $Value -PropertyType $Type -Force -ErrorAction Stop | Out-Null
        $oldDisplay = if ($existing) { $existing.$Name } else { '<not set>' }
        Write-Host "Set $Path\$Name = $Value (was: $oldDisplay)"
    } catch {
        Write-Host "FAILED to set $Path\$Name : $($_.Exception.Message)"
    }
}

$personalize = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize'
$advanced = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced'
$search = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Search'
$people = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced\People'
$feeds = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Feeds'

Set-RegTweak -Path $personalize -Name 'AppsUseLightTheme' -Value 0
Set-RegTweak -Path $personalize -Name 'SystemUsesLightTheme' -Value 0
Set-RegTweak -Path $advanced -Name 'HideIcons' -Value 1
Set-RegTweak -Path $search -Name 'SearchboxTaskbarMode' -Value 0
Set-RegTweak -Path $advanced -Name 'ShowTaskViewButton' -Value 0
Set-RegTweak -Path $people -Name 'PeopleBand' -Value 0
Set-RegTweak -Path $feeds -Name 'ShellFeedsTaskbarViewMode' -Value 2
Set-RegTweak -Path $advanced -Name 'TaskbarSmallIcons' -Value 1
Set-RegTweak -Path $advanced -Name 'HideFileExt' -Value 0

# --- Taskbar auto-hide: flip bit 0x01 of byte index 8 in the binary blob,
# rather than writing a hardcoded replacement array. ---
$stuckRectsPath = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Explorer\StuckRects3'
$settings = (Get-ItemProperty -Path $stuckRectsPath -Name 'Settings' -ErrorAction Stop).Settings
$oldByte = $settings[8]
$newByte = $oldByte -bor 0x01
if ($oldByte -eq $newByte) {
    Write-Host "Taskbar auto-hide already enabled."
} else {
    $undoBytes = ($settings -join ',')
    Add-Content -Path $undoPath -Value "`$__stuckRects = (Get-ItemProperty -Path '$stuckRectsPath' -Name 'Settings').Settings; `$__stuckRects[8] = $oldByte; Set-ItemProperty -Path '$stuckRectsPath' -Name 'Settings' -Value `$__stuckRects -Type Binary"
    $settings[8] = $newByte
    Set-ItemProperty -Path $stuckRectsPath -Name 'Settings' -Value $settings -Type Binary
    Write-Host "Taskbar auto-hide enabled (byte[8]: $oldByte -> $newByte)"
}

# --- Autostart ---
#
# komorebi is the window manager (see README "From GlazeWM to komorebi"). It
# ships its own autostart command, which also handles whkd - komorebi has no
# built-in keybindings, so the hotkey daemon has to come up with it.
#
# YASB needs its own entry here: under GlazeWM it was launched from that WM's
# startup_commands, and komorebi has no equivalent.
$startupDir = [Environment]::GetFolderPath('Startup')
$shell = New-Object -ComObject WScript.Shell

function Set-StartupShortcut {
    param([string]$Name, [string]$Target, [string]$Arguments = '')

    $shortcutPath = Join-Path $startupDir "$Name.lnk"
    if (Test-Path $shortcutPath) {
        Write-Host "Startup shortcut already exists: $shortcutPath"
        return
    }
    if (-not (Test-Path $Target)) {
        Write-Host "SKIPPED $Name autostart - target not built yet: $Target"
        return
    }
    $shortcut = $shell.CreateShortcut($shortcutPath)
    $shortcut.TargetPath = $Target
    if ($Arguments) { $shortcut.Arguments = $Arguments }
    $shortcut.Save()
    Write-Host "Created startup shortcut: $shortcutPath -> $Target $Arguments"
}

if (Get-Command komorebic -ErrorAction SilentlyContinue) {
    # Writes komorebi.lnk pointing at komorebic-no-console, so no console window
    # flashes at sign-in. --whkd brings up the keybinding daemon alongside it.
    komorebic enable-autostart --whkd
} else {
    Write-Host "komorebic not found - skipping window manager autostart."
}

if (Get-Command yasbc -ErrorAction SilentlyContinue) {
    yasbc enable-autostart
}

Set-StartupShortcut -Name 'OmarchyDock' -Target (Join-Path $dotfiles 'dock\publish\OmarchyDock.exe')

# GlazeWM stays installed so 07-switch-wm.ps1 can switch back, but it must not
# also start at sign-in - two tiling managers at once fight over every window.
$staleGlazeWm = Join-Path $startupDir 'GlazeWM.lnk'
if (Test-Path $staleGlazeWm) {
    Remove-Item $staleGlazeWm -Force
    Write-Host "Removed the old GlazeWM autostart entry."
}

# --- Wallpaper: minimalist Catppuccin Mocha wallpaper from the well-known
# orangci/walls-catppuccin-mocha community collection. Falls back to a
# generated solid fill in the theme's base color if the download fails
# (e.g. no network on a fresh machine). ---
$wallpaperDir = Join-Path $dotfiles 'wallpaper'
$wallpaperPath = Join-Path $wallpaperDir 'minimalist-black-hole.png'
$fallbackWallpaperPath = Join-Path $wallpaperDir 'mocha-base.png'
New-Item -ItemType Directory -Force -Path $wallpaperDir | Out-Null

if (-not (Test-Path $wallpaperPath)) {
    try {
        Invoke-WebRequest -Uri 'https://raw.githubusercontent.com/orangci/walls-catppuccin-mocha/master/minimalist-black-hole.png' -OutFile $wallpaperPath -ErrorAction Stop
        Write-Host "Downloaded wallpaper: $wallpaperPath"
    } catch {
        Write-Host "Wallpaper download failed ($($_.Exception.Message)), falling back to a generated solid fill."
        $wallpaperPath = $fallbackWallpaperPath
    }
}

if (-not (Test-Path $wallpaperPath)) {
    Add-Type -AssemblyName System.Drawing
    $mocha = (Get-Content (Join-Path $dotfiles 'theme\mocha.json') -Raw | ConvertFrom-Json).colors
    $color = [System.Drawing.ColorTranslator]::FromHtml($mocha.base)
    $bmp = New-Object System.Drawing.Bitmap(64, 64)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.Clear($color)
    $g.Dispose()
    $bmp.Save($wallpaperPath, [System.Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()
    Write-Host "Generated fallback wallpaper: $wallpaperPath ($($mocha.base))"
} else {
    Write-Host "Wallpaper already present: $wallpaperPath"
}

Set-RegTweak -Path 'HKCU:\Control Panel\Desktop' -Name 'WallpaperStyle' -Value '10' -Type String
Set-RegTweak -Path 'HKCU:\Control Panel\Desktop' -Name 'TileWallpaper' -Value '0' -Type String

Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;
public class Wallpaper {
    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    public static extern int SystemParametersInfo(int uAction, int uParam, string lpvParam, int fuWinIni);
}
'@ -ErrorAction SilentlyContinue

$SPI_SETDESKWALLPAPER = 0x0014
$SPIF_UPDATEINIFILE = 0x01
$SPIF_SENDCHANGE = 0x02
[Wallpaper]::SystemParametersInfo($SPI_SETDESKWALLPAPER, 0, $wallpaperPath, $SPIF_UPDATEINIFILE -bor $SPIF_SENDCHANGE) | Out-Null
Write-Host "Applied wallpaper via SystemParametersInfo."

Write-Host "`nPhase 4 registry tweaks done. Restarting Explorer to apply..."
Stop-Process -Name explorer -Force
