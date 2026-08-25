# Adds the currently focused window's process to GlazeWM's ignore list, so the
# window manager leaves it alone entirely.
#
# This is the fix for games that get pulled out of fullscreen: GlazeWM re-tiles
# the window as soon as it goes fullscreen, which drops it back to windowed.
# Auto-pausing on fullscreen isn't implemented upstream, so ignoring the process
# is the durable answer (Alt+Shift+P pauses everything as a one-off alternative).
#
# Usage:
#   1. Focus the game (windowed is fine)
#   2. pwsh -File 06-ignore-window.ps1
#
# Add -WhatIf to only print what would change.

[CmdletBinding(SupportsShouldProcess = $true)]
param()

$ErrorActionPreference = 'Stop'

$configPath = Join-Path $PSScriptRoot '..\glazewm\config.yaml'
if (-not (Test-Path $configPath)) {
    throw "GlazeWM config not found at $configPath"
}

Write-Host "Reading the focused window from GlazeWM..."
$json = & glazewm query focused | ConvertFrom-Json

$window = $json.data.focused
if (-not $window -or -not $window.processName) {
    throw "GlazeWM reported no focused window. Click the target window first, then re-run."
}

$processName = $window.processName
Write-Host "Focused window: process='$processName' class='$($window.className)' title='$($window.title)'"

$config = Get-Content $configPath -Raw

if ($config -match "window_process:\s*\{\s*equals:\s*'$([regex]::Escape($processName))'\s*\}") {
    Write-Host "'$processName' is already ignored - nothing to do."
    return
}

# Insert into the existing `ignore` rule's match list, right after its first entry.
$anchor = "  - commands: ['ignore']`n    match:`n"
if ($config -notmatch [regex]::Escape($anchor)) {
    throw "Could not find the 'ignore' window rule in $configPath - add the entry by hand."
}

$newEntry = "      - window_process: { equals: '$processName' }`n"
$updated = $config -replace [regex]::Escape($anchor), ($anchor + $newEntry)

if ($PSCmdlet.ShouldProcess($configPath, "add ignore rule for '$processName'")) {
    Set-Content -Path $configPath -Value $updated -NoNewline -Encoding UTF8
    Write-Host "Added ignore rule for '$processName'."

    & glazewm command wm-reload-config | Out-Null
    Write-Host "GlazeWM config reloaded. The window is no longer managed - fullscreen should stick."
}
