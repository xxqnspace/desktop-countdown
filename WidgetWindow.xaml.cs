using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using DesktopCountdown.Helpers;
using DesktopCountdown.Models;
using DesktopCountdown.Services;

namespace DesktopCountdown;

public partial class WidgetWindow : Window
{
    private readonly App _app;
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(1) };
    private readonly bool _usesSystemBackdrop;
    private bool _hasNotifiedEnd;
    private readonly ObservableCollection<CountdownSegment> _countdownSegments = new();

    /// <summary>允许真正关闭（切换背景模式需要重建窗口时置为 true）。</summary>
    internal bool ForceClose { get; set; }

    public WidgetWindow(App app)
    {
        _app = app;
        _usesSystemBackdrop = LessonWindow.ShouldUseSystemBackdrop(app.Config);

        InitializeComponent();

        if (_usesSystemBackdrop)
        {
            // 系统亚克力要求窗口为普通窗口（非 layered），且窗口自身不绘制背景。
            AllowsTransparency = false;
            Background = null;
            SourceInitialized += OnSourceInitialized;
        }

        CountdownItems.ItemsSource = _countdownSegments;
        _timer.Tick += (_, _) => RefreshCountdown();
        _timer.Start();

        ContextMenu = BuildContextMenu();
    }

    private const double BaseWidth = 300.0;
    private const double BaseHeight = 150.0;

    public bool UsesSystemBackdrop => _usesSystemBackdrop;

    public void ApplyConfig()
    {
        var config = _app.Config;

        if (LessonWindow.ShouldUseSystemBackdrop(config) != _usesSystemBackdrop)
        {
            _app.RecreateWidget();
            return;
        }

        Width = Math.Max(config.Window.Width, MinWidth);
        Height = Math.Max(config.Window.Height, MinHeight);
        Left = config.Window.Left;
        Top = config.Window.Top;
        Topmost = config.Window.Topmost;

        TitleText.Text = config.Countdown.Title;
        TargetText.Text = config.Countdown.TargetDateTime.LocalDateTime.ToString("yyyy-MM-dd HH:mm:ss");
        FontFamily = new System.Windows.Media.FontFamily(config.Appearance.FontFamily);
        Foreground = ColorHelper.BrushFrom(config.Appearance.TextColor, System.Windows.Media.Brushes.White);

        RootBorder.CornerRadius = new CornerRadius(config.Appearance.CornerRadius);
        RootBorder.BorderBrush = config.Appearance.BorderEnabled ? ColorHelper.BrushFrom(config.Appearance.BorderColor, System.Windows.Media.Brushes.White) : System.Windows.Media.Brushes.Transparent;
        RootBorder.Background = AppearanceBrushFactory.Create(config.Appearance, _usesSystemBackdrop);
        RootBorder.Effect = _usesSystemBackdrop
            ? null
            : new DropShadowEffect { BlurRadius = Math.Max(4, config.Appearance.BlurRadius), ShadowDepth = 8, Opacity = 0.22 };
        Opacity = config.Appearance.Opacity;

        RefreshCountdown();
        UpdateFontSizes();
    }

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        var appearance = _app.Config.Appearance;
        var applied = WindowBackdropHelper.Apply(this, SystemBackdropKind.Acrylic, appearance.AcrylicTintColor);
        if (!applied)
        {
            Background = AppearanceBrushFactory.Create(appearance, false);
            RootBorder.Background = System.Windows.Media.Brushes.Transparent;
        }
    }

    private void RefreshCountdown()
    {
        var config = _app.Config;
        var remaining = config.Countdown.TargetDateTime - DateTimeOffset.Now;
        if (remaining <= TimeSpan.Zero)
        {
            UpdateSegments(new[] { new CountdownSegment("0", config.Countdown.EndText) });
            if (!_hasNotifiedEnd && config.Countdown.NotifyOnEnd)
            {
                _hasNotifiedEnd = true;
                System.Windows.Forms.MessageBox.Show(config.Countdown.EndText, "倒计时结束");
            }
        }
        else
        {
            _hasNotifiedEnd = false;
            UpdateSegments(CountdownFormatter.Format(config.Countdown.TargetDateTime, config.DisplayUnits));
        }
    }

    private void UpdateSegments(IReadOnlyList<CountdownSegment> newSegments)
    {
        if (newSegments.Count != _countdownSegments.Count)
        {
            _countdownSegments.Clear();
            foreach (var seg in newSegments)
            {
                _countdownSegments.Add(seg);
            }
            Dispatcher.BeginInvoke(UpdateFontSizes, DispatcherPriority.Render);
            return;
        }

        bool anyUnitChanged = false;
        for (int i = 0; i < newSegments.Count; i++)
        {
            if (_countdownSegments[i].Unit != newSegments[i].Unit)
            {
                anyUnitChanged = true;
                break;
            }
        }

        if (anyUnitChanged)
        {
            _countdownSegments.Clear();
            foreach (var seg in newSegments)
            {
                _countdownSegments.Add(seg);
            }
            Dispatcher.BeginInvoke(UpdateFontSizes, DispatcherPriority.Render);
            return;
        }

        for (int i = 0; i < newSegments.Count; i++)
        {
            _countdownSegments[i].Value = newSegments[i].Value;
        }
    }

    private ContextMenu BuildContextMenu()
    {
        var menu = new ContextMenu();
        var openSettings = new MenuItem { Header = "打开设置" };
        openSettings.Click += (_, _) => _app.ShowSettings();
        menu.Items.Add(openSettings);

        var hide = new MenuItem { Header = "隐藏悬浮窗" };
        hide.Click += (_, _) => _app.HideWidget();
        menu.Items.Add(hide);

        var topmost = new MenuItem { Header = "始终置顶", IsCheckable = true };
        topmost.SetBinding(MenuItem.IsCheckedProperty, new System.Windows.Data.Binding(nameof(Topmost)) { Source = this });
        topmost.Click += (_, _) =>
        {
            _app.Config.Window.Topmost = topmost.IsChecked;
            ApplyConfig();
            _app.SaveConfig();
        };
        menu.Items.Add(topmost);

        var locked = new MenuItem { Header = "锁定位置", IsCheckable = true, IsChecked = _app.Config.Window.Locked };
        locked.Click += (_, _) =>
        {
            _app.Config.Window.Locked = locked.IsChecked;
            _app.SaveConfig();
        };
        menu.Items.Add(locked);

        menu.Items.Add(new Separator());
        var exit = new MenuItem { Header = "退出应用" };
        exit.Click += (_, _) => _app.ExitApplication();
        menu.Items.Add(exit);
        return menu;
    }

    private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (!_app.Config.Window.Locked)
        {
            DragMove();
        }
    }

    private void Window_LocationChanged(object? sender, EventArgs e)
    {
        _app.Config.Window.Left = Left;
        _app.Config.Window.Top = Top;
        _app.SyncLessonWindowPosition();
        _app.SaveConfig();
    }

    private void Window_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        _app.Config.Window.Width = Width;
        _app.Config.Window.Height = Height;
        _app.SyncLessonWindowPosition();
        _app.SaveConfig();
        UpdateFontSizes();
    }

    private void UpdateFontSizes()
    {
        double scaleX = ActualWidth / BaseWidth;
        double scaleY = ActualHeight / BaseHeight;
        double scale = Math.Min(scaleX, scaleY);
        scale = Math.Max(scale, 0.6);
        scale = Math.Min(scale, 3.0);

        TitleText.FontSize = Math.Max(10, 15 * scale);
        TargetText.FontSize = Math.Max(8, 12 * scale);

        foreach (var tb in VisualTreeHelperExtensions.FindVisualChildren<TextBlock>(CountdownItems))
        {
            if (tb.FontWeight == FontWeights.Bold)
            {
                tb.FontSize = Math.Max(16, 32 * scale);
            }
            else
            {
                tb.FontSize = Math.Max(8, 12 * scale);
            }
        }
    }

    private void Window_Closing(object sender, CancelEventArgs e)
    {
        if (_app.IsExiting || ForceClose)
        {
            return;
        }

        e.Cancel = true;
        _app.HideWidget();
    }
}
