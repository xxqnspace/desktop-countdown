using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Threading;
using DesktopCountdown.Helpers;
using DesktopCountdown.Models;
using DesktopCountdown.Services;

namespace DesktopCountdown;

/// <summary>
/// 课程提示窗口：显示当前是第几节课、距下课/上课还有多久。
/// 可吸附在倒计时窗口正下方，也可独立摆放。
/// </summary>
public partial class LessonWindow : Window
{
    private const double BaseWidth = 300.0;
    private const double BaseHeight = 84.0;
    private const double FollowGap = 8.0;

    private readonly App _app;
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(1) };
    private readonly bool _usesSystemBackdrop;
    private string _lastPrimary = "";
    private string _lastSecondary = "";

    /// <summary>允许真正关闭（切换背景模式需要重建窗口时置为 true）。</summary>
    internal bool ForceClose { get; set; }

    public LessonWindow(App app)
    {
        _app = app;
        _usesSystemBackdrop = ShouldUseSystemBackdrop(app.Config);

        InitializeComponent();

        if (_usesSystemBackdrop)
        {
            AllowsTransparency = false;
            Background = null;
            SourceInitialized += OnSourceInitialized;
        }

        ContextMenu = BuildContextMenu();
        _timer.Tick += (_, _) => Refresh();
        _timer.Start();
    }

    /// <summary>是否应使用系统亚克力背景（DWM 级别）。</summary>
    public static bool ShouldUseSystemBackdrop(AppConfig config)
    {
        return WindowBackdropHelper.IsSupported && config.Appearance.BackgroundMode == BackgroundMode.Acrylic;
    }

    public bool UsesSystemBackdrop => _usesSystemBackdrop;

    public void ApplyConfig()
    {
        var config = _app.Config;
        var schedule = config.Schedule;

        if (ShouldUseSystemBackdrop(config) != _usesSystemBackdrop)
        {
            _app.RecreateLessonWindow();
            return;
        }

        if (!schedule.FollowWidget)
        {
            Width = Math.Max(schedule.Window.Width, MinWidth);
            Height = Math.Max(schedule.Window.Height, MinHeight);
            Left = schedule.Window.Left;
            Top = schedule.Window.Top;
        }

        Topmost = schedule.Window.Topmost || config.Window.Topmost;
        FontFamily = new System.Windows.Media.FontFamily(config.Appearance.FontFamily);
        Foreground = ColorHelper.BrushFrom(schedule.TextColor, System.Windows.Media.Brushes.Black);

        RootBorder.CornerRadius = new CornerRadius(config.Appearance.CornerRadius);
        RootBorder.BorderBrush = config.Appearance.BorderEnabled
            ? ColorHelper.BrushFrom(config.Appearance.BorderColor, System.Windows.Media.Brushes.Black)
            : new SolidColorBrush(System.Windows.Media.Color.FromArgb(60, 0, 0, 0));
        RootBorder.Background = BuildBackdropBrush(config);
        RootBorder.Effect = _usesSystemBackdrop
            ? null
            : new DropShadowEffect { BlurRadius = Math.Max(4, config.Appearance.BlurRadius), ShadowDepth = 8, Opacity = 0.22 };
        Opacity = config.Appearance.Opacity;

        Refresh();
        UpdateFontSizes();
    }

    /// <summary>吸附到倒计时窗口正下方，并与其同宽。</summary>
    public void FollowTo(WidgetWindow widget)
    {
        Left = widget.Left;
        Top = widget.Top + widget.ActualHeight + FollowGap;

        var targetWidth = Math.Max(widget.ActualWidth, MinWidth);
        if (Math.Abs(Width - targetWidth) > 0.5)
        {
            Width = targetWidth;
            UpdateFontSizes();
        }
    }

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        var config = _app.Config;
        var applied = WindowBackdropHelper.Apply(this, SystemBackdropKind.Acrylic, config.Schedule.BackgroundTint);
        if (!applied)
        {
            // 系统背景不可用时回退为自绘背景，避免出现黑色窗口。
            Background = BuildBackdropBrush(config);
            RootBorder.Background = System.Windows.Media.Brushes.Transparent;
        }
    }

    /// <summary>课程窗口背景：系统亚克力下叠加浅色着色层（保证黑字可读），其余模式复用外观配置。</summary>
    private System.Windows.Media.Brush BuildBackdropBrush(AppConfig config)
    {
        if (_usesSystemBackdrop)
        {
            var tint = ColorHelper.ColorFrom(config.Schedule.BackgroundTint, System.Windows.Media.Color.FromArgb(0xCC, 0xFF, 0xFF, 0xFF));
            tint.A = Math.Min(tint.A, (byte)0xE6);
            return new SolidColorBrush(tint);
        }

        return AppearanceBrushFactory.Create(config.Appearance, false);
    }

    private void Refresh()
    {
        var state = ScheduleService.Evaluate(_app.Config.Schedule, DateTime.Now);

        if (!string.Equals(state.Primary, _lastPrimary, StringComparison.Ordinal))
        {
            PrimaryText.Text = state.Primary;
            _lastPrimary = state.Primary;
        }

        if (!string.Equals(state.Secondary, _lastSecondary, StringComparison.Ordinal))
        {
            SecondaryText.Text = state.Secondary;
            _lastSecondary = state.Secondary;
        }
    }

    private void UpdateFontSizes()
    {
        var scaleX = ActualWidth / BaseWidth;
        var scaleY = ActualHeight / BaseHeight;
        var scale = Math.Max(0.6, Math.Min(3.0, Math.Min(scaleX, scaleY)));

        PrimaryText.FontSize = Math.Max(10, 15 * scale);
        SecondaryText.FontSize = Math.Max(13, 24 * scale);
    }

    private ContextMenu BuildContextMenu()
    {
        var menu = new ContextMenu();

        var openSettings = new MenuItem { Header = "打开设置" };
        openSettings.Click += (_, _) => _app.ShowSettings();
        menu.Items.Add(openSettings);

        var hideLesson = new MenuItem { Header = "隐藏课程窗口" };
        hideLesson.Click += (_, _) => _app.HideLesson();
        menu.Items.Add(hideLesson);

        menu.Items.Add(new Separator());

        var exit = new MenuItem { Header = "退出应用" };
        exit.Click += (_, _) => _app.ExitApplication();
        menu.Items.Add(exit);
        return menu;
    }

    private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (!_app.Config.Window.Locked && !_app.Config.Schedule.FollowWidget)
        {
            DragMove();
        }
    }

    private void Window_LocationChanged(object? sender, EventArgs e)
    {
        if (_app.Config.Schedule.FollowWidget)
        {
            return;
        }

        _app.Config.Schedule.Window.Left = Left;
        _app.Config.Schedule.Window.Top = Top;
        _app.SaveConfig();
    }

    private void Window_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        UpdateFontSizes();

        if (_app.Config.Schedule.FollowWidget)
        {
            return;
        }

        _app.Config.Schedule.Window.Width = Width;
        _app.Config.Schedule.Window.Height = Height;
        _app.SaveConfig();
    }

    private void Window_Closing(object sender, CancelEventArgs e)
    {
        if (_app.IsExiting || ForceClose)
        {
            return;
        }

        e.Cancel = true;
        _app.HideLesson();
    }
}
