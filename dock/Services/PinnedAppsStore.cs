using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace OmarchyDock.Services;

internal class PinnedApp
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("path")]
    public string Path { get; set; } = "";
}

internal static class PinnedAppsStore
{
    public static List<PinnedApp> Load()
    {
        var path = System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "dotfiles", "dock", "pinned.json");

        if (!File.Exists(path)) return new List<PinnedApp>();

        try
        {
            var apps = JsonSerializer.Deserialize<List<PinnedApp>>(File.ReadAllText(path)) ?? new();
            foreach (var app in apps)
            {
                app.Path = Environment.ExpandEnvironmentVariables(app.Path);
            }
            return apps;
        }
        catch
        {
            return new List<PinnedApp>();
        }
    }
}
