using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace OmarchyDock.Services;

internal class DockSettings
{
    [JsonPropertyName("hideWindowsTaskbar")]
    public bool HideWindowsTaskbar { get; set; }

    /// <summary>
    /// Pauses GlazeWM while a fullscreen app is focused, and resumes on exit.
    /// On by default: without it, tiling fights fullscreen games and drops them
    /// back to windowed.
    /// </summary>
    [JsonPropertyName("autoPauseTilingInFullscreen")]
    public bool AutoPauseTilingInFullscreen { get; set; } = true;

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
