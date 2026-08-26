using System.ComponentModel;

namespace TatamiSetup.Models;

public class Component : INotifyPropertyChanged
{
    public required string Id { get; init; }
    public required string Title { get; init; }
    public required string Description { get; init; }

    /// <summary>scoop package names this component installs, if any.</summary>
    public string[] Packages { get; init; } = Array.Empty<string>();

    private bool _isSelected = true;
    public bool IsSelected
    {
        get => _isSelected;
        set { _isSelected = value; OnPropertyChanged(nameof(IsSelected)); }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    public static List<Component> BuildCatalog() =>
    [
        new()
        {
            Id = "komorebi",
            Title = "komorebi — tiling window manager",
            Description = "Tiles windows automatically, 6 workspaces, Alt-based keybindings. Includes whkd for the hotkeys (komorebi has none built in) and a community rule set covering hundreds of apps that misbehave under tiling.",
            Packages = ["komorebi", "whkd"],
        },
        new()
        {
            Id = "glazewm",
            Title = "GlazeWM — alternative window manager",
            Description = "Kept as a fallback so scripts\\07-switch-wm.ps1 can switch between the two. Not started automatically.",
            Packages = ["glazewm"],
        },
        new()
        {
            Id = "yasb",
            Title = "YASB — status bar",
            Description = "Top bar showing workspaces, clock and system stats. Gets its own autostart entry.",
            Packages = ["yasb"],
        },
        new()
        {
            Id = "dock",
            Title = "Pier — macOS-style dock",
            Description = "Custom dock with genie minimize animation, hover magnification, pinned apps and a Launchpad. Needs the .NET 8 Desktop Runtime, installed automatically.",
        },
        new()
        {
            Id = "wezterm",
            Title = "WezTerm — terminal",
            Description = "GPU-accelerated terminal, themed Catppuccin Mocha, opens with Alt+Enter.",
            Packages = ["wezterm"],
        },
        new()
        {
            Id = "flow",
            Title = "Flow Launcher — app launcher",
            Description = "Spotlight-style launcher on Alt+Space.",
            Packages = ["flow-launcher"],
        },
        new()
        {
            Id = "neovim",
            Title = "Neovim + LazyVim",
            Description = "Editor with the LazyVim distribution, Catppuccin Mocha colorscheme.",
            Packages = ["neovim"],
        },
        new()
        {
            Id = "shell",
            Title = "PowerShell prompt + CLI tools",
            Description = "oh-my-posh prompt plus fzf, zoxide, eza, bat, fd, ripgrep and gh.",
            Packages = ["oh-my-posh", "fzf", "zoxide", "eza", "bat", "fd", "ripgrep", "gh"],
        },
        new()
        {
            Id = "font",
            Title = "JetBrainsMono Nerd Font",
            Description = "Required for the icons in the bar, terminal and prompt to render. Installed system-wide (needs admin).",
        },
        new()
        {
            Id = "tweaks",
            Title = "Windows appearance tweaks",
            Description = "Dark theme, hidden desktop icons, taskbar auto-hide, wallpaper. All HKCU-only and reversible via _backup\\registry-undo.ps1.",
        },
        new()
        {
            Id = "hidetaskbar",
            Title = "Hide the Windows taskbar",
            Description = "Hides the taskbar window itself, leaving explorer.exe running as the shell. Reversible with scripts\\05-taskbar.ps1 -Show.",
        },
    ];
}
