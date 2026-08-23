# Phase 1: preflight report + repo scaffold + restore point.
# Idempotent: safe to re-run (restore point creation is skipped if one was made in the last 24h,
# which is a Windows System Restore limitation, not something this script controls).

$ErrorActionPreference = 'Stop'

Write-Host "=== OS Version ==="
[System.Environment]::OSVersion | Format-List
(Get-ItemProperty "HKLM:\SOFTWARE\Microsoft\Windows NT\CurrentVersion").CurrentBuildNumber
(Get-ItemProperty "HKLM:\SOFTWARE\Microsoft\Windows NT\CurrentVersion").DisplayVersion

Write-Host "`n=== PowerShell Version (current host) ==="
$PSVersionTable.PSVersion

Write-Host "`n=== pwsh (PS7) presence ==="
if (-not (Get-Command pwsh -ErrorAction SilentlyContinue)) {
    Write-Host "pwsh not found, installing via winget..."
    winget install --id Microsoft.PowerShell -e --accept-source-agreements --accept-package-agreements --silent
} else {
    Write-Host "pwsh already installed."
}

Write-Host "`n=== git ==="
if (-not (Get-Command git -ErrorAction SilentlyContinue)) {
    Write-Host "git not found, installing via winget (needed early for git init / scoop buckets)..."
    winget install --id Git.Git -e --accept-source-agreements --accept-package-agreements --silent
} else {
    Write-Host "git already installed."
}

$env:Path = [System.Environment]::GetEnvironmentVariable('Path', 'Machine') + ';' + [System.Environment]::GetEnvironmentVariable('Path', 'User')

Write-Host "`n=== Architecture ==="
[System.Environment]::Is64BitOperatingSystem

$currentPrincipal = New-Object Security.Principal.WindowsPrincipal([Security.Principal.WindowsIdentity]::GetCurrent())
$isAdmin = $currentPrincipal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
Write-Host "`n=== Admin rights (current session) ==="
$isAdmin

Write-Host "`n=== Restore point ==="
if ($isAdmin) {
    try {
        Enable-ComputerRestore -Drive "C:\" -ErrorAction Stop
    } catch {
        Write-Host "Enable-ComputerRestore: $($_.Exception.Message)"
    }
    try {
        Checkpoint-Computer -Description "Pre-Omarchy-setup $(Get-Date -Format yyyy-MM-dd)" -RestorePointType "MODIFY_SETTINGS" -ErrorAction Stop
        Write-Host "Restore point created."
    } catch {
        Write-Host "FAILED to create restore point: $($_.Exception.Message)"
    }
} else {
    Write-Host "Not running as admin — skipping restore point. Re-run elevated, or continue only with explicit user consent."
}

Write-Host "`n=== Repo scaffold ==="
$base = "$env:USERPROFILE\dotfiles"
$dirs = @(
    "$base\glazewm", "$base\zebar", "$base\wezterm", "$base\powershell",
    "$base\nvim", "$base\scripts", "$base\_backup", "$base\theme", "$base\wallpaper"
)
foreach ($d in $dirs) {
    New-Item -ItemType Directory -Force -Path $d | Out-Null
}

if (-not (Test-Path "$base\.git")) {
    Push-Location $base
    git init
    git config core.autocrlf false
    git config core.eol lf
    Pop-Location
    Write-Host "Repo initialized at $base"
} else {
    Write-Host "Repo already initialized at $base"
}

Write-Host "`nPhase 1 preflight done."
