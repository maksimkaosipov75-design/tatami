using System.IO;
using System.Text.RegularExpressions;

namespace OmarchyDock.Services;

/// <summary>
/// Edits GlazeWM's config.yaml to add ignore rules.
///
/// An ignored window is never managed, so GlazeWM never re-tiles it and never
/// knocks it out of fullscreen. That's the only mechanism that reliably works:
/// pausing after the fact always loses the race, because GlazeWM reacts from its
/// own window hook within milliseconds.
/// </summary>
internal static partial class GlazeWmConfig
{
    private static string ConfigPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        ".glzr", "glazewm", "config.yaml");

    public static bool IsIgnored(string processName)
    {
        try
        {
            if (!File.Exists(ConfigPath)) return false;
            return IgnoreEntryRegex(processName).IsMatch(File.ReadAllText(ConfigPath));
        }
        catch
        {
            return false;
        }
    }

    /// <summary>Adds an ignore rule for the process and reloads GlazeWM. Idempotent.</summary>
    public static async Task<bool> AddIgnoreAsync(string processName)
    {
        try
        {
            if (!File.Exists(ConfigPath)) return false;

            var config = await File.ReadAllTextAsync(ConfigPath);
            if (IgnoreEntryRegex(processName).IsMatch(config)) return true;

            var anchor = AnchorRegex().Match(config);
            if (!anchor.Success)
            {
                Diagnostics.Log("could not find the 'ignore' window rule in config.yaml");
                return false;
            }

            var entry = $"      - window_process: {{ equals: '{processName}' }}\n";
            var updated = config.Insert(anchor.Index + anchor.Length, entry);

            await File.WriteAllTextAsync(ConfigPath, updated);
            await GlazeWmController.ReloadConfigAsync();

            Diagnostics.Log($"added GlazeWM ignore rule for '{processName}'");
            return true;
        }
        catch (Exception ex)
        {
            Diagnostics.Log($"failed to add ignore rule for '{processName}': {ex.Message}");
            return false;
        }
    }

    private static Regex IgnoreEntryRegex(string processName) =>
        new(@"window_process:\s*\{\s*equals:\s*'" + Regex.Escape(processName) + @"'\s*\}",
            RegexOptions.IgnoreCase);

    /// <summary>Matches the header of the existing `ignore` rule, so entries go into its match list.</summary>
    [GeneratedRegex(@"-\s*commands:\s*\['ignore'\]\s*\r?\n\s*match:\s*\r?\n")]
    private static partial Regex AnchorRegex();
}
