using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using HD2MM.Models;
using HD2MM.ViewModels;
using DragEventArgs = System.Windows.DragEventArgs;
using Forms = System.Windows.Forms;

namespace HD2MM;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Closed += (_, _) => _tray?.Dispose();
    }

    /// <summary>Run on the secondary display so automation never touches the main screen.</summary>
    private void PlaceOnTargetScreen()
    {
        try
        {
            var target = Forms.Screen.AllScreens.FirstOrDefault(s => !s.Primary)
                         ?? Forms.Screen.AllScreens.FirstOrDefault();
            if (target is null) return;

            var hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
            if (hwnd == IntPtr.Zero) return;

            if (!GetWindowRect(hwnd, out var current)) return;

            var area = target.WorkingArea;
            var physicalWidth = current.Right - current.Left;
            var ratio = physicalWidth / Width;                 // physical pixels per DIP
            if (ratio <= 0) return;

            // centre the window on the target monitor, converting physical -> DIP
            var centerX = area.Left + area.Width / 2.0;
            var centerY = area.Top + area.Height / 2.0;

            WindowStartupLocation = WindowStartupLocation.Manual;
            Left = centerX / ratio - Width / 2.0;
            Top = centerY / ratio - Height / 2.0;
        }
        catch
        {
            // keep the default position
        }
    }

    private const uint SwpNoSize = 0x0001;
    private const uint SwpNoZOrder = 0x0004;

    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    private struct WinRect
    {
        public int Left, Top, Right, Bottom;
    }

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern bool GetWindowRect(IntPtr hWnd, out WinRect rect);

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr after, int x, int y, int cx, int cy, uint flags);

    private void OnRootBorderSizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (sender is not Border border) return;
        border.Clip = new RectangleGeometry(
            new Rect(0, 0, border.ActualWidth, border.ActualHeight), 8, 8);
    }

    private DispatcherTimer? _scrollHideTimer;

    private void OnWorkAreaScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        if (sender is not ScrollViewer sv) return;
        if (e.VerticalChange == 0 && e.ViewportHeightChange == 0 && e.ExtentHeightChange == 0) return;

        var bar = sv.Template.FindName("PART_VerticalScrollBar", sv) as ScrollBar;
        if (bar is null) return;

        // show
        bar.BeginAnimation(OpacityProperty, null);
        bar.Opacity = 1;

        // shrink the thumb when pinned to the very top / bottom
        var atTop = e.VerticalOffset <= 0.5;
        var atBottom = e.VerticalOffset >= e.ExtentHeight - e.ViewportHeight - 0.5;

        if (bar.Template.FindName("PART_Track", bar) is Track { Thumb: { } thumb })
        {
            thumb.RenderTransformOrigin = new Point(0.5, atTop ? 0 : 1);
            var target = atTop || atBottom ? 0.78 : 1.0;

            if (thumb.RenderTransform is not ScaleTransform scale)
            {
                scale = new ScaleTransform(1, target);
                thumb.RenderTransform = scale;
            }

            scale.BeginAnimation(ScaleTransform.ScaleYProperty,
                new DoubleAnimation(target, TimeSpan.FromMilliseconds(120)));
        }

        // hide after a moment
        _scrollHideTimer ??= new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(1100) };
        _scrollHideTimer.Stop();
        _scrollHideTimer.Tick -= OnScrollHideTick;
        _scrollHideTimer.Tick += OnScrollHideTick;
        _scrollHideTimer.Start();
    }

    private void OnScrollHideTick(object? sender, EventArgs e)
    {
        _scrollHideTimer?.Stop();

        var bar = WorkArea.Template.FindName("PART_VerticalScrollBar", WorkArea) as ScrollBar;
        if (bar is null) return;

        bar.BeginAnimation(OpacityProperty,
            new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(280)));
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        PlaceOnTargetScreen();
        PlayOpenAnimation();

        if (DataContext is MainWindowViewModel vm)
        {
            vm.AutoCheckUpdates();
            vm.RefreshNexusInfo();
        }
    }

    private void PlayOpenAnimation()
    {
        RootBorder.RenderTransformOrigin = new Point(0.5, 0.5);

        var scale = new ScaleTransform(0.94, 0.94);
        RootBorder.RenderTransform = scale;

        var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
        var duration = TimeSpan.FromMilliseconds(240);

        RootBorder.BeginAnimation(OpacityProperty,
            new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(180)));
        scale.BeginAnimation(ScaleTransform.ScaleXProperty,
            new DoubleAnimation(0.94, 1, duration) { EasingFunction = ease });
        scale.BeginAnimation(ScaleTransform.ScaleYProperty,
            new DoubleAnimation(0.94, 1, duration) { EasingFunction = ease });
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        ApplyRoundedCorners();

        if (PresentationSource.FromVisual(this) is System.Windows.Interop.HwndSource source)
            source.AddHook(WndProc);
    }

    private const int WmSysCommand = 0x0112;
    private const int ScMinimize = 0xF020;

    /// <summary>
    /// Intercepts minimize requests (taskbar click, Win+D, system menu) so the
    /// shrink animation plays before the window actually goes down.
    /// </summary>
    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WmSysCommand && (wParam.ToInt64() & 0xFFF0) == ScMinimize)
        {
            handled = true;
            PlayMinimizeAnimation();
        }

        return IntPtr.Zero;
    }

    [System.Runtime.InteropServices.DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

    /// <summary>Windows 11 rounded corners (DWMWCP_ROUND).</summary>
    private void ApplyRoundedCorners()
    {
        try
        {
            var hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
            if (hwnd == IntPtr.Zero) return;

            const int DwmWindowCornerPreference = 33;
            const int DwmWindowCornerRound = 2;
            var pref = DwmWindowCornerRound;
            DwmSetWindowAttribute(hwnd, DwmWindowCornerPreference, ref pref, sizeof(int));
        }
        catch
        {
            // older Windows: keep square corners
        }
    }

    private void OnToggleSidebar(object sender, RoutedEventArgs e)
    {
        var visible = Sidebar.Visibility == Visibility.Visible;
        Sidebar.Visibility = visible ? Visibility.Collapsed : Visibility.Visible;
        SidebarColumn.Width = visible ? new GridLength(0) : new GridLength(210);
    }

    private void OnMinimize(object sender, RoutedEventArgs e) => PlayMinimizeAnimation();

    private bool _wasMinimized;

    private void PlayMinimizeAnimation()
    {
        RootBorder.RenderTransformOrigin = new Point(0.5, 1);
        var scale = new ScaleTransform(1, 1);
        RootBorder.RenderTransform = scale;

        var ease = new QuadraticEase { EasingMode = EasingMode.EaseIn };
        var duration = TimeSpan.FromMilliseconds(130);

        var fade = new DoubleAnimation(1, 0, duration) { EasingFunction = ease };
        fade.Completed += (_, _) =>
        {
            WindowState = WindowState.Minimized;
            RootBorder.BeginAnimation(OpacityProperty, null);
            RootBorder.Opacity = 1;
            RootBorder.RenderTransform = Transform.Identity;
        };

        RootBorder.BeginAnimation(OpacityProperty, fade);
        scale.BeginAnimation(ScaleTransform.ScaleXProperty,
            new DoubleAnimation(1, 0.86, duration) { EasingFunction = ease });
        scale.BeginAnimation(ScaleTransform.ScaleYProperty,
            new DoubleAnimation(1, 0.86, duration) { EasingFunction = ease });
    }

    private void OnWindowStateChanged(object sender, EventArgs e)
    {
        if (WindowState == WindowState.Minimized)
        {
            _wasMinimized = true;
            return;
        }

        // restored from the taskbar (or any other way back to normal)
        if (WindowState != WindowState.Normal || !_wasMinimized) return;
        _wasMinimized = false;

        RootBorder.RenderTransformOrigin = new Point(0.5, 1);
        var scale = new ScaleTransform(0.9, 0.9);
        RootBorder.RenderTransform = scale;

        var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
        var duration = TimeSpan.FromMilliseconds(180);

        RootBorder.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(150)));
        scale.BeginAnimation(ScaleTransform.ScaleXProperty,
            new DoubleAnimation(0.9, 1, duration) { EasingFunction = ease });
        scale.BeginAnimation(ScaleTransform.ScaleYProperty,
            new DoubleAnimation(0.9, 1, duration) { EasingFunction = ease });
    }

    private void OnMaximize(object sender, RoutedEventArgs e) =>
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;

    private Forms.NotifyIcon? _tray;

    private void OnCloseWindow(object sender, RoutedEventArgs e)
    {
        if (DataContext is MainWindowViewModel { CloseToTray: true })
        {
            HideToTray();
            return;
        }

        Close();
    }

    private void HideToTray()
    {
        SetupTray();
        Hide();

        if (_tray is null) return;
        _tray.Visible = true;
        _tray.ShowBalloonTip(1500, "Luna My Master", "已收进后台，双击托盘图标恢复。", Forms.ToolTipIcon.Info);
    }

    private void SetupTray()
    {
        if (_tray is not null) return;

        var exe = Environment.ProcessPath;
        var icon = exe is not null ? System.Drawing.Icon.ExtractAssociatedIcon(exe) : null;

        _tray = new Forms.NotifyIcon
        {
            Icon = icon,
            Text = "Luna My Master",
            Visible = false,
        };

        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add("显示窗口", null, (_, _) => RestoreFromTray());
        menu.Items.Add("退出", null, (_, _) =>
        {
            if (_tray is not null) _tray.Visible = false;
            Application.Current.Shutdown();
        });

        _tray.ContextMenuStrip = menu;
        _tray.DoubleClick += (_, _) => RestoreFromTray();
    }

    private void RestoreFromTray()
    {
        Show();
        WindowState = WindowState.Normal;
        Activate();

        if (_tray is not null) _tray.Visible = false;
    }

    private void OnCardMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount != 2) return;
        if (sender is not FrameworkElement { DataContext: ModItem mod }) return;

        e.Handled = true;
        _ = ShowDetailAsync(mod);
    }

    private async Task ShowDetailAsync(ModItem mod)
    {
        if (DataContext is not MainWindowViewModel vm) return;

        await vm.OpenDetailAsync(mod);

        DetailPanel.Visibility = Visibility.Visible;
        DetailPanel.RenderTransformOrigin = new Point(0.5, 0.5);

        var scale = new ScaleTransform(0.92, 0.92);
        DetailPanel.RenderTransform = scale;

        var ease = new QuadraticEase { EasingMode = EasingMode.EaseOut };
        var duration = TimeSpan.FromMilliseconds(190);

        scale.BeginAnimation(ScaleTransform.ScaleXProperty,
            new DoubleAnimation(0.92, 1, duration) { EasingFunction = ease });
        scale.BeginAnimation(ScaleTransform.ScaleYProperty,
            new DoubleAnimation(0.92, 1, duration) { EasingFunction = ease });
        DetailPanel.BeginAnimation(OpacityProperty,
            new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(160)));
    }

    private void OnCloseDetail(object sender, RoutedEventArgs e) =>
        DetailPanel.Visibility = Visibility.Collapsed;

    private void OnDetailEditNote(object sender, RoutedEventArgs e)
    {
        if (DataContext is MainWindowViewModel { SelectedMod: { } mod } vm)
            vm.EditNote(mod);
    }

    private void OnDetailOpenNexus(object sender, RoutedEventArgs e)
    {
        if (DataContext is MainWindowViewModel { SelectedMod: { NexusId: { } id } })
            Process.Start(new ProcessStartInfo($"https://www.nexusmods.com/helldivers2/mods/{id}")
            {
                UseShellExecute = true,
            });
    }

    private static ModItem? ItemFromMenu(object sender)
    {
        if (sender is not MenuItem item) return null;
        if (item.DataContext is ModItem direct) return direct;

        if (item.Parent is ContextMenu { PlacementTarget: FrameworkElement target } &&
            target.DataContext is ModItem fromTarget)
            return fromTarget;

        return null;
    }

    private void OnMenuEditNote(object sender, RoutedEventArgs e)
    {
        if (ItemFromMenu(sender) is { } mod)
            (DataContext as MainWindowViewModel)?.EditNote(mod);
    }

    private void OnMenuEditOptions(object sender, RoutedEventArgs e)
    {
        if (ItemFromMenu(sender) is { } mod)
            (DataContext as MainWindowViewModel)?.EditOptions(mod);
    }

    private void OnMenuOpenNexus(object sender, RoutedEventArgs e)
    {
        if (ItemFromMenu(sender) is not { NexusId: { } id }) return;

        Process.Start(new ProcessStartInfo($"https://www.nexusmods.com/helldivers2/mods/{id}")
        {
            UseShellExecute = true,
        });
    }

    private void OnMenuDelete(object sender, RoutedEventArgs e)
    {
        if (ItemFromMenu(sender) is { } mod)
            (DataContext as MainWindowViewModel)?.DeleteMod(mod);
    }

    private static bool ArchiveLike(string path) =>
        path.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) ||
        path.EndsWith(".rar", StringComparison.OrdinalIgnoreCase) ||
        path.EndsWith(".7z", StringComparison.OrdinalIgnoreCase);

    private static bool HasArchives(DragEventArgs e) =>
        e.Data.GetDataPresent(DataFormats.FileDrop) &&
        e.Data.GetData(DataFormats.FileDrop) is string[] files &&
        files.Any(ArchiveLike);

    private void OnDragOver(object sender, DragEventArgs e)
    {
        var ok = HasArchives(e);
        e.Effects = ok ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
        DropHint.Visibility = ok ? Visibility.Visible : Visibility.Collapsed;
    }

    private void OnDragLeave(object sender, DragEventArgs e)
    {
        DropHint.Visibility = Visibility.Collapsed;
    }

    private void OnDrop(object sender, DragEventArgs e)
    {
        DropHint.Visibility = Visibility.Collapsed;
        e.Handled = true;

        if (e.Data.GetData(DataFormats.FileDrop) is not string[] files) return;
        (DataContext as MainWindowViewModel)?.ImportFiles(files);
    }
}
