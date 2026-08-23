# Full teardown of the Omarchy-like setup. NOT run automatically by anything
# else in this repo - review before running.
#
# Deliberately left in place: scoop itself, git, and PowerShell 7. Those were
# installed as general-purpose tools in Phase 1, not exclusively for this
# rice, so removing them here would be more destructive than "undo the rice."

$ErrorActionPreference = 'Continue'

$dotfiles = "$env:USERPROFILE\dotfiles"

Write-Host "=== Reverting registry tweaks ==="
$undoScript = Join-Path $dotfiles '_backup\registry-undo.ps1'
if (Test-Path $undoScript) {
    & $undoScript
} else {
    Write-Host "No registry-undo.ps1 found, skipping."
}

Write-Host "`n=== Removing startup shortcut ==="
$startupDir = [Environment]::GetFolderPath('Startup')
$shortcutPath = Join-Path $startupDir 'GlazeWM.lnk'
if (Test-Path $shortcutPath) {
    Remove-Item $shortcutPath -Force
    Write-Host "Removed $shortcutPath"
}

Write-Host "`n=== Removing symlinks ==="
$links = @(
    "$env:USERPROFILE\.glzr\glazewm\config.yaml",
    "$env:USERPROFILE\.glzr\zebar",
    "$env:USERPROFILE\.wezterm.lua",
    $PROFILE,
    "$env:LOCALAPPDATA\nvim"
)
foreach ($link in $links) {
    $item = Get-Item -LiteralPath $link -Force -ErrorAction SilentlyContinue
    if ($item -and $item.LinkType -eq 'SymbolicLink') {
        Remove-Item -LiteralPath $link -Force -Recurse
        Write-Host "Removed symlink: $link"
    } elseif ($item) {
        Write-Host "Skipping $link - exists but is not a symlink (not touching it)."
    }
}

# Leftover empty container dir from Phase 3 linking.
if ((Test-Path "$env:USERPROFILE\.glzr\glazewm") -and -not (Get-ChildItem "$env:USERPROFILE\.glzr\glazewm" -Force -ErrorAction SilentlyContinue)) {
    Remove-Item "$env:USERPROFILE\.glzr\glazewm" -Force
}
if ((Test-Path "$env:USERPROFILE\.glzr") -and -not (Get-ChildItem "$env:USERPROFILE\.glzr" -Force -ErrorAction SilentlyContinue)) {
    Remove-Item "$env:USERPROFILE\.glzr" -Force
}

Write-Host "`n=== Removing custom scoop shim ==="
if (Get-Command scoop -ErrorAction SilentlyContinue) {
    scoop shim rm flow-launcher 2>$null
}

Write-Host "`n=== Uninstalling scoop packages ==="
$packages = @(
    'glazewm', 'zebar', 'wezterm', 'flow-launcher', 'neovim', 'oh-my-posh',
    'fzf', 'zoxide', 'eza', 'bat', 'fd', 'ripgrep', 'gh'
)
if (Get-Command scoop -ErrorAction SilentlyContinue) {
    scoop uninstall $packages
    scoop uninstall -g nerd-fonts/JetBrainsMono-NF
} else {
    Write-Host "scoop not found, skipping package removal."
}

Write-Host "`nUninstall done."
Write-Host "Left in place: scoop, git, PowerShell 7, the dotfiles repo itself, and any backups under _backup\."
Write-Host "Your pre-existing pwsh profile (if any) is still sitting in _backup\<date>\ - restore it by hand if wanted."
