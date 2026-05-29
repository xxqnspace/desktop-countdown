using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
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
    private bool _hasNotifiedEnd;
    private readonly ObservableCollection<CountdownSegment> _countdownSegments = new();

    public WidgetWindow(App app)
    {
        InitializeComponent();
        _app = app;
        CountdownItems.ItemsSource = _countdownSegments;
        _timer.Tick += (_, _) => RefreshCountdown();
        _timer.Start();

        ContextMenu = BuildContextMenu();
    }

    private const double BaseWidth = 300.0;
    private const double BaseHeight = 150.0;

    public void ApplyConfig()
    {
        var config = _app.Config;
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
        RootBorder.Background = CreateBackgroundBrush(config.Appearance);
        Opacity = config.Appearance.Opacity;

        RefreshCountdown();
        UpdateFontSizes();
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

    private System.Windows.Media.Brush CreateBackgroundBrush(AppearanceConfig appearance)
    {
        if (appearance.BackgroundMode == BackgroundMode.Image &&
            !string.IsNullOrWhiteSpace(appearance.BackgroundImagePath) &&
            File.Exists(appearance.BackgroundImagePath))
        {
            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.UriSource = new Uri(appearance.BackgroundImagePath);
            image.EndInit();
            return new ImageBrush(image)
            {
                Stretch = appearance.ImageStretch switch
                {
                    ImageStretchMode.Fill => Stretch.Fill,
                    ImageStretchMode.Uniform => Stretch.Uniform,
                    _ => Stretch.UniformToFill
                }
            };
        }

        if (appearance.BackgroundMode == BackgroundMode.Solid)
        {
            return ColorHelper.BrushFrom(appearance.BackgroundColor, new SolidColorBrush(System.Windows.Media.Color.FromArgb(210, 255, 255, 255)));
        }

        if (appearance.BackgroundMode == BackgroundMode.Gradient)
        {
            return new LinearGradientBrush(
                ColorHelper.ColorFrom(appearance.BackgroundColor, System.Windows.Media.Color.FromArgb(210, 255, 255, 255)),
                ColorHelper.ColorFrom(appearance.AccentColor, System.Windows.Media.Color.FromArgb(180, 124, 183, 255)),
                35);
        }

        var brush = new LinearGradientBrush { StartPoint = new System.Windows.Point(0, 0), EndPoint = new System.Windows.Point(1, 1) };
        brush.GradientStops.Add(new GradientStop(System.Windows.Media.Color.FromArgb(210, 255, 255, 255), 0));
        brush.GradientStops.Add(new GradientStop(System.Windows.Media.Color.FromArgb(120, 124, 183, 255), 0.48));
        brush.GradientStops.Add(new GradientStop(System.Windows.Media.Color.FromArgb(170, 255, 255, 255), 1));
        return brush;
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
        _app.SaveConfig();
    }

    private void Window_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        _app.Config.Window.Width = Width;
        _app.Config.Window.Height = Height;
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
        if (_app.IsExiting)
        {
            return;
        }

        e.Cancel = true;
        _app.HideWidget();
    }
}
