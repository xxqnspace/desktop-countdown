using System.ComponentModel;
using System.Globalization;
using System.Windows;
using DesktopCountdown.Models;

namespace DesktopCountdown;

public partial class MainWindow : Window
{
    private readonly App _app;

    public MainWindow(App app)
    {
        InitializeComponent();
        _app = app;
        InitializeTimeSelectors();
    }

    public void ApplyConfigToControls()
    {
        var config = _app.Config;
        var targetTime = config.Countdown.TargetDateTime.LocalDateTime;
        TitleBox.Text = config.Countdown.Title;
        TargetDatePicker.SelectedDate = targetTime.Date;
        TargetHourBox.SelectedItem = targetTime.Hour.ToString("00", CultureInfo.InvariantCulture);
        TargetMinuteBox.SelectedItem = targetTime.Minute.ToString("00", CultureInfo.InvariantCulture);
        TargetSecondBox.SelectedItem = targetTime.Second.ToString("00", CultureInfo.InvariantCulture);
        EndTextBox.Text = config.Countdown.EndText;

        ShowDaysBox.IsChecked = config.DisplayUnits.ShowDays;
        ShowHoursBox.IsChecked = config.DisplayUnits.ShowHours;
        ShowMinutesBox.IsChecked = config.DisplayUnits.ShowMinutes;
        ShowSecondsBox.IsChecked = config.DisplayUnits.ShowSeconds;
        CeilingDaysBox.IsChecked = config.DisplayUnits.DayRounding == DayRounding.Ceiling;

        BackgroundModeBox.SelectedIndex = config.Appearance.BackgroundMode switch
        {
            BackgroundMode.Solid => 1,
            BackgroundMode.Gradient => 2,
            BackgroundMode.Image => 3,
            _ => 0
        };
        BackgroundColorBox.Text = config.Appearance.BackgroundColor;
        TextColorBox.Text = config.Appearance.TextColor;
        OpacitySlider.Value = config.Appearance.Opacity;
        BackgroundImageBox.Text = config.Appearance.BackgroundImagePath ?? string.Empty;

        TopmostBox.IsChecked = config.Window.Topmost;
        LockedBox.IsChecked = config.Window.Locked;
        AutoStartBox.IsChecked = config.Behavior.AutoStart;
        NotifyOnEndBox.IsChecked = config.Countdown.NotifyOnEnd;
        StatusText.Text = string.Empty;
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            ApplyControlsToConfig();
            _app.ApplyConfigChanges();
            StatusText.Text = $"已保存：{DateTime.Now:HH:mm:ss}";
        }
        catch (Exception ex)
        {
            StatusText.Text = ex.Message;
            System.Windows.MessageBox.Show(ex.Message, "保存失败", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void ApplyControlsToConfig()
    {
        var config = _app.Config;
        var date = TargetDatePicker.SelectedDate ?? DateTime.Today;
        var hour = ParseSelectedNumber(TargetHourBox.SelectedItem);
        var minute = ParseSelectedNumber(TargetMinuteBox.SelectedItem);
        var second = ParseSelectedNumber(TargetSecondBox.SelectedItem);

        var localTarget = date.Date.AddHours(hour).AddMinutes(minute).AddSeconds(second);
        config.Countdown.Title = string.IsNullOrWhiteSpace(TitleBox.Text) ? "重要倒计时" : TitleBox.Text.Trim();
        config.Countdown.TargetDateTime = new DateTimeOffset(localTarget);
        config.Countdown.EndText = string.IsNullOrWhiteSpace(EndTextBox.Text) ? "已结束" : EndTextBox.Text.Trim();
        config.Countdown.NotifyOnEnd = NotifyOnEndBox.IsChecked == true;

        var showDays = ShowDaysBox.IsChecked == true;
        var showHours = ShowHoursBox.IsChecked == true;
        var showMinutes = ShowMinutesBox.IsChecked == true;
        var showSeconds = ShowSecondsBox.IsChecked == true;
        if (!showDays && !showHours && !showMinutes && !showSeconds)
        {
            showDays = true;
            ShowDaysBox.IsChecked = true;
        }

        config.DisplayUnits.ShowDays = showDays;
        config.DisplayUnits.ShowHours = showHours;
        config.DisplayUnits.ShowMinutes = showMinutes;
        config.DisplayUnits.ShowSeconds = showSeconds;
        config.DisplayUnits.DayRounding = CeilingDaysBox.IsChecked == true ? DayRounding.Ceiling : DayRounding.Floor;

        config.Appearance.BackgroundMode = BackgroundModeBox.SelectedIndex switch
        {
            1 => BackgroundMode.Solid,
            2 => BackgroundMode.Gradient,
            3 => BackgroundMode.Image,
            _ => BackgroundMode.LiquidGlass
        };
        config.Appearance.BackgroundColor = NormalizeColor(BackgroundColorBox.Text, "#CCFFFFFF");
        config.Appearance.TextColor = NormalizeColor(TextColorBox.Text, "#FFFFFFFF");
        config.Appearance.Opacity = Math.Clamp(OpacitySlider.Value, 0.2, 1);
        config.Appearance.BackgroundImagePath = string.IsNullOrWhiteSpace(BackgroundImageBox.Text) ? null : BackgroundImageBox.Text.Trim();

        config.Window.Topmost = TopmostBox.IsChecked == true;
        config.Window.Locked = LockedBox.IsChecked == true;

        var autoStart = AutoStartBox.IsChecked == true;
        if (autoStart != config.Behavior.AutoStart)
        {
            _app.SetAutoStart(autoStart);
        }
    }

    private static string NormalizeColor(string value, string fallback)
    {
        value = value.Trim();
        if (value.StartsWith('#') && (value.Length == 7 || value.Length == 9))
        {
            return value;
        }

        return fallback;
    }

    private void InitializeTimeSelectors()
    {
        TargetHourBox.ItemsSource = Enumerable.Range(0, 24).Select(x => x.ToString("00", CultureInfo.InvariantCulture)).ToList();
        TargetMinuteBox.ItemsSource = Enumerable.Range(0, 60).Select(x => x.ToString("00", CultureInfo.InvariantCulture)).ToList();
        TargetSecondBox.ItemsSource = Enumerable.Range(0, 60).Select(x => x.ToString("00", CultureInfo.InvariantCulture)).ToList();
    }

    private static int ParseSelectedNumber(object? selectedItem)
    {
        return int.TryParse(selectedItem?.ToString(), CultureInfo.InvariantCulture, out var value) ? value : 0;
    }

    private void ChooseImageButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Filter = "图片文件|*.png;*.jpg;*.jpeg;*.bmp;*.webp|所有文件|*.*"
        };

        if (dialog.ShowDialog(this) == true)
        {
            BackgroundImageBox.Text = dialog.FileName;
            BackgroundModeBox.SelectedIndex = 3;
        }
    }

    private void HideWidgetButton_Click(object sender, RoutedEventArgs e)
    {
        _app.HideWidget();
    }

    private void ExitButton_Click(object sender, RoutedEventArgs e)
    {
        _app.ExitApplication();
    }

    private void Window_Closing(object sender, CancelEventArgs e)
    {
        if (_app.IsExiting)
        {
            return;
        }

        e.Cancel = true;
        Hide();
    }
}
