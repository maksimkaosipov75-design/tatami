# Omarchy-like setup for Windows 10

Tiling, minimal desktop environment for Windows 10, modeled after Omarchy (Hyprland + Waybar + Alacritty + Walker + LazyVim, Catppuccin Mocha), built from GlazeWM + YASB + WezTerm + LazyVim.

Status: Phase 5 (final) done, all five phases complete. **Status bar switched from Zebar to YASB post-Phase-5** — see "From Zebar to YASB" below. **A community YASB theme plus a macOS-style dock and downloaded wallpapers were added after that** — see "Theme, dock, and wallpapers" below.

## Layout

- `komorebi/` — window manager config, keybindings (`whkdrc`) and the community app rule set
- `glazewm/config.yaml` — the previous WM, kept as a switchable fallback
- `yasb/` — status bar config (`config.yaml`, `styles.css`, generated `theme.css`) — replaces the original Zebar setup, see below
- `dock/` — OmarchyDock, a custom C#/WPF macOS-style dock (see below)
- `installer/` — OmarchySetup, a single-exe GUI installer for the whole setup (see below)
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

## Theme, dock, and wallpapers (2026-08-23, post-YASB-migration)

### Theme

You browsed community themes with the bundled `yasb_themes.exe` GUI (installed alongside YASB) and picked one — a minimal "pill" style (rounded widget backgrounds on a `mantle`-colored bar, small dot-style workspace indicators). Two things needed fixing before it actually worked here:

1. **The theme was written for Komorebi, not GlazeWM.** `komorebi_workspaces` (type `komorebi.workspaces.WorkspaceWidget`) doesn't talk to GlazeWM at all. Changed to `glazewm_workspaces` (type `glazewm.workspaces.GlazewmWorkspacesWidget`), the top-level `komorebi:` config block to `glazewm:`, and the workspace CSS states from generic `.ws-btn.active` to GlazeWM's actual `.ws-btn.active_populated`/`.ws-btn.active_empty`/`.ws-btn.focused_populated`/`.ws-btn.focused_empty` (Komorebi and GlazeWM use different state class names for the same idea).
2. **Colors were hardcoded in `styles.css`** (a `:root { --mauve: #cba6f7; ... }` block with all 26 canonical Catppuccin Mocha colors, unprefixed). Rather than rewrite ~230 `var(--x)` call sites to match our old `--ctp-x` naming, `theme/mocha.json` was expanded from 10 to the full 26-color palette (plus this theme's own small deviations — `text: #D3D3D3` and `surface0: #282936` instead of the canonical values, plus an extra `main: #10151d` — kept as-is since that's what you were actually looking at and liked) and the generator in `scripts/02-link-configs.ps1` now emits unprefixed `--x`/`--x-rgb` variable names to match. `styles.css` now starts with `@import "theme.css";` instead of the inline block — one line changed, ~230 call sites untouched.

An earlier candidate theme (also picked via the same GUI browser, later replaced) had a widget labeled "Network Diagnostic Required" whose click handler actually just rickrolled (`Start-Process https://rroll.to/...` via PowerShell) — a prank left in by that theme's original author, not malicious, but worth knowing this ecosystem's themes can contain surprises like that. It's gone now since you moved to a different theme, but check click handlers before trusting a theme you didn't write.

### macOS-style dock

Standalone Windows dock apps that mimic macOS (RocketDock, ObjectDock, Appetizer, etc.) are either abandoned since ~2010–2015 or commercial, and none install via winget/scoop — installing one would mean hand-downloading a `.exe`, which the plan's own rules forbid. Instead: **YASB supports multiple independent bars** (`bars:` is a dict, not a single entry), so `yasb/config.yaml` now has a second bar, `dock` — bottom-positioned, centered, `width: "auto"`, floating (`windows_app_bar: false`, so it doesn't reserve screen space or push GlazeWM's tiled layout), holding a `yasb.taskbar.TaskbarWidget` instance (`dock_taskbar`) with 34px icons and no title labels. Styled in `styles.css` as a rounded pill (`.yasb-dock`) matching the top bar's aesthetic. The redundant small `taskbar` widget was removed from the top bar's left side — one place for running-app icons (the dock), matching how macOS actually splits this (menu bar shows the active app's name, not icons; the Dock holds icons).

This isn't true macOS magnification-on-hover (Qt's stylesheet engine doesn't support CSS transforms), just a hover/foreground background highlight — a reasonable approximation given what the styling engine can actually do.

### Wallpapers

Downloaded three from **[orangci/walls-catppuccin-mocha](https://github.com/orangci/walls-catppuccin-mocha)** — the original, most-referenced Catppuccin Mocha wallpaper collection — into `dotfiles/wallpaper/`: `minimalist-black-hole.png` (4400×2475, set as the active wallpaper), `space.png` (3840×2160), `pixel-earth.png` (1920×1080, pixel-art style). All verified as valid images (loaded via `System.Drawing.Image`) before being trusted. `scripts/03-windows-tweaks.ps1` now downloads `minimalist-black-hole.png` on a fresh run (falling back to the old generated solid-fill PNG if there's no network yet). To switch to one of the other two, or add your own, just point the registry `Wallpaper` value (or re-run the relevant part of that script with a different filename) at a different file in that folder.

## OmarchyDock — the custom dock (2026-08-24)

The YASB second-bar dock (previous section) was replaced by a purpose-built app: `dock/`, a C#/WPF project (.NET 8). You picked C#/WPF over Rust or Python — native .NET has the most mature Win32 interop for this exact job (window enumeration, icon extraction, WinEvent hooks) and needs no WebView2, so it can't inherit the Zebar failure mode.

### What it does

| Feature | How |
|---|---|
| Icons for open windows | `EnumWindows` + alt-tab-style filtering (visible, has title, not cloaked, not a tool window, is its own root) — the same rules the real taskbar uses |
| Click = focus / minimize | `SetForegroundWindow` + `ShowWindow`; clicking the already-focused window minimizes it, matching macOS |
| Pinned apps | `dock/pinned.json` — pinned entries keep a fixed position and merge with their running window rather than showing twice; clicking a non-running pin launches it |
| Hover magnification | WPF `ScaleTransform` + storyboard on `IsMouseOver`, anchored bottom-center (`RenderTransformOrigin="0.5,1"`) so icons grow upward like the real Dock |
| Auto-hide | Polls the cursor (120 ms) and slides the dock down `DockHiddenOffset` px via an eased `TranslateTransform` when the cursor leaves the bottom edge |
| Launchpad | Grid button on the left opens a full-screen overlay of every app found in both Start Menu folders; Esc or a click on the backdrop closes it |
| Theming | `Services/ThemeLoader.cs` parses `theme/mocha.json` at startup into WPF resources (`MauveBrush`, `BaseColor`, …). No generated intermediate file — unlike the YASB side, a compiled app can just read the JSON directly |
| Live updates | `SetWinEventHook` over the window create/destroy/show/hide/foreground/minimize range, debounced 200 ms, instead of polling |
| Minimize animations | A snapshot of the window is textured onto a mesh and deformed toward the icon: **Genie** funnels it in tail-first, **Vortex** wrings it out as it shrinks, **Shrink** scales it, **Drop** lets it fall. Windows' own animation is suppressed for the duration |
| Settings window | Right-click any icon → *Dock settings…*; every change is applied live and persisted to `dock/settings.json` (untracked — it is per-machine state) |

### Build and run

```powershell
pwsh -File scripts\04-build-dock.ps1   # installs .NET 8 SDK if missing, publishes to dock\publish\
```

`scripts\03-windows-tweaks.ps1` creates a `shell:startup` shortcut pointing at `dock\publish\OmarchyDock.exe`, so it starts with the session. The dock needs no ignore rule: it sets `ShowInTaskbar="False"` and `WS_EX_NOACTIVATE`, and komorebi does not manage windows that never take focus.

### Bugs found and fixed during development

- **Launchpad crashed the whole app on first click.** `Directory.EnumerateFiles(..., SearchOption.AllDirectories)` throws `UnauthorizedAccessException` on `C:\ProgramData\Microsoft\Windows\Start Menu\Программы`, and because the method is *lazy*, the throw happened during the `foreach` — outside the `try` that wrapped the call. Fixed by using the `EnumerationOptions` overload with `IgnoreInaccessible = true` (the `SearchOption` overload uses `EnumerationOptions.Compatible`, where that flag is off). Verified by reproducing the exact enumeration afterward: 75 files, no exception.
- **WezTerm had no icon.** `pinned.json` pointed at `scoop\shims\wezterm-gui.exe`, and scoop shims are thin launcher stubs carrying no icon resource. Repointed at the real binary under `scoop\apps\wezterm\current\`.
- A `DispatcherUnhandledException` handler now logs to `dock/omarchydock.log` and keeps the app alive, since a shell component shouldn't die from one bad icon or launch target.

### Known gaps (not implemented)

- No tray icon; the dock is controlled entirely from its own right-click menu.
- Launchpad has no search box (you chose the simpler grid-only version); with ~120 apps it's a scroll, not a filter.
- One dock on the primary monitor only; no per-monitor instances.
- The right-click menu is a stock WPF `ContextMenu`, so it renders in the system light style rather than the Mocha palette.
- The dock is fixed to the bottom edge — no left/right/top placement, and no length limit.

## OmarchySetup — the installer (2026-08-25)

`installer/` builds a single self-contained `OmarchySetup.exe` (~70 MB) that reproduces this whole setup on any Win10/11 machine, with a checkbox per component.

```powershell
cd installer
dotnet publish -c Release -o dist      # -> dist\OmarchySetup.exe
```

**Design:** the installer does not reimplement the setup logic. It embeds the dotfiles tree (staged and zipped by the `StagePayload` MSBuild target, straight from this repo — no duplicated copy), unpacks it to `%USERPROFILE%\dotfiles`, and then runs the same `scripts\*.ps1` this project already used. One source of truth, and the scripts stay usable standalone.

| Component | What it installs |
|---|---|
| GlazeWM | tiling WM |
| YASB | status bar |
| OmarchyDock | the custom dock (bundled prebuilt; pulls the .NET 8 Desktop Runtime via winget if missing) |
| WezTerm / Flow Launcher / Neovim | terminal, launcher, editor |
| PowerShell prompt + CLI tools | oh-my-posh, fzf, zoxide, eza, bat, fd, ripgrep, gh |
| JetBrainsMono Nerd Font | system-wide, needs admin |
| Windows tweaks | dark theme, hidden desktop icons, taskbar auto-hide, wallpaper |
| Hide the Windows taskbar | see below |

`scripts\01-packages.ps1` gained `-Packages` / `-InstallFont` so a subset can be installed; with no arguments it behaves exactly as before.

Notes:
- Self-contained on purpose — a fresh machine has no .NET, and the installer must run there.
- The bundled dock is framework-dependent (small), so the installer installs the .NET 8 Desktop Runtime only if the dock is selected.
- `00-preflight.ps1` is launched with Windows PowerShell 5.1 (always present); everything after it uses pwsh 7 once preflight has installed it.

## Hiding the Windows taskbar

`scripts\05-taskbar.ps1 -Hide` / `-Show`. Phase 4 only ever set the taskbar to **auto-hide** (the `StuckRects3` bit) — it was still there on hover. This actually hides the `Shell_TrayWnd` window.

It deliberately does **not** replace `explorer.exe` as the shell (forbidden by the project rules, and it breaks notifications and crash recovery): explorer keeps running, only its taskbar window is hidden. Verified with `IsWindowVisible` returning false while the explorer process stays alive.

Known limitation: the taskbar comes back if explorer restarts (a crash, or a settings change that recycles it). Re-run the script, or sign out and back in.

## From GlazeWM to komorebi (2026-08-25)

The window manager was switched to [komorebi](https://github.com/LGUG2Z/komorebi) after GlazeWM's limitations kept surfacing: no auto-handling of fullscreen apps (an open upstream feature request, no config option), and no way to re-adopt a window once it had been detached.

**What made the decision, and what it uncovered.** While setting komorebi up, the actual cause of "WezTerm and Firefox stopped tiling" turned out to be neither manager: both windows had `WS_EX_LAYERED` stuck on them, left behind by OmarchyDock's genie animation when a cycle didn't complete. Tiling managers skip layered windows on purpose (they're usually overlays), which is why switching managers didn't help either. Fixed properly — see "Alpha-hide bookkeeping" in `dock/MainWindow.xaml.cs`: hidden windows are tracked, released after 5s if an animation never finishes, and restored when the dock exits.

**What komorebi brings:** `applications.json`, a ~64KB community-maintained rule set covering hundreds of apps that misbehave under tiling, `komorebic ignore-rule` / `manage-rule` as first-class CLI commands, window stacking, and per-workspace layouts.

**What it costs:** no built-in keybindings — `whkd` runs alongside for those, and YASB needs its own autostart entry since GlazeWM used to launch it from `startup_commands`.

| | |
|---|---|
| Config | `komorebi/komorebi.json` → `~/komorebi.json`, `komorebi/whkdrc` → `~/.config/whkdrc` |
| Rules | `komorebi/applications.json` |
| Autostart | `komorebic enable-autostart --whkd` (uses `komorebic-no-console`, so no console flashes at sign-in) + `yasbc enable-autostart` |
| Switch back | `pwsh -File scripts\07-switch-wm.ps1 -To glazewm` |

GlazeWM stays installed and its config stays linked, purely so the switch script works both ways. It no longer starts automatically — two tiling managers at once fight over every window.

**Keybindings** were kept close to the GlazeWM set (Alt+HJKL focus, Alt+Shift+HJKL move, Alt+1..6 workspaces, Alt+Enter terminal, Alt+Space launcher, Alt+Q close). New from komorebi: stacking on Alt+arrows, `Alt+T` float a window, `Alt+F` monocle.

**Shared limitation, worth knowing:** neither manager adopts windows that were already open when it starts. Minimise and restore the window to hand it over. In daily use this doesn't bite, since the manager starts at sign-in before anything else.

## Known limitations

- DWM provides no blur, animations, or per-workspace effects like Hyprland — not attempted.
- Some windows (UWP, dialogs, installers) tile poorly; handled via ignore rules, not fought.
- Symlinks on Win10 require admin or Developer Mode (Developer Mode is enabled here).
- Mainstream support for Windows 10 ended October 2025; consumer ESU is time-limited — treat this setup as temporary.
- **GlazeWM was launched manually by you during testing** (confirmed running alongside YASB). If you ever start fresh, launch it (e.g. `glazewm start`) and confirm windows tile, the YASB bar appears with live workspaces, and the keybindings from `glazewm/config.yaml` work as expected.
- **Flow Launcher's theme** (Settings → Theme → Catppuccin Mocha) needs to be set by hand in its GUI once launched — no CLI for this.
- **`nvim-treesitter`/`blink.cmp` want a C compiler** for native parsers/fuzzy-matching (`winget install --id=BrechtSanders.WinLibs.POSIX.UCRT -e`) — not installed, since it's optional (Lua fallback works) and wasn't asked for. Install it later if you notice degraded completion/highlighting performance.
