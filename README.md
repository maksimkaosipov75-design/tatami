# Omarchy-like setup for Windows 10

Tiling, minimal desktop environment for Windows 10, modeled after Omarchy (Hyprland + Waybar + Alacritty + Walker + LazyVim, Catppuccin Mocha), built from GlazeWM + YASB + WezTerm + LazyVim.

Status: Phase 5 (final) done, all five phases complete. **Status bar switched from Zebar to YASB post-Phase-5** — see "From Zebar to YASB" below.

## Layout

- `glazewm/config.yaml` — tiling WM config
- `yasb/` — status bar config (`config.yaml`, `styles.css`, generated `theme.css`) — replaces the original Zebar setup, see below
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
| ~~Status bar~~ | ~~zebar 3.3.1~~ | uninstalled | replaced by YASB, see "From Zebar to YASB" |
| Status bar | yasb | 2.0.6 | extras |
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

## Phase 3: configs and symlinks (2026-08-23)

All configs were built from the actual verified schemas/sources of each tool (GlazeWM's own `sample-config.yaml`, Zebar's real `resources/starter/with-glazewm.html` example and `zpack-schema.json`, WezTerm's own docs, oh-my-posh's bundled `catppuccin_mocha.omp.json`, LazyVim's official colorscheme snippet) rather than guessed from the plan's prose description — Zebar in particular turned out to be HTML/CSS/JS widgets (webview-based), not a single CSS file as the plan assumed.

Symlinks created (all verified with `Get-Item | Select LinkTarget`):

| Link | Target |
|---|---|
| `%USERPROFILE%\.glzr\glazewm\config.yaml` | `dotfiles\glazewm\config.yaml` |
| `%USERPROFILE%\.config\yasb` (whole dir) | `dotfiles\yasb` — superseded the original `%USERPROFILE%\.glzr\zebar` link, see "From Zebar to YASB" |
| `%USERPROFILE%\.wezterm.lua` | `dotfiles\wezterm\wezterm.lua` |
| `$PROFILE` (pwsh 7) | `dotfiles\powershell\Microsoft.PowerShell_profile.ps1` |
| `%LOCALAPPDATA%\nvim` | `dotfiles\nvim` (LazyVim) |

**Backed up before overwrite**: `Documents\PowerShell\Microsoft.PowerShell_profile.ps1` already existed with your own PSReadLine color customization (Command/Parameter/Operator/Variable/String/Number/Comment colors) — saved to `_backup/2026-08-23/Microsoft.PowerShell_profile.ps1`, **not merged** into the new profile. If you want that customization back, merge it manually from the backup; it wasn't carried over automatically since the new profile is Catppuccin-themed via oh-my-posh instead.

### Design decisions / deviations from the plan's prose

- ~~**Zebar auto-start is fully scripted, not a GUI step.**~~ Historical — applied to Zebar, which was later replaced by YASB. See "From Zebar to YASB."
- ~~**Zebar theming is not hardcoded CSS.**~~ Historical — same reason. YASB uses an analogous but different mechanism, see below.
- **WezTerm uses its own built-in `'Catppuccin Mocha'` scheme by name**, not a hand-derived palette from `theme/mocha.json`. That file only has 10 colors (no cyan/magenta/etc.), so building a full ANSI terminal palette from it would mean inventing values. WezTerm's bundled scheme is the same canonical Catppuccin Mocha hex values, referenced by name — arguably less duplication than re-deriving it, not more.
- **oh-my-posh theme (`omarchy.omp.json`) is adapted from oh-my-posh's own verified `catppuccin_mocha.omp.json`**, hex-swapped to our palette via targeted text replacement (not retyped by hand — the file contains Nerd Font private-use-area glyphs that render invisibly in plain text, so a manual retype risked silently corrupting icons that can't be visually verified through this channel).
- **`flow-launcher` got its own scoop shim** (`scoop shim add flow-launcher ...\Flow.Launcher.exe`) so GlazeWM's `alt+space` binding can `shell-exec flow-launcher` without hardcoding a path containing the Cyrillic username into a config tracked in git.
- **`alt+shift+e` (`wm-exit`) was added** beyond the plan's explicit keybinding list — without it there's no way to stop GlazeWM short of Task Manager once it's running.
- **No battery widget in Zebar**: checked `Get-CimInstance Win32_Battery` — no battery present (desktop), so the plan's conditional "skip if no battery" applies and it's omitted rather than added-then-hidden.

### Known open item: PowerShell profile load time

The plan's own suggested benchmark (`Measure-Command { pwsh -NoLogo -Command exit }`) measured **~1.1–1.5s with the profile vs. ~310ms baseline** — well over the 300ms target. Breaking it down, `Set-PSReadLineOption` and `oh-my-posh init pwsh | Invoke-Expression` are the two expensive lines (~600ms each). However, this measurement ran inside this non-interactive automation session, which lacks a real console (VT processing) — PSReadLine's own warning during the test ("predictive suggestion feature cannot be enabled because the console output doesn't support virtual terminal processing or it's redirected") confirms the console here isn't representative of a real terminal window. **This number needs to be re-checked by you in an actual WezTerm/interactive pwsh window** — it may well be near-instant there. Flagging as unresolved rather than claiming the 300ms target is met.

## Phase 4: Windows tweaks (2026-08-23)

All changes are HKCU-only, applied by `scripts/03-windows-tweaks.ps1`, and reversible via `_backup/registry-undo.ps1` (every undo line was recorded *before* its change was applied, per the plan's own safety rule).

| Tweak | Key | Value | Result |
|---|---|---|---|
| Dark apps | `...\Themes\Personalize` `AppsUseLightTheme` | 0 | applied |
| Dark system | `...\Themes\Personalize` `SystemUsesLightTheme` | 0 | already set |
| Hide desktop icons | `...\Explorer\Advanced` `HideIcons` | 1 | applied |
| Remove taskbar search box | `...\Search` `SearchboxTaskbarMode` | 0 | already set |
| Remove Task View button | `...\Explorer\Advanced` `ShowTaskViewButton` | 0 | applied |
| Remove People | `...\Explorer\Advanced\People` `PeopleBand` | 0 | already set |
| Remove News and Interests | `...\Feeds` `ShellFeedsTaskbarViewMode` | 2 | **FAILED — see below** |
| Small taskbar icons | `...\Explorer\Advanced` `TaskbarSmallIcons` | 1 | applied |
| Show file extensions | `...\Explorer\Advanced` `HideFileExt` | 0 | already set |
| Taskbar auto-hide | `...\Explorer\StuckRects3` `Settings` byte[8] bit 0x01 | flipped via read-modify-write, not a replacement array | applied (2 → 3) |
| Wallpaper | `HKCU:\Control Panel\Desktop` `Wallpaper`/`WallpaperStyle`/`TileWallpaper` | generated solid `theme/mocha.json` `base` (#1e1e2e) fill, applied via `SystemParametersInfo` | applied |
| Autostart | `shell:startup\GlazeWM.lnk` → `scoop\shims\glazewm.exe start` | Zebar not linked separately — its own `startup_commands` already launches it, a second shortcut would duplicate it | created |

**`ShellFeedsTaskbarViewMode` failed with "Attempted to perform an unauthorized operation"** even though it's HKCU and the session is elevated — this specific `Feeds` subkey appears to carry a restrictive ACL on this machine (not something a normal HKCU value should have). The script catches this, logs it, and continues rather than aborting or trying to force it (e.g. by taking registry ownership) — that felt like it crossed from "tweak a value" into "fight the OS," out of scope for a cosmetic taskbar item. If you want "News and Interests" gone, you likely need to either grant yourself permission on that key manually (`regedit` → right-click `Feeds` → Permissions) or use Group Policy (`gpedit.msc`, if available on this SKU) instead.

**Wallpaper** was generated rather than fetched from the web or guessed — you confirmed this preference (a 64×64 solid PNG in the palette's `base` color, no gradient, matching the rest of the theme's "no gradients" rule). If you'd rather use a real photo/image later, drop it in `dotfiles/wallpaper/`, update the path the script points at, and re-run.

Explorer was restarted (`Stop-Process -Name explorer -Force`, confirmed back up with a fresh PID/StartTime afterward) so the changes take visual effect immediately. **Confirmed by you**: the taskbar auto-hides and reappears on hovering the bottom edge, as expected for `StuckRects3` byte[8]=3. (If it looked "gone" at first: that's the intended behavior, not a bug — the native taskbar retracts fully and only the very edge is hover-sensitive.)

## Phase 5: final (2026-08-23)

### Keybindings (`glazewm/config.yaml`)

| Keys | Action |
|---|---|
| `Alt+H/J/K/L` | Focus window left/down/up/right |
| `Alt+Shift+H/J/K/L` | Move window left/down/up/right |
| `Alt+1`..`Alt+6` | Switch to workspace 1–6 |
| `Alt+Shift+1`..`Alt+Shift+6` | Move focused window to workspace 1–6 and follow it |
| `Alt+Q` | Close focused window |
| `Alt+Enter` | Open WezTerm |
| `Alt+Space` | Open/focus Flow Launcher |
| `Alt+V` | Toggle tiling split direction |
| `Alt+F` | Toggle fullscreen |
| `Alt+Shift+R` | Reload GlazeWM config |
| `Alt+Shift+E` | Exit GlazeWM (added beyond the plan's list — otherwise there's no way to stop the WM short of Task Manager) |

### Restore from scratch on a new machine

1. Install git and PowerShell 7 (`winget install --id Git.Git -e`, `winget install --id Microsoft.PowerShell -e`).
2. Clone this repo to `%USERPROFILE%\dotfiles`.
3. Run, in order, from an elevated `pwsh`:
   ```powershell
   pwsh -File scripts\00-preflight.ps1
   pwsh -File scripts\01-packages.ps1
   pwsh -File scripts\02-link-configs.ps1
   pwsh -File scripts\03-windows-tweaks.ps1
   ```
4. Manual GUI steps that can't be scripted (see Known limitations): launch GlazeWM once (`glazewm start`) and confirm tiling/the YASB bar; set Flow Launcher's theme to Catppuccin Mocha by hand.
5. Log off and back on (or reboot) so the `shell:startup\GlazeWM.lnk` shortcut takes over autostart going forward.
6. Verify: `git log --oneline` in `dotfiles\` shows the phase history; `scoop list` shows the Phase 2 package table above; `nvim` opens with the `catppuccin` colorscheme active.

To fully undo everything: review and run `scripts\99-uninstall.ps1` (reverts registry via `_backup\registry-undo.ps1`, removes symlinks and the startup shortcut, uninstalls the scoop packages — leaves scoop/git/pwsh themselves in place, see the script's own header comment for why).

## From Zebar to YASB (2026-08-23, post-Phase-5)

After Phase 5 was done, Zebar's tray **Settings** window turned out to be unusable — it flashed and vanished immediately on every click, on a single monitor, both with and without GlazeWM running. Troubleshooting, in order:

1. Checked `Crashpad\reports` under `%APPDATA%\zebar` and `%LOCALAPPDATA%\com.glzr.zebar` — found `.dmp` crash files, initially assumed to be the cause.
2. Checked WebView2 Runtime (151.0.4129.101, present) and the GPU driver (integrated AMD Radeon Vega 8, driver dated 2024-02-20 — old, and this machine has restore-point history of GPU driver trouble via DDU).
3. Set `WEBVIEW2_ADDITIONAL_BROWSER_ARGUMENTS=--disable-gpu` (user env var) and relaunched the bar — **no change**.
4. Checked Windows Event Viewer (`Application` log) for the reproduction window — **no crash events at all** for `zebar.exe`/`msedgewebview2.exe`, and no new `.dmp` files were generated after the GPU-disable relaunch. This invalidated the crash theory: the original dumps were coincidental, from routine webview subprocess cycling at app startup, not from clicking Settings.
5. Ruled out GlazeWM interference: closed GlazeWM entirely (`Alt+Shift+E`) and reproduced the same symptom — so it isn't GlazeWM stealing focus from the popup either.
6. Cleared the WebView2 cache (`%APPDATA%\zebar\webview-cache`, `%LOCALAPPDATA%\com.glzr.zebar\EBWebView`) and relaunched — **no change**.
7. Checked zebar's own GitHub issues/releases for a matching report — none found; v3.3.1 (our installed version, and the latest) release notes mention "Bug fix for incorrect window positioning on Windows," suggesting this class of bug exists in this version but isn't specifically documented as fixed or reintroduced.

At that point, with WebView2 Runtime reinstall as the only untried option and no confirmed root cause, you asked to switch to an alternative rather than keep chasing it. **[YASB](https://github.com/amnweb/yasb)** (Python/PyQt6, not WebView2-based, actively maintained, with native GlazeWM widgets) replaced Zebar entirely.

### What changed

- `zebar` uninstalled (`scoop uninstall zebar`); `yasb` installed (`scoop install extras/yasb`, v2.0.6).
- `dotfiles/zebar/` deleted; `dotfiles/yasb/{config.yaml, styles.css, theme.css}` added.
- `glazewm/config.yaml`: `startup_commands` now `shell-exec yasb` (was the Zebar preset command), `shutdown_commands` now `shell-exec yasbc stop` (YASB ships its own control CLI, `yasbc`, with `start`/`stop`/`reload`/`log`/`migrate-config`/`enable-autostart` — nicer than anything Zebar's CLI offered), and the ignore `window_rules` entry changed from `window_process: zebar` to `window_process: yasb`.
- **Theming**: YASB's QSS engine doesn't support arbitrary CSS custom properties, but it does support `@import` of an external stylesheet plus `var()` — confirmed from YASB's own `system_colors`/`yasb_colors.css` feature docs, which use exactly this mechanism for Windows-accent-color theming. `scripts/02-link-configs.ps1` now generates `dotfiles/yasb/theme.css` (a `:root { --ctp-base: #1e1e2e; --ctp-base-rgb: 30, 30, 46; ... }` block, both hex and comma-separated RGB per color for `rgba(var(--x-rgb), alpha)` opacity blending) from `theme/mocha.json` every time it runs, and `styles.css` starts with `@import "theme.css";` and references `var(--ctp-*)` throughout. `theme/mocha.json` is still the one authored source; `theme.css` is a generated, regenerate-on-relink artifact (same pattern as the wallpaper PNG in Phase 4).
- **Icons**: the plan/theme called for Nerd Font icons in the bar (CPU/RAM/network/volume glyphs). Typing those directly into `config.yaml` repeatedly produced invisible Unicode private-use-area characters I could not visually verify through this text channel (the same failure mode hit earlier with the oh-my-posh theme) — Edit tool calls even failed to match because two "identical-looking" invisible strings weren't actually identical bytes. Rather than risk silently-wrong glyphs a third time, **`yasb/config.yaml` uses plain ASCII labels ("CPU", "MEM", "NET", "VOL") instead of icons.** If you want icons, the safest way to add them is by hand in a real text editor where you can see what you're typing, using the widget wiki pages' verbatim `\uXXXX` escapes as reference.
- **Autostart**: deliberately did **not** run `yasbc enable-autostart` — GlazeWM's own `startup_commands` already launches YASB, and enabling both would start two instances. If you ever stop using GlazeWM's autostart for this, switch to `yasbc enable-autostart` instead of hand-rolling something.
- Verified: `yasbc migrate-config` reports "No deprecated options found," and the bar ran stably (multiple minutes, both `yasb.exe` processes alive, no new crash artifacts) with GlazeWM running alongside it during testing.

### Widgets in the new bar (`yasb/config.yaml`)

Left: GlazeWM workspaces (1–6, active one gets the mauve accent block, matching the original one-accent-block design intent). Center: clock. Right: CPU, memory, network (WiFi/ethernet, one widget handles both), volume. No battery widget (confirmed no battery present via `Get-CimInstance Win32_Battery` back in Phase 3 — still a desktop).

## Known limitations

- DWM provides no blur, animations, or per-workspace effects like Hyprland — not attempted.
- Some windows (UWP, dialogs, installers) tile poorly; handled via ignore rules, not fought.
- Symlinks on Win10 require admin or Developer Mode (Developer Mode is enabled here).
- Mainstream support for Windows 10 ended October 2025; consumer ESU is time-limited — treat this setup as temporary.
- **GlazeWM was launched manually by you during testing** (confirmed running alongside YASB). If you ever start fresh, launch it (e.g. `glazewm start`) and confirm windows tile, the YASB bar appears with live workspaces, and the keybindings from `glazewm/config.yaml` work as expected.
- **Flow Launcher's theme** (Settings → Theme → Catppuccin Mocha) needs to be set by hand in its GUI once launched — no CLI for this.
- **`nvim-treesitter`/`blink.cmp` want a C compiler** for native parsers/fuzzy-matching (`winget install --id=BrechtSanders.WinLibs.POSIX.UCRT -e`) — not installed, since it's optional (Lua fallback works) and wasn't asked for. Install it later if you notice degraded completion/highlighting performance.
