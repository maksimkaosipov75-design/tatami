# Switches the tiling window manager between GlazeWM and komorebi.
#
# Both stay installed - this only decides which one runs, so going back is one
# command. It also swaps YASB's workspace widget, since the two managers expose
# different widget types and the wrong one silently shows nothing.
#
# Usage:
#   pwsh -File 07-switch-wm.ps1 -To komorebi
#   pwsh -File 07-switch-wm.ps1 -To glazewm

param(
    [Parameter(Mandatory = $true)]
    [ValidateSet('komorebi', 'glazewm')]
    [string]$To
)

$ErrorActionPreference = 'Stop'

$env:Path = [System.Environment]::GetEnvironmentVariable('Path', 'Machine') + ';' +
            [System.Environment]::GetEnvironmentVariable('Path', 'User')

$yasbConfig = Join-Path $PSScriptRoot '..\yasb\config.yaml'

function Stop-All {
    Write-Host "Stopping any running window manager..."

    if (Get-Command glazewm -ErrorAction SilentlyContinue) {
        glazewm command wm-exit 2>&1 | Out-Null
    }
    if (Get-Command komorebic -ErrorAction SilentlyContinue) {
        komorebic stop --whkd 2>&1 | Out-Null
    }

    Start-Sleep -Seconds 2
    Get-Process glazewm, glazewm-watcher, komorebi, whkd -ErrorAction SilentlyContinue | Stop-Process -Force
    Get-Process yasb -ErrorAction SilentlyContinue | Stop-Process -Force
    Start-Sleep -Seconds 1
}

function Set-YasbWorkspaceWidget {
    param([string]$Target)

    if (-not (Test-Path $yasbConfig)) {
        Write-Host "YASB config not found, skipping widget swap."
        return
    }

    $config = Get-Content $yasbConfig -Raw

    if ($Target -eq 'komorebi') {
        $config = $config -replace 'glazewm_workspaces', 'komorebi_workspaces'
        $config = $config -replace 'glazewm\.workspaces\.GlazewmWorkspacesWidget', 'komorebi.workspaces.WorkspaceWidget'
    } else {
        $config = $config -replace 'komorebi_workspaces', 'glazewm_workspaces'
        $config = $config -replace 'komorebi\.workspaces\.WorkspaceWidget', 'glazewm.workspaces.GlazewmWorkspacesWidget'
    }

    Set-Content -Path $yasbConfig -Value $config -NoNewline -Encoding UTF8
    Write-Host "YASB workspace widget switched to $Target."
}

Stop-All
Set-YasbWorkspaceWidget -Target $To

if ($To -eq 'komorebi') {
    Write-Host "Starting komorebi (with whkd for keybindings)..."
    # --whkd starts the hotkey daemon; komorebi has no built-in keybindings,
    # unlike GlazeWM.
    Start-Process komorebic -ArgumentList 'start', '--whkd' -WindowStyle Hidden
    Start-Sleep -Seconds 5
    Start-Process yasb
} else {
    Write-Host "Starting GlazeWM..."
    # GlazeWM starts YASB itself through its startup_commands.
    Start-Process glazewm -ArgumentList 'start' -WindowStyle Hidden
    Start-Sleep -Seconds 5
}

Start-Sleep -Seconds 2
Write-Host "`nNow running:"
Get-Process glazewm, komorebi, whkd, yasb -ErrorAction SilentlyContinue |
    Select-Object ProcessName, Id | Format-Table -AutoSize

Write-Host "Switch the autostart entry too if you want this to persist:"
Write-Host "  shell:startup contains GlazeWM.lnk - replace it for komorebi with"
Write-Host "  'komorebic start --whkd' if you settle on komorebi."
