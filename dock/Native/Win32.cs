using System.Runtime.InteropServices;
using System.Text;

namespace OmarchyDock.Native;

internal static class Win32
{
    public delegate bool EnumWindowsProc(nint hWnd, nint lParam);

    [DllImport("user32.dll")]
    public static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, nint lParam);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    public static extern int GetWindowText(nint hWnd, StringBuilder lpString, int nMaxCount);

    [DllImport("user32.dll")]
    public static extern int GetWindowTextLength(nint hWnd);

    [DllImport("user32.dll")]
    public static extern bool IsWindowVisible(nint hWnd);

    [DllImport("user32.dll")]
    public static extern nint GetAncestor(nint hWnd, uint gaFlags);

    [DllImport("user32.dll")]
    public static extern nint GetWindow(nint hWnd, uint uCmd);

    [DllImport("user32.dll")]
    public static extern int GetWindowLong(nint hWnd, int nIndex);

    [DllImport("user32.dll")]
    public static extern int SetWindowLong(nint hWnd, int nIndex, int dwNewLong);

    [DllImport("dwmapi.dll")]
    public static extern int DwmGetWindowAttribute(nint hwnd, int dwAttribute, out int pvAttribute, int cbAttribute);

    [DllImport("dwmapi.dll")]
    public static extern int DwmExtendFrameIntoClientArea(nint hwnd, ref MARGINS pMarInset);

    [StructLayout(LayoutKind.Sequential)]
    public struct MARGINS
    {
        public int cxLeftWidth;
        public int cxRightWidth;
        public int cyTopHeight;
        public int cyBottomHeight;

        /// <summary>-1 on every side asks DWM to treat the whole client area as glass.</summary>
        public static MARGINS Sheet => new()
        {
            cxLeftWidth = -1,
            cxRightWidth = -1,
            cyTopHeight = -1,
            cyBottomHeight = -1,
        };
    }

    [DllImport("user32.dll")]
    public static extern uint GetWindowThreadProcessId(nint hWnd, out uint lpdwProcessId);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern nint OpenProcess(uint dwDesiredAccess, bool bInheritHandle, uint dwProcessId);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CloseHandle(nint hObject);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool QueryFullProcessImageName(nint hProcess, uint dwFlags, StringBuilder lpExeName, ref uint lpdwSize);

    // PROCESS_QUERY_LIMITED_INFORMATION works across integrity levels, unlike
    // Process.MainModule, which throws "access denied" for elevated or
    // anti-cheat-protected processes - games would silently vanish from the dock.
    private const uint PROCESS_QUERY_LIMITED_INFORMATION = 0x1000;

    public static string? GetProcessImagePath(uint processId)
    {
        if (processId == 0) return null;

        var handle = OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION, false, processId);
        if (handle == 0) return null;

        try
        {
            var capacity = 1024u;
            var buffer = new StringBuilder((int)capacity);
            return QueryFullProcessImageName(handle, 0, buffer, ref capacity)
                ? buffer.ToString()
                : null;
        }
        finally
        {
            CloseHandle(handle);
        }
    }

    [DllImport("user32.dll", SetLastError = true)]
    public static extern nint SendMessageTimeout(nint hWnd, uint msg, nint wParam, nint lParam, uint flags, uint timeout, out nint result);

    [DllImport("user32.dll", EntryPoint = "GetClassLongPtrW")]
    public static extern nint GetClassLongPtr(nint hWnd, int nIndex);

    public const uint WM_GETICON = 0x007F;
    public const nint ICON_SMALL = 0;
    public const nint ICON_BIG = 1;
    public const nint ICON_SMALL2 = 2;
    public const int GCLP_HICON = -14;
    public const int GCLP_HICONSM = -34;
    public const uint SMTO_ABORTIFHUNG = 0x0002;

    /// <summary>
    /// Asks the window itself for its icon. Used when the executable's path or
    /// its icon can't be read - a protected game still answers WM_GETICON.
    /// </summary>
    public static nint GetWindowIconHandle(nint hWnd)
    {
        foreach (var type in new[] { ICON_BIG, ICON_SMALL2, ICON_SMALL })
        {
            if (SendMessageTimeout(hWnd, WM_GETICON, type, 0, SMTO_ABORTIFHUNG, 250, out var handle) != 0
                && handle != 0)
            {
                return handle;
            }
        }

        foreach (var index in new[] { GCLP_HICON, GCLP_HICONSM })
        {
            var handle = GetClassLongPtr(hWnd, index);
            if (handle != 0) return handle;
        }

        return 0;
    }

    [DllImport("user32.dll")]
    public static extern bool ShowWindow(nint hWnd, int nCmdShow);

    [DllImport("user32.dll")]
    public static extern bool SetForegroundWindow(nint hWnd);

    [DllImport("user32.dll")]
    public static extern bool PostMessage(nint hWnd, uint msg, nint wParam, nint lParam);

    public const uint WM_CLOSE = 0x0010;

    [DllImport("user32.dll")]
    public static extern bool IsIconic(nint hWnd);

    [DllImport("user32.dll")]
    public static extern nint GetForegroundWindow();

    [DllImport("user32.dll")]
    public static extern bool GetCursorPos(out POINT lpPoint);

    [DllImport("user32.dll")]
    public static extern bool PrintWindow(nint hwnd, nint hdcBlt, uint nFlags);

    [DllImport("user32.dll")]
    public static extern bool SetLayeredWindowAttributes(nint hwnd, uint crKey, byte bAlpha, uint dwFlags);

    [DllImport("user32.dll")]
    public static extern bool GetWindowRect(nint hWnd, out RECT lpRect);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool SystemParametersInfo(uint uiAction, uint uiParam, ref ANIMATIONINFO pvParam, uint fWinIni);

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    public static extern uint ExtractIconEx(string lpszFile, int nIconIndex, nint[] phiconLarge, nint[]? phiconSmall, uint nIcons);

    [DllImport("user32.dll")]
    public static extern bool DestroyIcon(nint hIcon);

    public delegate void WinEventDelegate(nint hWinEventHook, uint eventType, nint hwnd, int idObject, int idChild, uint dwEventThread, uint dwmsEventTime);

    [DllImport("user32.dll")]
    public static extern nint SetWinEventHook(uint eventMin, uint eventMax, nint hmodWinEventProc, WinEventDelegate lpfnWinEventProc, uint idProcess, uint idThread, uint dwFlags);

    [DllImport("user32.dll")]
    public static extern bool UnhookWinEvent(nint hWinEventHook);

    public const int GWL_EXSTYLE = -20;
    public const int WS_EX_TOOLWINDOW = 0x00000080;
    public const int WS_EX_APPWINDOW = 0x00040000;
    public const int WS_EX_NOACTIVATE = 0x08000000;
    public const int WS_EX_TRANSPARENT = 0x00000020;
    public const int WS_EX_LAYERED = 0x00080000;
    public const uint LWA_ALPHA = 0x00000002;
    public const uint GA_ROOT = 2;
    public const int DWMWA_CLOAKED = 14;
    public const int SW_RESTORE = 9;
    public const int SW_MINIMIZE = 6;

    public const uint EVENT_OBJECT_CREATE = 0x8000;
    public const uint EVENT_OBJECT_DESTROY = 0x8001;
    public const uint EVENT_OBJECT_SHOW = 0x8002;
    public const uint EVENT_OBJECT_HIDE = 0x8003;
    public const uint EVENT_SYSTEM_FOREGROUND = 0x0003;
    public const uint EVENT_SYSTEM_MINIMIZESTART = 0x0016;
    public const uint EVENT_SYSTEM_MINIMIZEEND = 0x0017;
    public const uint WINEVENT_OUTOFCONTEXT = 0x0000;

    // PW_RENDERFULLCONTENT - required to capture DWM-composited windows
    // (anything hardware-accelerated); without it such windows come back blank.
    public const uint PW_RENDERFULLCONTENT = 0x00000002;

    public const uint SPI_GETANIMATION = 0x0048;
    public const uint SPI_SETANIMATION = 0x0049;
    public const uint SPIF_SENDCHANGE = 0x02;

    public struct POINT
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;

        public int Width => Right - Left;
        public int Height => Bottom - Top;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct ANIMATIONINFO
    {
        public uint cbSize;
        public int iMinAnimate;
    }

    /// <summary>
    /// Makes a window invisible without changing its window state, so a tiling
    /// WM keeps it in the layout and doesn't re-flow the other windows. Used to
    /// hide the real window while the genie plays over its slot, so the re-tile
    /// happens once, after the animation, instead of snapping at the start.
    /// Returns false (and does nothing) if the window already manages its own
    /// layered transparency, rather than trampling on it.
    /// </summary>
    public static bool TryPrepareAlphaHide(nint hWnd)
    {
        var exStyle = GetWindowLong(hWnd, GWL_EXSTYLE);
        if ((exStyle & WS_EX_LAYERED) != 0) return false;

        if (SetWindowLong(hWnd, GWL_EXSTYLE, exStyle | WS_EX_LAYERED) == 0) return false;

        // Fully opaque for now: adding the layered style makes DWM rebuild the
        // window's redirection surface, which is expensive. Doing it up-front at
        // alpha 255 is visually a no-op, and keeps that cost out of the
        // animation's first frame where it showed up as a 20-50ms stall.
        return SetLayeredWindowAttributes(hWnd, 0, 255, LWA_ALPHA);
    }

    /// <summary>Cheap alpha-only change, safe to call on an animation frame.</summary>
    public static void SetAlpha(nint hWnd, byte alpha) =>
        SetLayeredWindowAttributes(hWnd, 0, alpha, LWA_ALPHA);

    /// <summary>Undoes <see cref="TryHideByAlpha"/>, putting the window back to fully opaque.</summary>
    public static void UnhideByAlpha(nint hWnd)
    {
        SetLayeredWindowAttributes(hWnd, 0, 255, LWA_ALPHA);
        var exStyle = GetWindowLong(hWnd, GWL_EXSTYLE);
        SetWindowLong(hWnd, GWL_EXSTYLE, exStyle & ~WS_EX_LAYERED);
    }

    /// <summary>
    /// Reads the system minimize/restore animation flag, returning the previous
    /// value so the caller can put it back. GlazeWM's tray toggle writes this
    /// same setting, so it must be restored rather than forced to a fixed value.
    /// </summary>
    public static bool SetMinimizeAnimation(bool enabled)
    {
        var info = new ANIMATIONINFO { cbSize = (uint)Marshal.SizeOf<ANIMATIONINFO>() };
        SystemParametersInfo(SPI_GETANIMATION, info.cbSize, ref info, 0);
        var previous = info.iMinAnimate != 0;

        if (previous != enabled)
        {
            var next = new ANIMATIONINFO
            {
                cbSize = (uint)Marshal.SizeOf<ANIMATIONINFO>(),
                iMinAnimate = enabled ? 1 : 0,
            };
            // No SPIF_UPDATEINIFILE: this is a momentary suppression during our
            // own animation, not a preference change to persist to disk.
            SystemParametersInfo(SPI_SETANIMATION, next.cbSize, ref next, SPIF_SENDCHANGE);
        }

        return previous;
    }

    public static string GetWindowTitle(nint hWnd)
    {
        var length = GetWindowTextLength(hWnd);
        if (length == 0) return string.Empty;
        var sb = new StringBuilder(length + 1);
        GetWindowText(hWnd, sb, sb.Capacity);
        return sb.ToString();
    }

    public static bool IsCloaked(nint hWnd)
    {
        var hr = DwmGetWindowAttribute(hWnd, DWMWA_CLOAKED, out var cloaked, sizeof(int));
        return hr == 0 && cloaked != 0;
    }

    public static bool IsAltTabWindow(nint hWnd)
    {
        if (!IsWindowVisible(hWnd)) return false;
        if (GetWindowTitle(hWnd).Length == 0) return false;
        if (IsCloaked(hWnd)) return false;

        var exStyle = GetWindowLong(hWnd, GWL_EXSTYLE);
        if ((exStyle & WS_EX_TOOLWINDOW) != 0 && (exStyle & WS_EX_APPWINDOW) == 0) return false;

        // Only top-level, non-owned windows count as separate taskbar/dock entries.
        var root = GetAncestor(hWnd, GA_ROOT);
        return root == hWnd;
    }
}
