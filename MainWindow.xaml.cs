using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using DesktopCountdown.Models;

// UseWindowsForms 会隐式引入 System.Windows.Forms，以下别名用于消除 Button 的二义性。
using Button = System.Windows.Controls.Button;

namespace DesktopCountdown;

public partial class MainWindow : Window
{
    private readonly App _app;
    private readonly ObservableCollection<LessonPeriod> _lessonItems = new();
    private string _lessonWarning = string.Empty;

    public MainWindow(App app)
    {
        InitializeComponent();
        _app = app;
        InitializeTimeSelectors();
        LessonList.ItemsSource = _lessonItems;
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
            BackgroundMode.Acrylic => 4,
            _ => 0
        };
        BackgroundColorBox.Text = config.Appearance.BackgroundColor;
        TextColorBox.Text = config.Appearance.TextColor;
        AcrylicTintBox.Text = config.Appearance.AcrylicTintColor;
        OpacitySlider.Value = config.Appearance.Opacity;
        BackgroundImageBox.Text = config.Appearance.BackgroundImagePath ?? string.Empty;

        TopmostBox.IsChecked = config.Window.Topmost;
        LockedBox.IsChecked = config.Window.Locked;
        AutoStartBox.IsChecked = config.Behavior.AutoStart;
        NotifyOnEndBox.IsChecked = config.Countdown.NotifyOnEnd;

        LessonEnabledBox.IsChecked = config.Schedule.Enabled;
        LessonFollowBox.IsChecked = config.Schedule.FollowWidget;
        LessonTextColorBox.Text = config.Schedule.TextColor;
        LessonTintBox.Text = config.Schedule.BackgroundTint;
        LessonIdleBox.Text = config.Schedule.IdleText;
        ReloadLessonItems(config.Schedule.Lessons);

        StatusText.Text = string.Empty;
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            _lessonWarning = string.Empty;
            ApplyControlsToConfig();
            _app.ApplyConfigChanges();
            StatusText.Text = $"已保存：{DateTime.Now:HH:mm:ss}" +
                              (string.IsNullOrEmpty(_lessonWarning) ? string.Empty : $"（{_lessonWarning}）");
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
            4 => BackgroundMode.Acrylic,
            _ => BackgroundMode.LiquidGlass
        };
        config.Appearance.BackgroundColor = NormalizeColor(BackgroundColorBox.Text, "#CCFFFFFF");
        config.Appearance.TextColor = NormalizeColor(TextColorBox.Text, "#FFFFFFFF");
        config.Appearance.AcrylicTintColor = NormalizeColor(AcrylicTintBox.Text, "#66111820");
        config.Appearance.Opacity = Math.Clamp(OpacitySlider.Value, 0.2, 1);
        config.Appearance.BackgroundImagePath = string.IsNullOrWhiteSpace(BackgroundImageBox.Text) ? null : BackgroundImageBox.Text.Trim();

        config.Window.Topmost = TopmostBox.IsChecked == true;
        config.Window.Locked = LockedBox.IsChecked == true;

        ApplyLessonControlsToConfig();

        var autoStart = AutoStartBox.IsChecked == true;
        if (autoStart != config.Behavior.AutoStart)
        {
            _app.SetAutoStart(autoStart);
        }
    }

    /// <summary>把课程提示设置写回配置，并校验时间格式。</summary>
    private void ApplyLessonControlsToConfig()
    {
        var schedule = _app.Config.Schedule;
        schedule.Enabled = LessonEnabledBox.IsChecked == true;
        schedule.FollowWidget = LessonFollowBox.IsChecked == true;
        schedule.TextColor = NormalizeColor(LessonTextColorBox.Text, "#FF000000");
        schedule.BackgroundTint = NormalizeColor(LessonTintBox.Text, "#CCFFFFFF");
        schedule.IdleText = string.IsNullOrWhiteSpace(LessonIdleBox.Text) ? "放学啦，注意休息" : LessonIdleBox.Text.Trim();

        var lessons = new List<LessonPeriod>();
        var invalidCount = 0;

        foreach (var item in _lessonItems)
        {
            var start = NormalizeTime(item.StartText);
            var end = NormalizeTime(item.EndText);
            if (start is null || end is null)
            {
                invalidCount++;
                continue;
            }

            var name = string.IsNullOrWhiteSpace(item.Name) ? $"第{ToChineseNumber(lessons.Count + 1)}节" : item.Name.Trim();
            lessons.Add(new LessonPeriod { Name = name, StartText = start, EndText = end });
        }

        if (invalidCount > 0)
        {
            _lessonWarning = $"已忽略 {invalidCount} 行时间格式错误";
        }

        if (lessons.Count == 0)
        {
            // 全部无效时保留原有课表，避免课程窗口突然失效。
            return;
        }

        lessons.Sort((a, b) => string.CompareOrdinal(a.StartText, b.StartText));
        schedule.Lessons = lessons;
    }

    private void ReloadLessonItems(IEnumerable<LessonPeriod> lessons)
    {
        _lessonItems.Clear();
        foreach (var lesson in lessons)
        {
            _lessonItems.Add(lesson.Clone());
        }
    }

    private static string? NormalizeTime(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var parts = value.Trim().Replace('：', ':').Split(':');
        if (parts.Length < 2)
        {
            return null;
        }

        if (!int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var hour) ||
            !int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var minute))
        {
            return null;
        }

        if (hour is < 0 or > 23 || minute is < 0 or > 59)
        {
            return null;
        }

        return $"{hour:00}:{minute:00}";
    }

    private static string ToChineseNumber(int value)
    {
        var digits = new[] { "零", "一", "二", "三", "四", "五", "六", "七", "八", "九" };

        if (value < 10)
        {
            return digits[value];
        }

        if (value == 10)
        {
            return "十";
        }

        if (value < 20)
        {
            return "十" + digits[value - 10];
        }

        if (value < 100)
        {
            var tens = value / 10;
            var ones = value % 10;
            return digits[tens] + "十" + (ones == 0 ? string.Empty : digits[ones]);
        }

        return value.ToString(CultureInfo.InvariantCulture);
    }

    private void AddLessonButton_Click(object sender, RoutedEventArgs e)
    {
        var last = _lessonItems.Count > 0 ? _lessonItems[^1] : null;
        _lessonItems.Add(new LessonPeriod
        {
            Name = $"第{ToChineseNumber(_lessonItems.Count + 1)}节",
            StartText = last?.EndText ?? "08:00",
            EndText = last?.EndText ?? "08:45"
        });
    }

    private void RemoveLessonButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: LessonPeriod lesson })
        {
            _lessonItems.Remove(lesson);
        }
    }

    private void ResetLessonButton_Click(object sender, RoutedEventArgs e)
    {
        ReloadLessonItems(ScheduleConfig.CreateDefaultLessons());
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
