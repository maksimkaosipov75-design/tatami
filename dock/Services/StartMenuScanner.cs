using System.IO;
using Pier.Models;

namespace Pier.Services;

internal static class StartMenuScanner
{
    public static List<AppEntry> ScanApps()
    {
        var roots = new[]
        {
            Environment.GetFolderPath(Environment.SpecialFolder.CommonStartMenu),
            Environment.GetFolderPath(Environment.SpecialFolder.StartMenu),
        };

        var seenTargets = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var entries = new List<AppEntry>();

        foreach (var root in roots)
        {
            if (!Directory.Exists(root)) continue;

            // EnumerationOptions (not the SearchOption overload) so inaccessible
            // subdirectories are skipped instead of throwing: the Start Menu
            // contains folders the current user can't open, and EnumerateFiles is
            // lazy, so such a throw would escape any try/catch around this call
            // and surface during the foreach below instead.
            var options = new EnumerationOptions
            {
                RecurseSubdirectories = true,
                IgnoreInaccessible = true,
            };

            IEnumerable<string> lnkFiles;
            try
            {
                lnkFiles = Directory.EnumerateFiles(root, "*.lnk", options);
            }
            catch
            {
                continue;
            }

            foreach (var lnk in lnkFiles)
            {
                var resolved = LnkResolver.Resolve(lnk);
                if (resolved is null) continue;
                var (target, iconLocation) = resolved.Value;
                if (string.IsNullOrWhiteSpace(target) || !target.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                    continue;
                if (!seenTargets.Add(target)) continue; // same app pinned in multiple Start Menu folders

                var icon = string.IsNullOrWhiteSpace(iconLocation)
                    ? IconExtractor.FromFile(target)
                    : IconExtractor.FromIconLocation(iconLocation, target);
                if (icon is null) continue;

                entries.Add(new AppEntry
                {
                    Name = Path.GetFileNameWithoutExtension(lnk),
                    TargetPath = target,
                    Icon = icon,
                });
            }
        }

        return entries.OrderBy(e => e.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
    }
}
