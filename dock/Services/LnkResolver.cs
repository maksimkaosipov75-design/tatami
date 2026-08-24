namespace OmarchyDock.Services;

// Resolves .lnk shortcut targets via the late-bound WScript.Shell COM object
// (no project-level COM reference needed, just Type.GetTypeFromProgID +
// dynamic dispatch - the standard lightweight way to do this from .NET).
internal static class LnkResolver
{
    public static (string TargetPath, string IconLocation)? Resolve(string lnkPath)
    {
        try
        {
            var shellType = Type.GetTypeFromProgID("WScript.Shell");
            if (shellType is null) return null;
            dynamic shell = Activator.CreateInstance(shellType)!;
            dynamic shortcut = shell.CreateShortcut(lnkPath);
            string target = shortcut.TargetPath ?? "";
            string iconLocation = shortcut.IconLocation ?? "";
            return (target, iconLocation);
        }
        catch
        {
            return null;
        }
    }
}
