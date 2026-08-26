using System.Diagnostics;

namespace Pier.Services;

/// <summary>Thin wrapper around komorebic, the window manager's CLI.</summary>
internal static class KomorebiCli
{
    public static async Task<string?> RunAsync(string arguments)
    {
        try
        {
            using var process = Process.Start(new ProcessStartInfo("komorebic", arguments)
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            });
            if (process is null) return null;

            var output = await process.StandardOutput.ReadToEndAsync();
            await process.WaitForExitAsync();
            return process.ExitCode == 0 ? output : null;
        }
        catch
        {
            // komorebi not installed or not running - callers treat this as "no".
            return null;
        }
    }
}
