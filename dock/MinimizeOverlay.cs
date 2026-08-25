using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using System.Windows.Threading;
using OmarchyDock.Native;
using OmarchyDock.Services;

namespace OmarchyDock;

/// <summary>
/// Draws the minimize/restore effect: a captured snapshot of the window is
/// textured onto a grid mesh which is then deformed toward the dock icon.
/// Windows exposes no way to restyle its own minimize animation, so the real
/// one is suppressed for the duration and this is drawn on top instead.
///
/// The style picks the deformation - see <see cref="MinimizeAnimation"/> - and
/// with it the mesh resolution. Only Genie bends within itself and needs a fine
/// grid; the others move the quad as a whole, where four corners are exact and
/// anything more is wasted vertex work.
/// </summary>
internal sealed class MinimizeOverlay : Window
{
    // How far ahead of the top edge the bottom edge starts moving in Genie.
    // Higher = longer tail, more pronounced funnel.
    private const double RowLag = 0.55;

    // Vortex: full turns at the end of the spin, and how much more the bottom
    // rows turn than the top ones - the difference is what wrings the shape
    // rather than turning it rigidly.
    private const double TwistTurns = 1.0;
    private const double TwistShear = 0.6;

    private readonly int _columns;
    private readonly int _rows;
    private readonly MinimizeAnimation _style;

    private readonly MeshGeometry3D _mesh = new();
    // Both stored relative to the overlay's own origin, since the overlay no
    // longer spans the whole screen.
    private readonly Rect _sourceRect;   // window bounds, DIPs
    private readonly Rect _targetRect;   // dock icon bounds, DIPs
    private readonly bool _reverse;      // restore instead of minimize
    private readonly TimeSpan _duration;
    private readonly Action? _onCompleted;
    private Action? _onFirstFrame;

    private Viewport3D _viewport = null!;
    private DateTime _startedAt;
    private bool _finished;
    private int _frameCount;

    // Per-frame instrumentation: separates CPU spent building the mesh from the
    // wall-clock gap between frames, so a slow frame can be attributed to our
    // own work rather than to compositing/vsync.
    private readonly System.Diagnostics.Stopwatch _clock = System.Diagnostics.Stopwatch.StartNew();
    private double _meshMsTotal;
    private double _lastFrameMs;
    private double _worstGapMs;

    private MinimizeOverlay(
        ImageSource capture,
        Rect sourceRect,
        Rect targetRect,
        Rect screenRect,
        MinimizeAnimation style,
        bool reverse,
        TimeSpan duration,
        Action? onCompleted,
        Action? onFirstFrame)
    {
        _style = style;
        (_columns, _rows) = style switch
        {
            MinimizeAnimation.Genie => (10, 48),
            // Sheared per row, so it needs rows - but far fewer than the tail.
            MinimizeAnimation.Vortex => (8, 24),
            // Affine: a single quad reproduces these exactly.
            _ => (2, 2),
        };

        _sourceRect = Rect.Offset(sourceRect, -screenRect.Left, -screenRect.Top);
        _targetRect = Rect.Offset(targetRect, -screenRect.Left, -screenRect.Top);
        _reverse = reverse;
        _duration = duration;
        _onCompleted = onCompleted;
        _onFirstFrame = onFirstFrame;

        // Deliberately NOT AllowsTransparency: that turns the window into a
        // layered window and drops the whole thing to software rendering, which
        // at full-screen size caps the animation around 30fps. Transparency is
        // instead done by extending the DWM frame across the client area (see
        // OnSourceInitialized), which keeps GPU compositing.
        WindowStyle = WindowStyle.None;
        AllowsTransparency = false;
        Background = Brushes.Transparent;
        Topmost = true;
        ShowInTaskbar = false;
        ResizeMode = ResizeMode.NoResize;
        IsHitTestVisible = false;

        Left = screenRect.Left;
        Top = screenRect.Top;
        Width = screenRect.Width;
        Height = screenRect.Height;

        _viewport = BuildViewport(capture, screenRect.Width, screenRect.Height);
        Content = _viewport;
        BuildMeshTopology();
        UpdateMesh(0);

        Loaded += (_, _) =>
        {
            _startedAt = DateTime.UtcNow;
            CompositionTarget.Rendering += OnRendering;
        };
    }

    /// <summary>
    /// Plays the effect for an already-captured window snapshot. The caller owns
    /// the capture so the same snapshot taken at minimize time can be replayed
    /// in reverse on restore - a minimized window can't be captured, and
    /// restoring it first just to grab pixels would make it pop into view before
    /// the animation had a chance to run.
    /// </summary>
    public static void Play(
        Services.CapturedWindow captured,
        Rect targetRectPhysical,
        MinimizeAnimation style,
        bool reverse,
        TimeSpan duration,
        Action? onCompleted,
        Action? onFirstFrame = null,
        Win32.RECT? sourceOverride = null)
    {
        var dpi = VisualTreeHelper.GetDpi(Application.Current.MainWindow);
        var scaleX = dpi.DpiScaleX;
        var scaleY = dpi.DpiScaleY;

        // On restore the window may come back into a different tiling slot than
        // it left, so the caller can supply the window's current bounds instead
        // of the ones baked into the snapshot.
        var bounds = sourceOverride ?? captured.Bounds;

        var source = new Rect(
            bounds.Left / scaleX,
            bounds.Top / scaleY,
            bounds.Width / scaleX,
            bounds.Height / scaleY);

        var target = new Rect(
            targetRectPhysical.Left / scaleX,
            targetRectPhysical.Top / scaleY,
            targetRectPhysical.Width / scaleX,
            targetRectPhysical.Height / scaleY);

        // Cover only the area the animation actually touches, not the whole
        // desktop. A full-screen transparent topmost window forces DWM to
        // recomposite every pixel behind it each frame; measured on a 2560x1440
        // 165Hz iGPU that alone pushed frames past the 6.06ms budget and halved
        // the effective rate to every second vblank.
        var union = Rect.Union(source, target);
        union.Inflate(24, 24);

        var virtualScreen = new Rect(
            SystemParameters.VirtualScreenLeft,
            SystemParameters.VirtualScreenTop,
            SystemParameters.VirtualScreenWidth,
            SystemParameters.VirtualScreenHeight);
        union.Intersect(virtualScreen);

        var screen = union;

        var overlay = new MinimizeOverlay(
            captured.Image, source, target, screen, style, reverse, duration, onCompleted, onFirstFrame);
        overlay.Show();
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);

        // Click-through and never focusable: the overlay is pure decoration and
        // must not intercept the mouse or steal activation mid-animation.
        var handle = new WindowInteropHelper(this).Handle;
        var exStyle = Win32.GetWindowLong(handle, Win32.GWL_EXSTYLE);
        Win32.SetWindowLong(handle, Win32.GWL_EXSTYLE,
            exStyle | Win32.WS_EX_TRANSPARENT | Win32.WS_EX_NOACTIVATE | Win32.WS_EX_TOOLWINDOW);

        // Hardware-accelerated transparency: let WPF render with an alpha
        // background and have DWM composite it, instead of the software path
        // AllowsTransparency would force.
        var source = HwndSource.FromHwnd(handle);
        if (source?.CompositionTarget is not null)
        {
            source.CompositionTarget.BackgroundColor = Colors.Transparent;
        }

        var margins = Win32.MARGINS.Sheet;
        Win32.DwmExtendFrameIntoClientArea(handle, ref margins);
    }

    private Viewport3D BuildViewport(ImageSource capture, double width, double height)
    {
        // Orthographic camera mapped 1:1 to DIPs, with world Y pointing up, so a
        // screen point (x, y) is simply (x, -y, 0) in world space.
        var camera = new OrthographicCamera
        {
            Position = new Point3D(width / 2, -height / 2, 10),
            LookDirection = new Vector3D(0, 0, -1),
            UpDirection = new Vector3D(0, 1, 0),
            Width = width,
        };

        var brush = new ImageBrush(capture) { Stretch = Stretch.Fill };
        // Linear filtering rather than WPF's high-quality resampler: the texture
        // is in constant motion and shrinking, so the extra sampling cost buys
        // nothing visible.
        RenderOptions.SetBitmapScalingMode(brush, BitmapScalingMode.LowQuality);
        brush.Freeze();

        var model = new Model3DGroup();
        // Ambient white light only - the texture should render at its captured
        // brightness rather than being shaded by a directional light.
        model.Children.Add(new AmbientLight(Colors.White));
        model.Children.Add(new GeometryModel3D
        {
            Geometry = _mesh,
            Material = new DiffuseMaterial(brush),
            // No BackMaterial on purpose: the mesh never turns away from the
            // camera, and setting one makes WPF rasterize back faces too -
            // doubling fragment work on a fill-rate-bound iGPU for nothing.
        });

        return new Viewport3D
        {
            Camera = camera,
            Children = { new ModelVisual3D { Content = model } },
            Width = width,
            Height = height,
        };
    }

    private void BuildMeshTopology()
    {
        var textureCoords = new PointCollection();
        var indices = new Int32Collection();

        for (var r = 0; r < _rows; r++)
        {
            var v = (double)r / (_rows - 1);
            for (var c = 0; c < _columns; c++)
            {
                var u = (double)c / (_columns - 1);
                textureCoords.Add(new Point(u, v));
            }
        }

        for (var r = 0; r < _rows - 1; r++)
        {
            for (var c = 0; c < _columns - 1; c++)
            {
                var topLeft = r * _columns + c;
                var topRight = topLeft + 1;
                var bottomLeft = topLeft + _columns;
                var bottomRight = bottomLeft + 1;

                indices.Add(topLeft);
                indices.Add(bottomLeft);
                indices.Add(topRight);

                indices.Add(topRight);
                indices.Add(bottomLeft);
                indices.Add(bottomRight);
            }
        }

        _mesh.TextureCoordinates = textureCoords;
        _mesh.TriangleIndices = indices;
        _mesh.Positions = new Point3DCollection(_rows * _columns);
        for (var i = 0; i < _rows * _columns; i++) _mesh.Positions.Add(new Point3D());
    }

    private void UpdateMesh(double t)
    {
        // Detach before mutating: assigning into an attached Point3DCollection
        // makes WPF re-validate the mesh on every single vertex write. Detach,
        // write every vertex, reattach once - and reuse the same collection so
        // the render loop doesn't allocate per frame.
        var positions = _mesh.Positions;
        _mesh.Positions = null;

        switch (_style)
        {
            case MinimizeAnimation.Genie: BuildGenie(positions, t); break;
            case MinimizeAnimation.Vortex: BuildVortex(positions, t); break;
            case MinimizeAnimation.Drop: BuildDrop(positions, t); break;
            default: BuildShrink(positions, t); break;
        }

        _mesh.Positions = positions;
    }

    /// <summary>Rows funnel in bottom-first, so the shape necks down into a tail.</summary>
    private void BuildGenie(Point3DCollection positions, double t)
    {
        var srcCenterX = _sourceRect.Left + _sourceRect.Width / 2;
        var srcHalfWidth = _sourceRect.Width / 2;
        var dstY = _targetRect.Top + _targetRect.Height / 2;
        var dstCenterX = _targetRect.Left + _targetRect.Width / 2;
        var dstHalfWidth = _targetRect.Width / 2;

        var index = 0;
        for (var r = 0; r < _rows; r++)
        {
            var v = (double)r / (_rows - 1); // 0 = top of window, 1 = bottom

            // Rows closer to the bottom start their journey sooner; that
            // staggering is what forms the tail instead of a uniform shrink.
            var rowStart = (1 - v) * RowLag;
            var e = SmoothStep(Clamp01((t - rowStart) / (1 - RowLag)));

            var y = Lerp(_sourceRect.Top + v * _sourceRect.Height, dstY, e);
            var centerX = Lerp(srcCenterX, dstCenterX, e);
            var halfWidth = Lerp(srcHalfWidth, dstHalfWidth, e);

            WriteRow(positions, ref index, centerX, halfWidth, y);
        }
    }

    /// <summary>
    /// Wrung out: the window spins a full turn as it shrinks toward the icon.
    ///
    /// Two things make that possible near a bottom-edge dock, where a rotating
    /// shape would otherwise be sliced off by the screen. The size runs ahead
    /// of the travel, so the shape is already small while still high on screen
    /// with room to turn; and the spin is gated on geometry - it is allowed
    /// only once the shape's circumscribed circle fits inside the overlay
    /// around its current centre, at which point no angle can push a corner
    /// out. The turn completes at 80% and holds there, so the last frame is
    /// square with the icon rather than caught mid-rotation.
    /// </summary>
    private void BuildVortex(Point3DCollection positions, double t)
    {
        var e = SmoothStep(t);
        var size = Math.Pow(e, 0.5);
        var travel = Math.Pow(e, 1.6);

        var centerX = Lerp(_sourceRect.Left + _sourceRect.Width / 2, _targetRect.Left + _targetRect.Width / 2, travel);
        var centerY = Lerp(_sourceRect.Top + _sourceRect.Height / 2, _targetRect.Top + _targetRect.Height / 2, travel);
        var halfWidth = Lerp(_sourceRect.Width / 2, _targetRect.Width / 2, size);
        var halfHeight = Lerp(_sourceRect.Height / 2, _targetRect.Height / 2, size);

        // Distance from the centre to the nearest overlay edge. The overlay was
        // already clipped to the visible screen, so this is the real room.
        var room = Math.Min(Math.Min(centerX, Width - centerX), Math.Min(centerY, Height - centerY));
        var reach = double.Hypot(halfWidth, halfHeight);
        var gate = room > 0 ? Clamp01((room - reach) / (0.15 * room)) : 0;

        var spin = SmoothStep(Clamp01(e / 0.8)) * TwistTurns * 2 * Math.PI * gate;

        var index = 0;
        for (var r = 0; r < _rows; r++)
        {
            var v = (double)r / (_rows - 1);

            // One sin/cos per row rather than per vertex - the shear only
            // varies down the window, never across it. It unwinds as the
            // animation ends so the shape lands flat instead of skewed.
            var angle = spin * (1 + (v - 0.5) * TwistShear * (1 - e));
            var sin = Math.Sin(angle);
            var cos = Math.Cos(angle);

            var localY = (v - 0.5) * 2 * halfHeight;

            for (var c = 0; c < _columns; c++)
            {
                var localX = ((double)c / (_columns - 1) - 0.5) * 2 * halfWidth;
                positions[index++] = new Point3D(
                    centerX + localX * cos - localY * sin,
                    -(centerY + localX * sin + localY * cos),
                    0);
            }
        }
    }

    /// <summary>An eased shrink straight into the icon - no bending, no spin.</summary>
    private void BuildShrink(Point3DCollection positions, double t)
    {
        var e = SmoothStep(t);

        var top = Lerp(_sourceRect.Top, _targetRect.Top, e);
        var height = Lerp(_sourceRect.Height, _targetRect.Height, e);
        var centerX = Lerp(_sourceRect.Left + _sourceRect.Width / 2, _targetRect.Left + _targetRect.Width / 2, e);
        var halfWidth = Lerp(_sourceRect.Width / 2, _targetRect.Width / 2, e);

        var index = 0;
        for (var r = 0; r < _rows; r++)
        {
            var v = (double)r / (_rows - 1);
            WriteRow(positions, ref index, centerX, halfWidth, top + v * height);
        }
    }

    /// <summary>
    /// Falls into the dock: the bottom edge accelerates away first and the top
    /// edge is left to catch up, so the window folds shut against the icon
    /// instead of scaling down. Driving the bottom edge - rather than the top,
    /// with height trailing - is what keeps it from overshooting past the dock
    /// and being clipped by the overlay's rect on the way.
    /// </summary>
    private void BuildDrop(Point3DCollection positions, double t)
    {
        var lead = t * t;
        var trail = SmoothStep(Clamp01((t - 0.25) / 0.75));

        var bottom = Lerp(_sourceRect.Bottom, _targetRect.Bottom, lead);
        var top = Lerp(_sourceRect.Top, _targetRect.Top, trail);
        var centerX = Lerp(_sourceRect.Left + _sourceRect.Width / 2, _targetRect.Left + _targetRect.Width / 2, SmoothStep(t));
        var halfWidth = Lerp(_sourceRect.Width / 2, _targetRect.Width / 2, trail);

        var index = 0;
        for (var r = 0; r < _rows; r++)
        {
            var v = (double)r / (_rows - 1);
            WriteRow(positions, ref index, centerX, halfWidth, Lerp(top, bottom, v));
        }
    }

    /// <summary>Lays one horizontal run of vertices at a fixed height and width.</summary>
    private void WriteRow(Point3DCollection positions, ref int index, double centerX, double halfWidth, double y)
    {
        for (var c = 0; c < _columns; c++)
        {
            var x = centerX + ((double)c / (_columns - 1) - 0.5) * 2 * halfWidth;
            positions[index++] = new Point3D(x, -y, 0);
        }
    }

    private void OnRendering(object? sender, EventArgs e)
    {
        var elapsed = DateTime.UtcNow - _startedAt;
        var raw = Clamp01(elapsed.TotalMilliseconds / _duration.TotalMilliseconds);
        var t = _reverse ? 1 - raw : raw;

        _frameCount++;

        var nowMs = _clock.Elapsed.TotalMilliseconds;
        if (_frameCount > 1)
        {
            var gap = nowMs - _lastFrameMs;
            if (gap > _worstGapMs) _worstGapMs = gap;
        }
        _lastFrameMs = nowMs;

        var meshStart = _clock.Elapsed.TotalMilliseconds;
        UpdateMesh(t);
        _meshMsTotal += _clock.Elapsed.TotalMilliseconds - meshStart;

        // Only fade on the way out. On restore the final frame lines up exactly
        // with the real window, so fading there would punch a hole between the
        // overlay disappearing and the window painting - the "blink".
        // Applied to the content, not the Window: Window.Opacity would need a
        // layered window and undo the hardware acceleration above.
        if (!_reverse)
        {
            const double fadeFrom = 0.85;
            _viewport.Opacity = raw > fadeFrom ? (1 - raw) / (1 - fadeFrom) : 1;
        }

        if (_frameCount == 1)
        {
            // Hand back control only once something is actually on screen. The
            // caller minimizes the real window here, so it can't vanish before
            // the overlay has painted over it.
            _onFirstFrame?.Invoke();
            _onFirstFrame = null;
        }

        if (raw < 1) return;

        Finish(elapsed);
    }

    private void Finish(TimeSpan elapsed)
    {
        if (_finished) return;
        _finished = true;

        CompositionTarget.Rendering -= OnRendering;

        var fps = elapsed.TotalSeconds > 0 ? _frameCount / elapsed.TotalSeconds : 0;
        var avgFrameMs = _frameCount > 0 ? elapsed.TotalMilliseconds / _frameCount : 0;
        var avgMeshMs = _frameCount > 0 ? _meshMsTotal / _frameCount : 0;
        Diagnostics.Log(
            $"{_style} {(_reverse ? "restore" : "minimize")}: {_frameCount}f {fps:F1}fps " +
            $"avgFrame={avgFrameMs:F2}ms avgMesh={avgMeshMs:F2}ms worstGap={_worstGapMs:F1}ms " +
            $"overlay={Width:F0}x{Height:F0} ({Width * Height / 1_000_000:F2}Mpx) mesh={_columns}x{_rows}");

        _onCompleted?.Invoke();

        if (!_reverse)
        {
            Close();
            return;
        }

        // Restoring: onCompleted has just un-minimized the real window, but it
        // needs a few frames to actually paint. Hold the finished (still fully
        // opaque) last frame over it until then, otherwise there's a gap where
        // neither is drawn.
        var hold = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(90) };
        hold.Tick += (_, _) =>
        {
            hold.Stop();
            Close();
        };
        hold.Start();
    }

    private static double Clamp01(double value) => value < 0 ? 0 : value > 1 ? 1 : value;
    private static double Lerp(double a, double b, double t) => a + (b - a) * t;
    private static double SmoothStep(double t) => t * t * (3 - 2 * t);
}
