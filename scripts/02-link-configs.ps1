# Phase 3: symlink dotfiles into place. Idempotent: re-linking an existing
# correct symlink is a no-op; anything that isn't already our symlink gets
# backed up to _backup/<date>/ before being replaced.

$ErrorActionPreference = 'Stop'

$dotfiles = "$env:USERPROFILE\dotfiles"
$backupDir = Join-Path $dotfiles "_backup\$(Get-Date -Format yyyy-MM-dd)"

function Set-DotfileLink {
    param(
        [string]$LinkPath,
        [string]$TargetPath,
        [ValidateSet('File', 'Directory')][string]$ItemType
    )

    $existing = Get-Item -LiteralPath $LinkPath -Force -ErrorAction SilentlyContinue

    if ($existing -and $existing.LinkType -eq 'SymbolicLink' -and $existing.Target -eq $TargetPath) {
        Write-Host "Already linked: $LinkPath"
        return
    }

    if ($existing) {
        New-Item -ItemType Directory -Force -Path $backupDir | Out-Null
        $backupName = Split-Path $LinkPath -Leaf
        $backupPath = Join-Path $backupDir $backupName
        Write-Host "Backing up existing $LinkPath -> $backupPath"
        if ($existing.LinkType -eq 'SymbolicLink') {
            Remove-Item -LiteralPath $LinkPath -Force -Recurse
        } else {
            Move-Item -LiteralPath $LinkPath -Destination $backupPath -Force
        }
    }

    if (Test-Path -LiteralPath $LinkPath) {
        Remove-Item -LiteralPath $LinkPath -Force -Recurse
    }

    New-Item -ItemType SymbolicLink -Path $LinkPath -Target $TargetPath | Out-Null
    Write-Host "Linked $LinkPath -> $TargetPath"
}

# --- GlazeWM: real container dir, config.yaml is the symlink ---
New-Item -ItemType Directory -Force -Path "$env:USERPROFILE\.glzr\glazewm" | Out-Null
Set-DotfileLink -LinkPath "$env:USERPROFILE\.glzr\glazewm\config.yaml" -TargetPath "$dotfiles\glazewm\config.yaml" -ItemType File

# --- Zebar: whole directory symlinked (widget packs live as subfolders) ---
New-Item -ItemType Directory -Force -Path "$env:USERPROFILE\.glzr" | Out-Null
Set-DotfileLink -LinkPath "$env:USERPROFILE\.glzr\zebar" -TargetPath "$dotfiles\zebar" -ItemType Directory

# --- WezTerm ---
Set-DotfileLink -LinkPath "$env:USERPROFILE\.wezterm.lua" -TargetPath "$dotfiles\wezterm\wezterm.lua" -ItemType File

# --- PowerShell 7 profile ---
Set-DotfileLink -LinkPath $PROFILE -TargetPath "$dotfiles\powershell\Microsoft.PowerShell_profile.ps1" -ItemType File

# --- Neovim (LazyVim) ---
Set-DotfileLink -LinkPath "$env:LOCALAPPDATA\nvim" -TargetPath "$dotfiles\nvim" -ItemType Directory

Write-Host "`nPhase 3 linking done."
