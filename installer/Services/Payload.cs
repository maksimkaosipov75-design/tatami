using System.IO;
using System.IO.Compression;
using System.Reflection;

namespace OmarchySetup.Services;

/// <summary>
/// Unpacks the dotfiles tree that was staged, zipped and embedded at build time
/// (see the StagePayload target in the csproj).
/// </summary>
internal static class Payload
{
    private const string ResourceName = "payload.zip";

    public static void ExtractTo(string destinationRoot, Action<string> log)
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException($"Embedded {ResourceName} is missing - the installer was built incorrectly.");

        using var archive = new ZipArchive(stream, ZipArchiveMode.Read);

        Directory.CreateDirectory(destinationRoot);
        log($"Unpacking {archive.Entries.Count} files to {destinationRoot}");

        foreach (var entry in archive.Entries)
        {
            if (entry.FullName.EndsWith('/')) continue; // directory marker

            var target = Path.GetFullPath(Path.Combine(destinationRoot, entry.FullName));

            // Guard against a crafted archive escaping the destination.
            if (!target.StartsWith(Path.GetFullPath(destinationRoot), StringComparison.OrdinalIgnoreCase))
                continue;

            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            entry.ExtractToFile(target, overwrite: true);
        }
    }
}
