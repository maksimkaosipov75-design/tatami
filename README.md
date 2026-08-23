# Omarchy-like setup for Windows 10

Tiling, minimal desktop environment for Windows 10, modeled after Omarchy (Hyprland + Waybar + Alacritty + Walker + LazyVim, Catppuccin Mocha), built from GlazeWM + Zebar + WezTerm + LazyVim.

Status: Phase 2 (packages) done.

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
- scoop: 0.5.3, installed with `-RunAsAdmin` (this session was already elevated; scoop's installer refuses admin sessions by default, so the flag was required — not a deviation we chose, the environment forced it)
- Architecture: 64-bit
- Developer Mode: enabled (symlinks do not require admin, though this session is already elevated)
- User profile path contains Cyrillic characters (`C:\Users\максим`) — flagged as a possible source of trouble for tools that mishandle non-ASCII paths; will verify per-tool as we go rather than assume.

## Installed packages (Phase 2, via scoop 0.5.3, 2026-08-23)

| Role | Package | Version | Bucket |
|---|---|---|---|
| Tiling WM | glazewm | 3.10.1 | extras |
| Status bar | zebar | 3.3.1 | extras |
| Terminal | wezterm | 20240203-110809-5046fc22 | extras |
| Launcher | flow-launcher | 2.1.3 | extras |
| Editor | neovim | 0.12.4 | main |
| Prompt | oh-my-posh | 30.6.5 | main |
| CLI | fzf | 0.74.3 | main |
| CLI | zoxide | 0.10.0 | main |
| CLI | eza | 0.23.5 | main |
| CLI | bat | 0.26.1 | main |
| CLI | fd | 10.4.2 | main |
| CLI | ripgrep | 15.2.0 | main |
| CLI | gh | 2.98.0 | main |
| Font | nerd-fonts/JetBrainsMono-NF | 3.5.1 | nerd-fonts (global install, `-g`) |

Notes:
- `git` deliberately **not** installed via scoop, even though it's listed under CLI utilities in the original plan: it was already installed via winget in Phase 1 (needed early for `git init`/scoop buckets) and is on PATH. Installing a second copy via scoop would just shadow it for no benefit.
- All manifest names matched the plan exactly (`scoop search` confirmed before install) — no winget fallback was needed for anything in this phase.
- Optional suggested deps not installed (skipped as out of scope unless something actually breaks): `extras/vcredist2022` (suggested by neovim, ripgrep, bat), `less` (suggested by bat).

## Known limitations

- DWM provides no blur, animations, or per-workspace effects like Hyprland — not attempted.
- Some windows (UWP, dialogs, installers) tile poorly; handled via ignore rules, not fought.
- Symlinks on Win10 require admin or Developer Mode (Developer Mode is enabled here).
- Mainstream support for Windows 10 ended October 2025; consumer ESU is time-limited — treat this setup as temporary.
