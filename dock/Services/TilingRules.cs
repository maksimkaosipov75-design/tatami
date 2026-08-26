using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Pier.Services;

/// <summary>
/// Excludes an application from tiling, so the window manager leaves it alone
/// entirely - the reliable fix for games that get pulled back out of fullscreen.
///
/// Applies the rule twice on purpose: through komorebi's CLI so it takes effect
/// immediately, and into komorebi.json so it survives a restart. The CLI rule
/// alone is session-only, and editing the file alone wouldn't apply until the
/// config is reloaded.
/// </summary>
internal static class TilingRules
{
    private const string IgnoreRulesKey = "ignore_rules";

    private static string ConfigPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        "komorebi.json");

    public static bool IsExcluded(string processName)
    {
        try
        {
            var rules = ReadRules(out _);
            return rules is not null && FindRule(rules, ExeOf(processName)) is not null;
        }
        catch
        {
            return false;
        }
    }

    public static async Task<bool> ExcludeAsync(string processName)
    {
        var exe = ExeOf(processName);

        try
        {
            var rules = ReadRules(out var root);
            if (rules is null || root is null) return false;

            if (FindRule(rules, exe) is null)
            {
                rules.Add(new JsonObject
                {
                    ["kind"] = "Exe",
                    ["id"] = exe,
                    ["matching_strategy"] = "Equals",
                });
                await WriteAsync(root);
            }

            // Immediate effect, without waiting for a config reload.
            await KomorebiCli.RunAsync($"ignore-rule exe {exe}");

            Diagnostics.Log($"excluded '{exe}' from tiling");
            return true;
        }
        catch (Exception ex)
        {
            Diagnostics.Log($"failed to exclude '{exe}' from tiling: {ex.Message}");
            return false;
        }
    }

    public static async Task<bool> IncludeAsync(string processName)
    {
        var exe = ExeOf(processName);

        try
        {
            var rules = ReadRules(out var root);
            if (rules is null || root is null) return false;

            var existing = FindRule(rules, exe);
            if (existing is null) return false;

            rules.Remove(existing);
            await WriteAsync(root);

            // komorebi has no "remove one rule" command, so the reload is what
            // actually drops it for the running instance.
            await KomorebiCli.RunAsync("reload-configuration");

            Diagnostics.Log($"restored tiling for '{exe}'");
            return true;
        }
        catch (Exception ex)
        {
            Diagnostics.Log($"failed to restore tiling for '{exe}': {ex.Message}");
            return false;
        }
    }

    private static JsonArray? ReadRules(out JsonObject? root)
    {
        root = null;
        if (!File.Exists(ConfigPath)) return null;

        root = JsonNode.Parse(File.ReadAllText(ConfigPath)) as JsonObject;
        if (root is null) return null;

        if (root[IgnoreRulesKey] is not JsonArray rules)
        {
            rules = new JsonArray();
            root[IgnoreRulesKey] = rules;
        }

        return rules;
    }

    private static JsonNode? FindRule(JsonArray rules, string exe) =>
        rules.FirstOrDefault(rule =>
            string.Equals(rule?["id"]?.GetValue<string>(), exe, StringComparison.OrdinalIgnoreCase));

    private static Task WriteAsync(JsonObject root) =>
        File.WriteAllTextAsync(ConfigPath, root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));

    private static string ExeOf(string processName) =>
        processName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ? processName : processName + ".exe";
}
