using System.Diagnostics;
using System.Text.Json;

namespace OmarchyDock.Services;

/// <summary>
/// Talks to a running GlazeWM through its CLI.
///
/// Exists to provide auto-pause on fullscreen, which GlazeWM itself doesn't
/// have (it's an open feature request upstream). Without it, GlazeWM re-tiles a
/// game the instant it goes fullscreen and knocks it straight back out - the
/// alternative being a hand-maintained ignore rule per game.
/// </summary>
internal static class GlazeWmController
{
    public static async Task<bool?> IsPausedAsync()
    {
        var output = await RunAsync("query paused");
        if (output is null) return null;

        try
        {
            using var document = JsonDocument.Parse(output);
            var root = document.RootElement;
            if (!root.TryGetProperty("success", out var success) || !success.GetBoolean()) return null;
            return root.GetProperty("data").GetBoolean();
        }
        catch
        {
            return null;
        }
    }

    public static Task TogglePauseAsync() => RunAsync("command wm-toggle-pause");

    /// <summary>Moves GlazeWM to <paramref name="paused"/>, doing nothing if it's already there.</summary>
    public static async Task SetPausedAsync(bool paused)
    {
        var current = await IsPausedAsync();
        if (current is null || current == paused) return;
        await TogglePauseAsync();
    }

    private static async Task<string?> RunAsync(string arguments)
    {
        try
        {
            using var process = Process.Start(new ProcessStartInfo("glazewm", arguments)
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
            // GlazeWM not installed or not running - auto-pause simply does nothing.
            return null;
        }
    }
}
