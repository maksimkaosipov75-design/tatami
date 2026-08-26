# Builds Pier (the custom WPF dock) into dock\publish\, which is what
# the autostart shortcut created by 03-windows-tweaks.ps1 points at.
# Idempotent: safe to re-run; stops a running instance first so the build can
# overwrite the exe, then relaunches it.

$ErrorActionPreference = 'Stop'

$dotfiles = "$env:USERPROFILE\dotfiles"
$dockDir = Join-Path $dotfiles 'dock'
$publishDir = Join-Path $dockDir 'publish'

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    Write-Host "dotnet SDK not found. Installing .NET 8 SDK via winget..."
    winget install --id Microsoft.DotNet.SDK.8 -e --accept-source-agreements --accept-package-agreements --silent
    $env:Path = [System.Environment]::GetEnvironmentVariable('Path', 'Machine') + ';' + [System.Environment]::GetEnvironmentVariable('Path', 'User')
}

$wasRunning = $null -ne (Get-Process Pier -ErrorAction SilentlyContinue)
if ($wasRunning) {
    Write-Host "Stopping running Pier so the build can replace its exe..."
    Get-Process Pier -ErrorAction SilentlyContinue | Stop-Process -Force
    Start-Sleep -Milliseconds 500
}

Push-Location $dockDir
try {
    dotnet publish -c Release -r win-x64 --self-contained false -o $publishDir
} finally {
    Pop-Location
}

Write-Host "`nBuilt: $publishDir\Pier.exe"

if ($wasRunning) {
    Start-Process (Join-Path $publishDir 'Pier.exe')
    Write-Host "Relaunched Pier."
}
