# Phase 2: install scoop + packages for the Omarchy-like Windows setup.
# Idempotent: safe to re-run. Requires PowerShell 5.1+ (installed via Phase 1).

$ErrorActionPreference = 'Stop'

function Test-CommandExists {
    param([string]$Name)
    return $null -ne (Get-Command $Name -ErrorAction SilentlyContinue)
}

function Update-SessionPath {
    $env:Path = [System.Environment]::GetEnvironmentVariable('Path', 'Machine') + ';' + [System.Environment]::GetEnvironmentVariable('Path', 'User')
}

$currentPrincipal = New-Object Security.Principal.WindowsPrincipal([Security.Principal.WindowsIdentity]::GetCurrent())
$isAdmin = $currentPrincipal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)

# --- scoop ---
if (-not (Test-CommandExists 'scoop')) {
    Write-Host "Installing scoop..."
    $installer = Invoke-RestMethod get.scoop.sh
    $sb = [scriptblock]::Create($installer)
    if ($isAdmin) {
        # scoop refuses to install under an elevated session unless explicitly told to.
        & $sb -RunAsAdmin
    } else {
        & $sb
    }
    Update-SessionPath
} else {
    Write-Host "scoop already installed."
}

Update-SessionPath

# --- buckets ---
$buckets = @('extras', 'nerd-fonts', 'versions')
$existingBuckets = (scoop bucket list | Select-Object -ExpandProperty Name)
foreach ($b in $buckets) {
    if ($existingBuckets -notcontains $b) {
        Write-Host "Adding bucket $b..."
        scoop bucket add $b
    } else {
        Write-Host "Bucket $b already added."
    }
}

# --- packages ---
# git is intentionally excluded: installed via winget in Phase 1 (needed early for
# `git init` and scoop bucket management) and already on PATH. A second copy from
# scoop would only shadow it for no benefit.
$packages = @(
    'glazewm',
    'zebar',
    'wezterm',
    'flow-launcher',
    'neovim',
    'oh-my-posh',
    'fzf',
    'zoxide',
    'eza',
    'bat',
    'fd',
    'ripgrep',
    'gh'
)

$installedNames = (scoop list 6>$null | Select-Object -ExpandProperty Name)
$toInstall = $packages | Where-Object { $installedNames -notcontains $_ }

if ($toInstall.Count -gt 0) {
    scoop install $toInstall
} else {
    Write-Host "All packages already installed."
}

# --- font (global install; requires admin or will prompt via scoop) ---
$fontInstalled = (scoop list 6>$null | Where-Object { $_.Name -eq 'JetBrainsMono-NF' })
if (-not $fontInstalled) {
    Write-Host "Installing JetBrainsMono Nerd Font (global, requires admin)..."
    scoop install -g nerd-fonts/JetBrainsMono-NF
} else {
    Write-Host "Font already installed."
}

Update-SessionPath
Write-Host "`nPhase 2 done. Installed versions:"
scoop list
