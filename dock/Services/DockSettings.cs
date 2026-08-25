using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace OmarchyDock.Services;

internal class DockSettings
{
    [JsonPropertyName("hideWindowsTaskbar")]
    public bool HideWindowsTaskbar { get; set; }

    // An "auto-pause the window manager while a fullscreen app is focused"
    // option lived here. Removed: it always lost the race (the WM reacts from
    // its own hook in milliseconds; this had to poll and then spawn a CLI
    // process), and resuming made the WM re-apply the layout - a second visible
    // rearrange. Excluding the app from tiling is the mechanism that works.

    // There was an "auto-ignore apps seen losing fullscreen" option here. It was
    // removed rather than fixed: with the taskbar hidden the work area equals
    // the screen, so every newly opened window briefly covers it before GlazeWM
    // tiles it - which is indistinguishable from a game being pulled out of
    // fullscreen. It matched ordinary apps instead of games. Excluding an app
    // from tiling is now an explicit choice in the dock's context menu.

    private static string StorePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        "dotfiles", "dock", "settings.json");

    public static DockSettings Load()
    {
        try
        {
            var path = StorePath;
            if (!File.Exists(path)) return new DockSettings();
            return JsonSerializer.Deserialize<DockSettings>(File.ReadAllText(path)) ?? new DockSettings();
        }
        catch
        {
            return new DockSettings();
        }
    }

    public void Save()
    {
        try
        {
            var path = StorePath;
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch (Exception ex)
        {
            Diagnostics.Log($"failed to save dock settings: {ex.Message}");
        }
    }
}
