# Omarchy-like setup for Windows 10

Tiling, minimal desktop environment for Windows 10, modeled after Omarchy (Hyprland + Waybar + Alacritty + Walker + LazyVim, Catppuccin Mocha), built from GlazeWM + Zebar + WezTerm + LazyVim.

Status: Phase 1 (preflight / scaffold) in progress.

## Layout

- `glazewm/config.yaml` — tiling WM config
- `zebar/` — status bar config
- `wezterm/wezterm.lua` — terminal config
- `powershell/` — PS7 profile + oh-my-posh theme
- `nvim/` — LazyVim config
- `theme/mocha.json` — single source of truth for Catppuccin Mocha colors
- `wallpaper/` — wallpaper files
- `scripts/` — setup/teardown scripts, run in order (00 → 99)
- `_backup/` — pre-overwrite backups of existing configs + `registry-undo.ps1`

## Environment (recorded at Phase 1 preflight, 2026-08-23)

- OS: Windows 10 IoT Enterprise LTSC 2021, build 19044 (21H2)
- PowerShell: 5.1 → installed PowerShell 7.6.5 via winget (`Microsoft.PowerShell`)
- Git: not present → installed 2.55.0.3 via winget (`Git.Git`)
- winget: v1.29.290 (present)
- scoop: not yet installed (Phase 2)
- Architecture: 64-bit
- Developer Mode: enabled (symlinks do not require admin, though this session is already elevated)
- User profile path contains Cyrillic characters (`C:\Users\максим`) — flagged as a possible source of trouble for tools that mishandle non-ASCII paths; will verify per-tool as we go rather than assume.

## Known limitations

- DWM provides no blur, animations, or per-workspace effects like Hyprland — not attempted.
- Some windows (UWP, dialogs, installers) tile poorly; handled via ignore rules, not fought.
- Symlinks on Win10 require admin or Developer Mode (Developer Mode is enabled here).
- Mainstream support for Windows 10 ended October 2025; consumer ESU is time-limited — treat this setup as temporary.
