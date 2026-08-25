using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Interop;
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
    private static readonly TimeSpan GenieDuration = TimeSpan.FromMilliseconds(420);

    // Snapshot taken when a window is minimized through the dock, replayed in
    // reverse when it's restored. Keyed by hwnd; entries are dropped on restore.
    private readonly Dictionary<nint, CapturedWindow> _captureCache = new();
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

        // Longer than Windows' own minimize/restore animation (~200-250ms) on
        // purpose: refreshing mid-animation made the dock shuffle its icons
        // while the window was still shrinking, which read as stutter. Settling
        // afterwards costs a little latency but looks like one motion.
        _refreshDebounce = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(350) };
        _refreshDebounce.Tick += (_, _) => { _refreshDebounce.Stop(); RefreshItems(); };

        Loaded += MainWindow_Loaded;
        Closed += MainWindow_Closed;
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);

        // WS_EX_NOACTIVATE: clicking the dock must not steal focus from the
        // window being clicked, the way the real taskbar behaves. Without it,
        // the dock itself becomes the foreground window on mouse-down, so by
        // the time the click handler runs, "is this item's window focused?"
        // is already false and clicking the active app would restore it
        // instead of minimizing it.
        var hwnd = new WindowInteropHelper(this).Handle;
        var exStyle = Win32.GetWindowLong(hwnd, Win32.GWL_EXSTYLE);
        Win32.SetWindowLong(hwnd, Win32.GWL_EXSTYLE, exStyle | Win32.WS_EX_NOACTIVATE);
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

    private void PositionAtBottomCenter(bool animate = false)
    {
        var workArea = SystemParameters.WorkArea;
        UpdateLayout();

        var targetLeft = workArea.Left + (workArea.Width - ActualWidth) / 2;
        Top = workArea.Bottom - ActualHeight - 6;

        // Adding/removing an icon changes the dock's width, which would snap it
        // to a new centered position. Easing the shift instead keeps the dock
        // from jumping while a window is still playing its minimize animation.
        if (!animate || Math.Abs(Left - targetLeft) < 1)
        {
            BeginAnimation(LeftProperty, null);
            Left = targetLeft;
            return;
        }

        var slide = new DoubleAnimation(targetLeft, TimeSpan.FromMilliseconds(200))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
            FillBehavior = FillBehavior.Stop,
        };
        slide.Completed += (_, _) =>
        {
            BeginAnimation(LeftProperty, null);
            Left = targetLeft;
        };
        BeginAnimation(LeftProperty, slide);
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

        ApplyItems(merged);
        Dispatcher.InvokeAsync(() => PositionAtBottomCenter(animate: true));
    }

    // Reconcile in place rather than Clear()+re-add: rebuilding the whole
    // ItemsControl on every window event made all icons tear down and re-render
    // at once, so minimizing one window visibly flickered the entire dock.
    // Updating matched items by key leaves their visuals (and hover state)
    // untouched, and only genuinely added/removed icons change.
    private void ApplyItems(List<DockItem> desired)
    {
        for (var i = 0; i < desired.Count; i++)
        {
            var want = desired[i];
            var existingIndex = IndexOfKey(want.Key);

            if (existingIndex < 0)
            {
                _items.Insert(i, want);
                continue;
            }

            if (existingIndex != i) _items.Move(existingIndex, i);

            var current = _items[i];
            current.DisplayName = want.DisplayName;
            current.WindowHandle = want.WindowHandle;
            current.IsRunning = want.IsRunning;
            current.IsForeground = want.IsForeground;
        }

        // Anything past the desired range is stale: the loop above already put
        // every desired item at its final index, in order.
        while (_items.Count > desired.Count)
        {
            _items.RemoveAt(_items.Count - 1);
        }
    }

    private int IndexOfKey(string key)
    {
        for (var i = 0; i < _items.Count; i++)
        {
            if (string.Equals(_items[i].Key, key, StringComparison.OrdinalIgnoreCase)) return i;
        }
        return -1;
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

        // Read the foreground window now rather than trusting item.IsForeground,
        // which is only as fresh as the last (debounced) refresh.
        var isActive = Win32.GetForegroundWindow() == item.WindowHandle
                       && !Win32.IsIconic(item.WindowHandle);

        var iconRect = GetIconScreenRect(sender as FrameworkElement);

        if (isActive)
        {
            MinimizeWithGenie(item.WindowHandle, iconRect);
        }
        else
        {
            RestoreWithGenie(item.WindowHandle, iconRect);
        }
    }

    /// <summary>Screen rect (physical px) of the clicked dock icon - the point the genie funnels into.</summary>
    private static Rect GetIconScreenRect(FrameworkElement? element)
    {
        if (element is null) return Rect.Empty;
        var topLeft = element.PointToScreen(new Point(0, 0));
        var bottomRight = element.PointToScreen(new Point(element.ActualWidth, element.ActualHeight));
        return new Rect(topLeft, bottomRight);
    }

    private void MinimizeWithGenie(nint hwnd, Rect iconRect)
    {
        // Capture while the window is still on screen - a minimized window has
        // nothing to capture. The same snapshot is kept for the restore
        // animation, since by then the window is gone from the screen.
        var captured = iconRect.IsEmpty ? null : WindowCapture.Capture(hwnd);

        if (captured is null)
        {
            Win32.ShowWindow(hwnd, Win32.SW_MINIMIZE);
            return;
        }

        _captureCache[hwnd] = captured;

        var previousAnimation = Win32.SetMinimizeAnimation(false);

        // Add the layered style now, while still opaque, so DWM's surface
        // rebuild happens before the animation rather than stalling its first
        // frame. The actual hide is then just an alpha write.
        var hiddenByAlpha = Win32.TryPrepareAlphaHide(hwnd);

        GenieOverlay.Play(
            captured,
            iconRect,
            reverse: false,
            duration: GenieDuration,
            onCompleted: () =>
            {
                // Minimize for real only now. That's what makes GlazeWM re-flow
                // the layout, so the re-tile lands after the genie instead of
                // snapping at the start with the animation playing over it.
                Win32.ShowWindow(hwnd, Win32.SW_MINIMIZE);
                if (hiddenByAlpha) Win32.UnhideByAlpha(hwnd);
                Win32.SetMinimizeAnimation(previousAnimation);
            },
            onFirstFrame: () =>
            {
                // Hide the real window without changing its state, once the
                // overlay is actually painted over it. Alpha-hiding keeps it in
                // the tiling layout (no re-flow yet) while the genie animates
                // its slot away; a plain minimize here would re-tile instantly.
                if (hiddenByAlpha)
                {
                    Win32.SetAlpha(hwnd, 0);
                }
                else
                {
                    // Window manages its own transparency - fall back to the
                    // old behaviour rather than fighting it.
                    Win32.ShowWindow(hwnd, Win32.SW_MINIMIZE);
                }
            });
    }

    private void RestoreWithGenie(nint hwnd, Rect iconRect)
    {
        if (iconRect.IsEmpty || !_captureCache.TryGetValue(hwnd, out var captured))
        {
            // Never minimized through the dock (so no snapshot exists) - restore
            // plainly rather than inventing a half-broken animation.
            Win32.ShowWindow(hwnd, Win32.SW_RESTORE);
            Win32.SetForegroundWindow(hwnd);
            return;
        }

        var previousAnimation = Win32.SetMinimizeAnimation(false);

        // Put the window back into the layout up-front but invisible, so GlazeWM
        // re-tiles first and the genie can then expand into the slot the window
        // actually ends up in. Restoring at the end instead would animate into a
        // space that doesn't exist yet, and the layout would jump afterwards.
        var hiddenByAlpha = Win32.TryPrepareAlphaHide(hwnd);
        if (hiddenByAlpha) Win32.SetAlpha(hwnd, 0);
        Win32.ShowWindow(hwnd, Win32.SW_RESTORE);
        Win32.SetForegroundWindow(hwnd);

        // GlazeWM re-tiles asynchronously in its own process, so the window's
        // final bounds aren't known immediately after restoring - reading them
        // right here would return the pre-tile rect. Wait a beat, then animate
        // into wherever it actually landed.
        var settle = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(70) };
        settle.Tick += (_, _) =>
        {
            settle.Stop();

            Win32.RECT? currentBounds = Win32.GetWindowRect(hwnd, out var rect) ? rect : null;

            GenieOverlay.Play(
                captured,
                iconRect,
                reverse: true,
                duration: GenieDuration,
                onCompleted: () =>
                {
                    if (hiddenByAlpha) Win32.UnhideByAlpha(hwnd);
                    Win32.SetMinimizeAnimation(previousAnimation);
                    _captureCache.Remove(hwnd);
                },
                sourceOverride: currentBounds);
        };
        settle.Start();
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
