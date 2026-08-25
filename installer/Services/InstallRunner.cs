using System.Diagnostics;
using System.IO;
using OmarchySetup.Models;

namespace OmarchySetup.Services;

/// <summary>
/// Drives the install by unpacking the payload and then running the same
/// PowerShell scripts the project already uses, rather than reimplementing
/// their logic in C# - one source of truth, and the scripts stay usable on
/// their own.
/// </summary>
internal class InstallRunner
{
    private readonly Action<string> _log;
    private readonly string _dotfiles;

    public InstallRunner(Action<string> log)
    {
        _log = log;
        _dotfiles = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "dotfiles");
    }

    public async Task RunAsync(IReadOnlyList<Component> components)
    {
        var selected = components.Where(c => c.IsSelected).ToList();
        if (selected.Count == 0)
        {
            _log("Nothing selected - nothing to do.");
            return;
        }

        await Task.Run(() =>
        {
            if (!HasWinget())
            {
                _log("winget (App Installer) was not found.");
                _log("Everything here is bootstrapped through it, so install App Installer from the");
                _log("Microsoft Store first, then run this again: https://aka.ms/getwinget");
                return;
            }

            BackUpExistingDotfiles();
            Payload.ExtractTo(_dotfiles, _log);

            _log("");
            _log("=== Prerequisites (PowerShell 7, git, scoop) ===");
            RunScript("00-preflight.ps1");

            var packages = selected.SelectMany(c => c.Packages).Distinct().ToArray();
            var wantsFont = selected.Any(c => c.Id == "font");

            if (packages.Length > 0 || wantsFont)
            {
                _log("");
                _log($"=== Packages: {(packages.Length > 0 ? string.Join(", ", packages) : "none")} ===");
                var packageArg = packages.Length > 0
                    ? "-Packages " + string.Join(",", packages.Select(p => $"'{p}'"))
                    : "";
                RunScript("01-packages.ps1", $"{packageArg} -InstallFont ${wantsFont}");
            }

            _log("");
            _log("=== Linking configs ===");
            RunScript("02-link-configs.ps1");

            if (selected.Any(c => c.Id == "tweaks"))
            {
                _log("");
                _log("=== Windows tweaks ===");
                RunScript("03-windows-tweaks.ps1");
            }

            if (selected.Any(c => c.Id == "dock"))
            {
                _log("");
                _log("=== OmarchyDock ===");
                InstallDock();
            }

            if (selected.Any(c => c.Id == "hidetaskbar"))
            {
                _log("");
                _log("=== Hiding the Windows taskbar ===");
                RunScript("05-taskbar.ps1", "-Hide");
            }

            _log("");
            _log("Done. Sign out and back in so the autostart entries take effect.");
        });
    }

    /// <summary>
    /// Unpacking overwrites files in %USERPROFILE%\dotfiles. If something is
    /// already there - a previous install, or somebody else's dotfiles that
    /// happen to live at the same path - copy it aside first rather than
    /// silently destroying it.
    /// </summary>
    private void BackUpExistingDotfiles()
    {
        if (!Directory.Exists(_dotfiles)) return;
        if (!Directory.EnumerateFileSystemEntries(_dotfiles).Any()) return;

        var backup = $"{_dotfiles}.backup-{DateTime.Now:yyyyMMdd-HHmmss}";
        _log($"{_dotfiles} already exists - backing it up to {backup}");

        try
        {
            CopyDirectory(_dotfiles, backup);
            _log("Backup complete.");
        }
        catch (Exception ex)
        {
            _log($"Backup failed ({ex.Message}). Stopping rather than overwriting your files.");
            throw;
        }
    }

    private static void CopyDirectory(string source, string destination)
    {
        Directory.CreateDirectory(destination);

        foreach (var file in Directory.EnumerateFiles(source))
        {
            File.Copy(file, Path.Combine(destination, Path.GetFileName(file)), overwrite: true);
        }

        foreach (var directory in Directory.EnumerateDirectories(source))
        {
            var name = Path.GetFileName(directory);
            // Skip build output and VCS metadata: large, regenerable, and full
            // of locked files that would fail the copy for no benefit.
            if (name is ".git" or "bin" or "obj" or "publish" or "dist") continue;

            var info = new DirectoryInfo(directory);
            if (info.LinkTarget is not null) continue; // don't follow symlinks out of the tree

            CopyDirectory(directory, Path.Combine(destination, name));
        }
    }

    private void InstallDock()
    {
        var dockDir = Path.Combine(_dotfiles, "dock");
        var exe = Path.Combine(dockDir, "OmarchyDock.exe");

        if (!File.Exists(exe))
        {
            _log("OmarchyDock binaries were not bundled into this installer - skipping.");
            return;
        }

        // The dock is framework-dependent, so the runtime has to be present even
        // though this installer itself is self-contained.
        if (!IsDesktopRuntimeInstalled())
        {
            _log("Installing the .NET 8 Desktop Runtime via winget...");
            Run("winget", "install --id Microsoft.DotNet.DesktopRuntime.8 -e --accept-source-agreements --accept-package-agreements --silent");
        }

        var startup = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.Startup),
            "OmarchyDock.lnk");

        if (!File.Exists(startup))
        {
            CreateShortcut(startup, exe);
            _log($"Autostart shortcut created: {startup}");
        }

        Process.Start(new ProcessStartInfo(exe) { UseShellExecute = true });
        _log("OmarchyDock started.");
    }

    private static bool HasWinget()
    {
        try
        {
            using var process = Process.Start(new ProcessStartInfo("winget", "--version")
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            });
            if (process is null) return false;
            process.WaitForExit(10_000);
            return process.HasExited && process.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    private static bool IsDesktopRuntimeInstalled()
    {
        var root = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            "dotnet", "shared", "Microsoft.WindowsDesktop.App");

        return Directory.Exists(root)
               && Directory.EnumerateDirectories(root, "8.*").Any();
    }

    private static void CreateShortcut(string shortcutPath, string target)
    {
        var shellType = Type.GetTypeFromProgID("WScript.Shell");
        if (shellType is null) return;
        dynamic shell = Activator.CreateInstance(shellType)!;
        dynamic shortcut = shell.CreateShortcut(shortcutPath);
        shortcut.TargetPath = target;
        shortcut.Save();
    }

    private void RunScript(string scriptName, string arguments = "")
    {
        var script = Path.Combine(_dotfiles, "scripts", scriptName);
        if (!File.Exists(script))
        {
            _log($"Script missing, skipped: {script}");
            return;
        }

        // powershell.exe (5.1) is guaranteed present on a fresh Win10/11 box;
        // 00-preflight installs pwsh 7 but can't be run by it.
        var shell = scriptName == "00-preflight.ps1" ? "powershell.exe" : PreferPwsh();
        Run(shell, $"-NoLogo -NoProfile -ExecutionPolicy Bypass -File \"{script}\" {arguments}");
    }

    private static string PreferPwsh()
    {
        var pwsh = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            "PowerShell", "7", "pwsh.exe");
        return File.Exists(pwsh) ? pwsh : "powershell.exe";
    }

    private void Run(string fileName, string arguments)
    {
        var info = new ProcessStartInfo(fileName, arguments)
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };

        try
        {
            using var process = Process.Start(info);
            if (process is null)
            {
                _log($"Could not start: {fileName}");
                return;
            }

            process.OutputDataReceived += (_, e) => { if (e.Data is not null) _log(e.Data); };
            process.ErrorDataReceived += (_, e) => { if (e.Data is not null) _log(e.Data); };
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            process.WaitForExit();

            if (process.ExitCode != 0) _log($"(exit code {process.ExitCode})");
        }
        catch (Exception ex)
        {
            _log($"FAILED: {ex.Message}");
        }
    }
}
