using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace OmarchyDock.Services;

internal class DockSettings
{
    [JsonPropertyName("hideWindowsTaskbar")]
    public bool HideWindowsTaskbar { get; set; }

    /// <summary>
    /// Pauses GlazeWM while a fullscreen app is focused.
    ///
    /// Off by default, and kept only as an escape hatch: it loses the race it's
    /// trying to win. GlazeWM re-tiles from its own hook within milliseconds,
    /// while this has to poll and then spawn a CLI process - so the game is
    /// already back in a window by the time the pause lands. Resuming afterwards
    /// makes GlazeWM re-apply the layout, which is a second visible rearrange.
    /// <see cref="AutoIgnoreFullscreenApps"/> is the mechanism that actually works.
    /// </summary>
    [JsonPropertyName("autoPauseTilingInFullscreen")]
    public bool AutoPauseTilingInFullscreen { get; set; }

    /// <summary>
    /// Offers to add an app to GlazeWM's ignore list when it's seen being pulled
    /// back out of fullscreen. Ignored windows are never managed at all, so
    /// there is no race to lose - this is the only reliable fix, and upstream
    /// has no config option for it.
    /// </summary>
    [JsonPropertyName("autoIgnoreFullscreenApps")]
    public bool AutoIgnoreFullscreenApps { get; set; } = true;

    /// <summary>Processes already offered - so a declined app isn't asked about again.</summary>
    [JsonPropertyName("fullscreenPromptsShown")]
    public List<string> FullscreenPromptsShown { get; set; } = new();

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
