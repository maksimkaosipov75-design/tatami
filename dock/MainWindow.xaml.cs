using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using OmarchyDock.Models;
using OmarchyDock.Native;
using OmarchyDock.Services;

namespace OmarchyDock;

public partial class MainWindow : Window
{
    private readonly ObservableCollection<DockItem> _items = new();
    private readonly List<PinnedApp> _pinnedApps = PinnedAppsStore.Load();
    private readonly DispatcherTimer _autoHideTimer;
    private readonly DispatcherTimer _refreshDebounce;
    private nint _winEventHook;
    private Win32.WinEventDelegate? _winEventProc; // keep alive - GC would otherwise collect the delegate
    private bool _isDockVisible = true;
    private const double DockHiddenOffset = 90; // px pushed below the screen edge when auto-hidden
    private const double RevealZonePx = 6; // how close to the bottom edge the cursor must get to reveal
    private LaunchpadWindow? _launchpad;

    private readonly DockItem _launchpadItem = new()
    {
        Key = "__launchpad__",
        DisplayName = "Launchpad",
        Icon = GlyphIcons.CreateGridDots((Color)Application.Current.Resources["TextColor"]),
        IsLaunchpad = true,
    };

    public MainWindow()
    {
        InitializeComponent();
        DataContext = _items;

        _autoHideTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(120) };
        _autoHideTimer.Tick += AutoHideTimer_Tick;

        _refreshDebounce = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(200) };
        _refreshDebounce.Tick += (_, _) => { _refreshDebounce.Stop(); RefreshItems(); };

        Loaded += MainWindow_Loaded;
        Closed += MainWindow_Closed;
    }

    private void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        PositionAtBottomCenter();
        RefreshItems();
        RegisterWinEventHook();
        _autoHideTimer.Start();
    }

    private void MainWindow_Closed(object? sender, EventArgs e)
    {
        if (_winEventHook != 0) Win32.UnhookWinEvent(_winEventHook);
    }

    private void PositionAtBottomCenter()
    {
        var workArea = SystemParameters.WorkArea;
        UpdateLayout();
        Left = workArea.Left + (workArea.Width - ActualWidth) / 2;
        Top = workArea.Bottom - ActualHeight - 6;
    }

    // --- Window list + pinned apps merge ---

    private void RefreshItems()
    {
        var running = WindowEnumerator.GetDockableWindows();
        var foreground = Win32.GetForegroundWindow();

        var merged = new List<DockItem>();
        var usedExePaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        merged.Add(_launchpadItem);

        // Pinned apps first, in configured order - keeps the dock layout stable
        // instead of icons jumping around as windows open/close.
        foreach (var pinned in _pinnedApps)
        {
            var match = running.FirstOrDefault(w => string.Equals(w.ExePath, pinned.Path, StringComparison.OrdinalIgnoreCase));
            var icon = IconExtractor.FromFile(pinned.Path);
            if (icon is null) continue;

            merged.Add(new DockItem
            {
                Key = pinned.Path,
                DisplayName = pinned.Name,
                Icon = icon,
                LaunchPath = pinned.Path,
                IsPinned = true,
                IsRunning = match is not null,
                WindowHandle = match?.Handle ?? 0,
                IsForeground = match is not null && match.Handle == foreground,
            });
            usedExePaths.Add(pinned.Path);
        }

        // Then any other running, dockable window not already covered by a pin.
        foreach (var win in running)
        {
            if (usedExePaths.Contains(win.ExePath)) continue;
            var icon = IconExtractor.FromFile(win.ExePath);
            if (icon is null) continue;

            merged.Add(new DockItem
            {
                Key = win.ExePath,
                DisplayName = win.Title,
                Icon = icon,
                IsPinned = false,
                IsRunning = true,
                WindowHandle = win.Handle,
                IsForeground = win.Handle == foreground,
            });
            usedExePaths.Add(win.ExePath);
        }

        _items.Clear();
        foreach (var item in merged) _items.Add(item);

        Dispatcher.InvokeAsync(PositionAtBottomCenter);
    }

    private void ScheduleRefresh()
    {
        _refreshDebounce.Stop();
        _refreshDebounce.Start();
    }

    // --- Click handling ---

    private void DockIcon_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: DockItem item }) return;

        if (item.IsLaunchpad)
        {
            ToggleLaunchpad();
            return;
        }

        if (!item.IsRunning)
        {
            if (item.LaunchPath is not null)
            {
                try
                {
                    Process.Start(new ProcessStartInfo(item.LaunchPath) { UseShellExecute = true });
                }
                catch
                {
                    // Launch target missing/broken - nothing sensible to do from the dock itself.
                }
            }
            return;
        }

        if (item.IsForeground)
        {
            Win32.ShowWindow(item.WindowHandle, Win32.SW_MINIMIZE);
        }
        else
        {
            Win32.ShowWindow(item.WindowHandle, Win32.SW_RESTORE);
            Win32.SetForegroundWindow(item.WindowHandle);
        }
    }

    private void ToggleLaunchpad()
    {
        if (_launchpad is not null)
        {
            _launchpad.Close();
            return;
        }

        _launchpad = new LaunchpadWindow();
        _launchpad.Closed += (_, _) => _launchpad = null;
        _launchpad.Show();
        _launchpad.Activate();
    }

    // --- Live updates via WinEvent hook (window open/close/show/hide/foreground) ---

    private void RegisterWinEventHook()
    {
        _winEventProc = (_, eventType, _, idObject, _, _, _) =>
        {
            // idObject == 0 (OBJID_WINDOW) filters out the flood of non-window
            // accessibility events (menu items, carets, etc.) this hook would
            // otherwise also fire for.
            if (idObject != 0) return;
            ScheduleRefresh();
        };

        _winEventHook = Win32.SetWinEventHook(
            Win32.EVENT_OBJECT_CREATE,
            Win32.EVENT_SYSTEM_MINIMIZEEND,
            0, _winEventProc, 0, 0, Win32.WINEVENT_OUTOFCONTEXT);
    }

    // --- Auto-hide ---

    private void AutoHideTimer_Tick(object? sender, EventArgs e)
    {
        Win32.GetCursorPos(out var cursor);
        var workArea = SystemParameters.WorkArea;

        var nearBottom = cursor.Y >= workArea.Bottom - RevealZonePx - DockHiddenOffset;
        var overDock = cursor.X >= Left && cursor.X <= Left + ActualWidth
                       && cursor.Y >= Top - DockHiddenOffset && cursor.Y <= Top + ActualHeight;

        var shouldShow = nearBottom || overDock;
        if (shouldShow == _isDockVisible) return;

        _isDockVisible = shouldShow;
        var targetY = shouldShow ? 0 : DockHiddenOffset;
        var anim = new DoubleAnimation(targetY, TimeSpan.FromMilliseconds(180))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
        };
        DockSlide.BeginAnimation(TranslateTransform.YProperty, anim);
    }
}
