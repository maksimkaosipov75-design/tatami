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
    private static readonly JsonSerializerOptions WriteOptions = new() { WriteIndented = true };

    private static string StorePath => System.IO.Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        "dotfiles", "dock", "pinned.json");

    public static List<PinnedApp> Load()
    {
        var path = StorePath;
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

    public static void Save(IEnumerable<PinnedApp> apps)
    {
        try
        {
            var path = StorePath;
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path)!);

            // Write paths back with the user profile re-tokenised, so the file
            // stays portable between machines and usernames - it's tracked in
            // the dotfiles repo and shipped inside the installer.
            var profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            var portable = apps.Select(a => new PinnedApp
            {
                Name = a.Name,
                Path = a.Path.StartsWith(profile, StringComparison.OrdinalIgnoreCase)
                    ? "%USERPROFILE%" + a.Path[profile.Length..]
                    : a.Path,
            });

            File.WriteAllText(path, JsonSerializer.Serialize(portable, WriteOptions));
        }
        catch (Exception ex)
        {
            Diagnostics.Log($"failed to save pinned apps: {ex.Message}");
        }
    }
}
