using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace OmarchyDock.Services;

public enum RunningIndicator
{
    None,
    Dot,
    Line,
}

/// <summary>
/// Everything the settings window can change, persisted to dock/settings.json.
///
/// Raises PropertyChanged so the dock can apply edits live - the settings window
/// is only useful if the result is visible while you drag the slider, not after
/// a restart.
/// </summary>
public class DockSettings : INotifyPropertyChanged
{
    // --- Appearance ---

    private int _iconSize = 48;
    [JsonPropertyName("iconSize")]
    public int IconSize { get => _iconSize; set => Set(ref _iconSize, Math.Clamp(value, 24, 96)); }

    private int _iconSpacing = 4;
    [JsonPropertyName("iconSpacing")]
    public int IconSpacing { get => _iconSpacing; set => Set(ref _iconSpacing, Math.Clamp(value, 0, 24)); }

    private int _cornerRadius = 20;
    [JsonPropertyName("cornerRadius")]
    public int CornerRadius { get => _cornerRadius; set => Set(ref _cornerRadius, Math.Clamp(value, 0, 40)); }

    private double _backgroundOpacity = 0.92;
    [JsonPropertyName("backgroundOpacity")]
    public double BackgroundOpacity { get => _backgroundOpacity; set => Set(ref _backgroundOpacity, Math.Clamp(value, 0.1, 1.0)); }

    private RunningIndicator _runningIndicator = RunningIndicator.Dot;
    [JsonPropertyName("runningIndicator")]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public RunningIndicator RunningIndicator { get => _runningIndicator; set => Set(ref _runningIndicator, value); }

    // --- Behaviour ---

    private bool _autoHide = true;
    [JsonPropertyName("autoHide")]
    public bool AutoHide { get => _autoHide; set => Set(ref _autoHide, value); }

    private bool _hideOverFullscreen = true;
    [JsonPropertyName("hideOverFullscreen")]
    public bool HideOverFullscreen { get => _hideOverFullscreen; set => Set(ref _hideOverFullscreen, value); }

    private bool _hideWindowsTaskbar;
    [JsonPropertyName("hideWindowsTaskbar")]
    public bool HideWindowsTaskbar { get => _hideWindowsTaskbar; set => Set(ref _hideWindowsTaskbar, value); }

    // --- Effects ---

    private bool _magnifyOnHover = true;
    [JsonPropertyName("magnifyOnHover")]
    public bool MagnifyOnHover { get => _magnifyOnHover; set => Set(ref _magnifyOnHover, value); }

    private double _magnifyScale = 1.4;
    [JsonPropertyName("magnifyScale")]
    public double MagnifyScale { get => _magnifyScale; set => Set(ref _magnifyScale, Math.Clamp(value, 1.0, 2.0)); }

    private bool _genieEnabled = true;
    [JsonPropertyName("genieEnabled")]
    public bool GenieEnabled { get => _genieEnabled; set => Set(ref _genieEnabled, value); }

    private int _genieDurationMs = 420;
    [JsonPropertyName("genieDurationMs")]
    public int GenieDurationMs { get => _genieDurationMs; set => Set(ref _genieDurationMs, Math.Clamp(value, 120, 1200)); }

    // An "auto-pause the window manager while a fullscreen app is focused"
    // option lived here. Removed: it always lost the race (the WM reacts from
    // its own hook in milliseconds; this had to poll and then spawn a CLI
    // process), and resuming made the WM re-apply the layout - a second visible
    // rearrange. Excluding the app from tiling is the mechanism that works.

    // --- Persistence ---

    private static string StorePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        "dotfiles", "dock", "settings.json");

    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    public static DockSettings Load()
    {
        try
        {
            var path = StorePath;
            if (!File.Exists(path)) return new DockSettings();
            return JsonSerializer.Deserialize<DockSettings>(File.ReadAllText(path), Options) ?? new DockSettings();
        }
        catch
        {
            // A hand-edited file with a syntax error shouldn't stop the dock
            // from starting - fall back to defaults.
            return new DockSettings();
        }
    }

    public void Save()
    {
        try
        {
            var path = StorePath;
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, JsonSerializer.Serialize(this, Options));
        }
        catch (Exception ex)
        {
            Diagnostics.Log($"failed to save dock settings: {ex.Message}");
        }
    }

    /// <summary>Copies every persisted value from another instance, raising change notifications.</summary>
    public void CopyFrom(DockSettings other)
    {
        IconSize = other.IconSize;
        IconSpacing = other.IconSpacing;
        CornerRadius = other.CornerRadius;
        BackgroundOpacity = other.BackgroundOpacity;
        RunningIndicator = other.RunningIndicator;
        AutoHide = other.AutoHide;
        HideOverFullscreen = other.HideOverFullscreen;
        HideWindowsTaskbar = other.HideWindowsTaskbar;
        MagnifyOnHover = other.MagnifyOnHover;
        MagnifyScale = other.MagnifyScale;
        GenieEnabled = other.GenieEnabled;
        GenieDurationMs = other.GenieDurationMs;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
