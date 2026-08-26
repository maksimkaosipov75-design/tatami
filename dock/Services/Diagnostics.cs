using System.IO;

namespace Pier;

internal static class Diagnostics
{
    private static readonly string LogPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        "dotfiles", "dock", "pier.log");

    public static void Log(string message)
    {
        try
        {
            File.AppendAllText(LogPath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}{Environment.NewLine}");
        }
        catch
        {
            // Diagnostics must never break the thing they're measuring.
        }
    }

    public static void Log(Exception ex) => Log(ex.ToString());
}
