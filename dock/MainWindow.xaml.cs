using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
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
    // Mutable: the context menu and drag-to-reorder edit this list and write it
    // straight back to pinned.json.
    private readonly List<PinnedApp> _pinnedApps = PinnedAppsStore.Load();
    private readonly DispatcherTimer _autoHideTimer;
    private readonly DispatcherTimer _refreshDebounce;
    private readonly DispatcherTimer _taskbarWatchdog;
    private readonly DockSettings _settings = DockSettings.Load();
    private nint _winEventHook;
    private nint _objectEventHook;
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

        // Windows re-shows the taskbar by itself, so hiding has to be enforced
        // rather than done once. Two seconds is slow enough to be free and fast
        // enough that a reappearance is barely noticeable.
        _taskbarWatchdog = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
        _taskbarWatchdog.Tick += (_, _) =>
        {
            if (_settings.HideWindowsTaskbar) TaskbarController.EnforceHidden();

            // Safety net: if the pause state ever drifts from what we want -
            // a missed transition, a CLI call that failed - this puts it back.
            // No-op when they already agree.
            if (_settings.AutoPauseTilingInFullscreen) _ = SyncPauseStateAsync();
        };

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

        Diagnostics.Log($"settings: hideTaskbar={_settings.HideWindowsTaskbar} autoPause={_settings.AutoPauseTilingInFullscreen}");

        if (_settings.HideWindowsTaskbar) TaskbarController.Hide();

        // Always running: it carries both the taskbar enforcement and the
        // pause-state safety net, and each is a no-op when not needed.
        _taskbarWatchdog.Start();
    }

    private void MainWindow_Closed(object? sender, EventArgs e)
    {
        if (_winEventHook != 0) Win32.UnhookWinEvent(_winEventHook);
        if (_objectEventHook != 0) Win32.UnhookWinEvent(_objectEventHook);

        // Always give the taskbar back on the way out. If the dock stops - a
        // crash, an update, the user quitting it - leaving the machine with no
        // taskbar and no dock would strand them with no shell UI at all.
        _taskbarWatchdog.Stop();
        if (_settings.HideWindowsTaskbar) TaskbarController.Show();

        // Same reasoning: never leave the machine in a state only this app knows
        // how to undo. If we paused tiling for a fullscreen app, resume it.
        if (_pausedByUs)
        {
            _pausedByUs = false;
            GlazeWmController.SetPausedAsync(false).GetAwaiter().GetResult();
        }
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
            // Protected/elevated processes expose no image path; key those by
            // window handle so they still get an entry instead of disappearing.
            var key = win.ExePath ?? $"hwnd:{win.Handle}";
            if (usedExePaths.Contains(key)) continue;

            var icon = (win.ExePath is not null ? IconExtractor.FromFile(win.ExePath) : null)
                       ?? IconExtractor.FromWindow(win.Handle);
            if (icon is null) continue;

            merged.Add(new DockItem
            {
                Key = key,
                DisplayName = win.Title,
                Icon = icon,
                IsPinned = false,
                IsRunning = true,
                WindowHandle = win.Handle,
                IsForeground = win.Handle == foreground,
            });
            usedExePaths.Add(key);
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
        if (_dragOccurred)
        {
            _dragOccurred = false;
            return;
        }

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

    // --- Pinning ---

    private void TogglePin_Click(object sender, RoutedEventArgs e)
    {
        if (ResolveItem(sender) is not { } item || item.IsLaunchpad) return;

        if (item.IsPinned) Unpin(item);
        else Pin(item);
    }

    public void Pin(DockItem item)
    {
        if (item.IsLaunchpad) return;
        if (IsOwnExecutable(item.Key)) return;
        if (_pinnedApps.Any(p => string.Equals(p.Path, item.Key, StringComparison.OrdinalIgnoreCase))) return;

        // DisplayName for a running, unpinned entry is the *window title*, which
        // would pin Firefox as "(73) ... - YouTube - Mozilla Firefox". Pins need
        // the application's own name instead.
        _pinnedApps.Add(new PinnedApp { Name = AppNameFor(item.Key), Path = item.Key });
        PersistPins();
    }

    /// <summary>The dock pinning itself would be meaningless, and its entry's
    /// menu could shut the dock down. Refuse regardless of how it got here.</summary>
    private static bool IsOwnExecutable(string path)
    {
        try
        {
            var self = Environment.ProcessPath;
            return self is not null && string.Equals(
                Path.GetFullPath(path), Path.GetFullPath(self), StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    private static string AppNameFor(string exePath)
    {
        try
        {
            var description = FileVersionInfo.GetVersionInfo(exePath).FileDescription;
            if (!string.IsNullOrWhiteSpace(description)) return description.Trim();
        }
        catch
        {
            // Unreadable metadata - fall through to the filename.
        }

        return Path.GetFileNameWithoutExtension(exePath);
    }

    public void PinPath(string path, string name)
    {
        if (IsOwnExecutable(path)) return;
        if (_pinnedApps.Any(p => string.Equals(p.Path, path, StringComparison.OrdinalIgnoreCase))) return;

        _pinnedApps.Add(new PinnedApp { Name = name, Path = path });
        PersistPins();
    }

    private void Unpin(DockItem item)
    {
        _pinnedApps.RemoveAll(p => string.Equals(p.Path, item.Key, StringComparison.OrdinalIgnoreCase));
        PersistPins();
    }

    private void PersistPins()
    {
        PinnedAppsStore.Save(_pinnedApps);
        RefreshItems();
    }

    /// <summary>
    /// The menu lives in a DataTemplate, so there's one instance per icon and no
    /// x:Name to bind against - sync the checkmark as each one opens instead.
    /// </summary>
    private void DockMenu_Opened(object sender, RoutedEventArgs e)
    {
        if (sender is not ContextMenu menu) return;

        var dockItem = (menu.PlacementTarget as FrameworkElement)?.Tag as DockItem;
        var processName = dockItem is not null ? ProcessNameFor(dockItem) : null;

        foreach (var item in menu.Items.OfType<MenuItem>())
        {
            switch (item.Tag as string)
            {
                case "taskbar":
                    item.IsChecked = _settings.HideWindowsTaskbar;
                    break;
                case "notiling":
                    item.IsEnabled = processName is not null;
                    item.IsChecked = processName is not null && GlazeWmConfig.IsIgnored(processName);
                    break;
            }
        }
    }

    private void ToggleTaskbar_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem item) return;

        _settings.HideWindowsTaskbar = item.IsChecked;
        _settings.Save();

        if (_settings.HideWindowsTaskbar)
        {
            TaskbarController.Hide();
            _taskbarWatchdog.Start();
        }
        else
        {
            _taskbarWatchdog.Stop();
            TaskbarController.Show();
        }
    }

    private void CloseWindow_Click(object sender, RoutedEventArgs e)
    {
        if (ResolveItem(sender) is not { } item) return;
        if (!item.IsRunning || item.WindowHandle == 0) return;

        // Belt and braces: the enumerator already excludes our own windows, but
        // never let this path close the dock itself.
        Win32.GetWindowThreadProcessId(item.WindowHandle, out var pid);
        if (pid == (uint)Environment.ProcessId) return;

        Win32.PostMessage(item.WindowHandle, Win32.WM_CLOSE, 0, 0);
        ScheduleRefresh();
    }

    /// <summary>Digs the DockItem out of whichever element raised the event.</summary>
    private static DockItem? ResolveItem(object sender) => sender switch
    {
        FrameworkElement { Tag: DockItem tagged } => tagged,
        FrameworkElement { DataContext: DockItem context } => context,
        _ => null,
    };

    // --- Drag to reorder ---

    private Point _dragStart;
    private DockItem? _dragCandidate;

    // A drag ends with a mouse-up over an icon, which would otherwise also read
    // as a click and minimize the window the user was only rearranging.
    private bool _dragOccurred;

    // WPF's built-in DragDrop gives no in-place feedback - icons would simply
    // jump after the drop. This is a manual drag instead, modelled on the macOS
    // dock: the grabbed icon lifts and follows the cursor while its neighbours
    // slide aside to open a gap, and it settles into that gap on release.

    private const double LiftScale = 1.25;
    private static readonly Duration SlideDuration = new(TimeSpan.FromMilliseconds(160));

    private int _dragIndex = -1;
    private int _dragTargetIndex = -1;
    private double _slotWidth;

    private void DockIcon_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        _dragStart = e.GetPosition(ItemsHost);
        _dragCandidate = ResolveItem(sender);
    }

    private void Dock_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (e.LeftButton != System.Windows.Input.MouseButtonState.Pressed)
        {
            _dragCandidate = null;
            return;
        }

        var position = e.GetPosition(ItemsHost);

        if (_dragIndex < 0)
        {
            if (_dragCandidate is null || _dragCandidate.IsLaunchpad) return;
            if (Math.Abs(position.X - _dragStart.X) < SystemParameters.MinimumHorizontalDragDistance) return;
            BeginDrag(_dragCandidate);
            if (_dragIndex < 0) return;
        }

        var dx = position.X - _dragStart.X;
        SetTranslate(ContainerAt(_dragIndex), dx);

        var target = Math.Clamp(
            _dragIndex + (int)Math.Round(dx / _slotWidth),
            FirstReorderableIndex,
            _items.Count - 1);

        if (target == _dragTargetIndex) return;
        _dragTargetIndex = target;
        AnimateGap();
    }

    private void BeginDrag(DockItem item)
    {
        var index = _items.IndexOf(item);
        if (index < FirstReorderableIndex) return;

        var container = ContainerAt(index);
        if (container is null || container.ActualWidth <= 0) return;

        _dragIndex = index;
        _dragTargetIndex = index;
        _slotWidth = container.ActualWidth;
        _dragOccurred = true;

        // Keep the dock on screen for the whole gesture - the auto-hide timer
        // would otherwise slide it away mid-drag when the cursor strays.
        _autoHideTimer.Stop();

        Panel.SetZIndex(container, 10);
        container.Opacity = 0.9;
        SetScale(container, LiftScale, animate: true);
    }

    /// <summary>Shifts the icons between the grabbed slot and the insertion point aside.</summary>
    private void AnimateGap()
    {
        for (var i = FirstReorderableIndex; i < _items.Count; i++)
        {
            if (i == _dragIndex) continue;

            double offset = 0;
            if (_dragIndex < _dragTargetIndex && i > _dragIndex && i <= _dragTargetIndex) offset = -_slotWidth;
            else if (_dragIndex > _dragTargetIndex && i >= _dragTargetIndex && i < _dragIndex) offset = _slotWidth;

            AnimateTranslate(ContainerAt(i), offset);
        }
    }

    private void Dock_MouseUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        _dragCandidate = null;
        if (_dragIndex < 0) return;

        var dragged = _items[_dragIndex];
        var from = _dragIndex;
        var to = _dragTargetIndex;
        var container = ContainerAt(from);

        _dragIndex = -1;
        _dragTargetIndex = -1;
        _autoHideTimer.Start();

        if (container is not null)
        {
            Panel.SetZIndex(container, 0);
            container.Opacity = 1;
            SetScale(container, 1, animate: true);

            // Glide into the gap rather than snapping, then commit once the
            // motion has finished so the list rebuild isn't visible.
            AnimateTranslate(container, (to - from) * _slotWidth, () => CommitReorder(dragged, to));
        }
        else
        {
            CommitReorder(dragged, to);
        }
    }

    private void CommitReorder(DockItem dragged, int targetIndex)
    {
        ClearDragTransforms();

        // A merely-running app has no stored position, so dropping it pins it -
        // the same thing dragging a running app into the macOS dock does.
        if (!dragged.IsPinned) Pin(dragged);

        var from = IndexOfPin(dragged.Key);
        if (from < 0) return;

        // Dock indices include the Launchpad and any unpinned windows, so map
        // the drop position onto the pinned list by counting pinned entries.
        var to = Math.Clamp(PinnedIndexForDockIndex(targetIndex), 0, _pinnedApps.Count - 1);
        if (from == to)
        {
            RefreshItems();
            return;
        }

        var moved = _pinnedApps[from];
        _pinnedApps.RemoveAt(from);
        _pinnedApps.Insert(to, moved);
        PersistPins();
    }

    private int PinnedIndexForDockIndex(int dockIndex)
    {
        var pinnedSeen = 0;
        for (var i = FirstReorderableIndex; i < _items.Count && i <= dockIndex; i++)
        {
            if (_items[i].IsPinned) pinnedSeen++;
        }
        return Math.Max(0, pinnedSeen - 1);
    }

    private void ClearDragTransforms()
    {
        for (var i = 0; i < _items.Count; i++)
        {
            var container = ContainerAt(i);
            if (container is null) continue;

            container.BeginAnimation(OpacityProperty, null);
            container.Opacity = 1;
            Panel.SetZIndex(container, 0);
            SetTranslate(container, 0);
            SetScale(container, 1, animate: false);
        }
    }

    /// <summary>Index 0 is the Launchpad button, which is fixed in place.</summary>
    private const int FirstReorderableIndex = 1;

    private ContentPresenter? ContainerAt(int index) =>
        index >= 0 && index < _items.Count
            ? ItemsHost.ItemContainerGenerator.ContainerFromIndex(index) as ContentPresenter
            : null;

    private static TransformGroup EnsureTransform(ContentPresenter container)
    {
        if (container.RenderTransform is TransformGroup existing) return existing;

        var group = new TransformGroup();
        group.Children.Add(new TranslateTransform());
        group.Children.Add(new ScaleTransform(1, 1));
        container.RenderTransform = group;
        container.RenderTransformOrigin = new Point(0.5, 1);
        return group;
    }

    private static void SetTranslate(ContentPresenter? container, double x)
    {
        if (container is null) return;
        var translate = (TranslateTransform)EnsureTransform(container).Children[0];
        translate.BeginAnimation(TranslateTransform.XProperty, null);
        translate.X = x;
    }

    private static void AnimateTranslate(ContentPresenter? container, double x, Action? onCompleted = null)
    {
        if (container is null)
        {
            onCompleted?.Invoke();
            return;
        }

        var translate = (TranslateTransform)EnsureTransform(container).Children[0];
        var animation = new DoubleAnimation(x, SlideDuration)
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
        };
        if (onCompleted is not null) animation.Completed += (_, _) => onCompleted();
        translate.BeginAnimation(TranslateTransform.XProperty, animation);
    }

    private static void SetScale(ContentPresenter? container, double scale, bool animate)
    {
        if (container is null) return;
        var transform = (ScaleTransform)EnsureTransform(container).Children[1];

        if (!animate)
        {
            transform.BeginAnimation(ScaleTransform.ScaleXProperty, null);
            transform.BeginAnimation(ScaleTransform.ScaleYProperty, null);
            transform.ScaleX = transform.ScaleY = scale;
            return;
        }

        var animation = new DoubleAnimation(scale, new Duration(TimeSpan.FromMilliseconds(120)))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
        };
        transform.BeginAnimation(ScaleTransform.ScaleXProperty, animation);
        transform.BeginAnimation(ScaleTransform.ScaleYProperty, animation.Clone());
    }

    private int IndexOfPin(string key) =>
        _pinnedApps.FindIndex(p => string.Equals(p.Path, key, StringComparison.OrdinalIgnoreCase));

    // --- Auto-pause tiling for fullscreen apps ---

    private bool _wantsPause;      // desired state: is a fullscreen app focused?
    private bool _pausedByUs;      // actual state: did we pause GlazeWM?
    private bool _syncInFlight;

    /// <summary>
    /// GlazeWM re-tiles a window the moment it goes fullscreen, which throws a
    /// game straight back out of it, and upstream has no auto-pause. Pausing the
    /// WM while a fullscreen app is focused solves it for every app at once,
    /// instead of needing a hand-written ignore rule per game.
    /// </summary>
    private void HandleFullscreenChange(bool fullscreen)
    {
        if (fullscreen == _wantsPause) return;
        _wantsPause = fullscreen;
        if (_settings.AutoPauseTilingInFullscreen) _ = SyncPauseStateAsync();
    }

    /// <summary>
    /// Drives GlazeWM's pause towards <see cref="_wantsPause"/>.
    ///
    /// Talking to GlazeWM means spawning its CLI, which takes long enough that
    /// the fullscreen state can flip mid-call. An earlier version set the
    /// "we paused it" flag only after that call returned, so a flip in that
    /// window was missed and GlazeWM stayed paused forever - after which every
    /// later fullscreen saw "already paused" and did nothing, which is what
    /// made this work only every other time. Re-reading the desired state after
    /// each step, with one conversation at a time, closes that race.
    /// </summary>
    private async Task SyncPauseStateAsync()
    {
        if (_syncInFlight) return;
        _syncInFlight = true;

        try
        {
            while (_wantsPause != _pausedByUs)
            {
                var desired = _wantsPause;

                if (desired)
                {
                    // Leave a pause the user set themselves with Alt+Shift+P alone.
                    if (await GlazeWmController.IsPausedAsync() is not false) return;
                    await GlazeWmController.TogglePauseAsync();
                    _pausedByUs = true;
                    Diagnostics.Log("fullscreen detected - paused GlazeWM");
                }
                else
                {
                    await GlazeWmController.TogglePauseAsync();
                    _pausedByUs = false;
                    Diagnostics.Log("fullscreen ended - resumed GlazeWM");
                }
            }
        }
        finally
        {
            _syncInFlight = false;
        }
    }

    // --- Excluding an app from tiling, chosen explicitly by the user ---

    /// <summary>
    /// Adds or removes a GlazeWM ignore rule for the clicked app's process.
    ///
    /// This is deliberately a manual choice rather than something detected.
    /// GlazeWM offers no way to tell a game apart from any other window, and an
    /// earlier attempt to infer it from "went fullscreen, then got resized while
    /// still focused" matched every ordinary app launch instead - with the
    /// taskbar hidden the work area equals the screen, so a new window briefly
    /// covers it before being tiled. Guessing wrong here silently breaks tiling
    /// for a normal app, so the user picks.
    /// </summary>
    private async void ToggleTiling_Click(object sender, RoutedEventArgs e)
    {
        if (ResolveItem(sender) is not { } item || item.IsLaunchpad) return;
        if (item.WindowHandle == 0) return;

        var processName = ProcessNameFor(item);
        if (string.IsNullOrWhiteSpace(processName)) return;

        if (GlazeWmConfig.IsIgnored(processName))
        {
            await GlazeWmConfig.RemoveIgnoreAsync(processName);
            MessageBox.Show(
                $"\"{processName}\" is managed by GlazeWM again.",
                "OmarchyDock", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (!await GlazeWmConfig.AddIgnoreAsync(processName)) return;

        MessageBox.Show(
            $"GlazeWM will no longer manage \"{processName}\", so fullscreen will stick.\n\n" +
            "Restart the app for this to take effect - the rule applies to windows opened from now on.",
            "OmarchyDock", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private static string? ProcessNameFor(DockItem item)
    {
        if (item.WindowHandle != 0)
        {
            Win32.GetWindowThreadProcessId(item.WindowHandle, out var pid);
            var path = Win32.GetProcessImagePath(pid);
            if (path is not null) return Path.GetFileNameWithoutExtension(path);
        }

        return item.Key.StartsWith("hwnd:", StringComparison.Ordinal)
            ? null
            : Path.GetFileNameWithoutExtension(item.Key);
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

        // Two hooks, because the events we need sit in two separate ranges and
        // SetWinEventHook takes a single contiguous one. This was previously a
        // single call with min=EVENT_OBJECT_CREATE (0x8000) and
        // max=EVENT_SYSTEM_MINIMIZEEND (0x0017) - an inverted range that matches
        // nothing, so the hook never fired and the dock only ever showed the
        // windows that existed when it started.
        _winEventHook = Win32.SetWinEventHook(
            Win32.EVENT_SYSTEM_FOREGROUND,
            Win32.EVENT_SYSTEM_MINIMIZEEND,
            0, _winEventProc, 0, 0, Win32.WINEVENT_OUTOFCONTEXT);

        _objectEventHook = Win32.SetWinEventHook(
            Win32.EVENT_OBJECT_CREATE,
            Win32.EVENT_OBJECT_HIDE,
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

        // Stay out of the way of fullscreen apps. The dock is topmost, so
        // without this it draws over a fullscreen game even when hidden-by-hover
        // would otherwise reveal it.
        var fullscreen = Win32.IsForegroundWindowFullscreen();
        HandleFullscreenChange(fullscreen);

        var shouldShow = (nearBottom || overDock) && !fullscreen;
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
