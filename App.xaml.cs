using System.Windows;
using DesktopCountdown.Models;
using DesktopCountdown.Services;
using Forms = System.Windows.Forms;

namespace DesktopCountdown;

public partial class App : System.Windows.Application
{
    private SingleInstanceService? _singleInstance;
    private ConfigService _configService = null!;
    private AutoStartService _autoStartService = null!;
    private Forms.NotifyIcon? _notifyIcon;
    private MainWindow? _settingsWindow;
    private WidgetWindow? _widgetWindow;
    private LessonWindow? _lessonWindow;

    public AppConfig Config { get; private set; } = new();
    public bool IsExiting { get; private set; }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        ShutdownMode = ShutdownMode.OnExplicitShutdown;
        _singleInstance = new SingleInstanceService();
        if (!_singleInstance.IsFirstInstance)
        {
            Shutdown();
            return;
        }

        _configService = new ConfigService();
        _autoStartService = new AutoStartService();
        Config = _configService.Load();
        MigrateConfig(Config);
        Config.Behavior.AutoStart = _autoStartService.IsEnabled();

        CreateTrayIcon();
        ShowWidget();

        var isAutoStart = e.Args.Any(arg => arg.Equals("--autostart", StringComparison.OrdinalIgnoreCase));
        if (Config.IsFirstRun || !isAutoStart)
        {
            ShowSettings();
        }
    }

    /// <summary>
    /// 配置升级：SchemaVersion 1 → 2 时，把旧的「液态玻璃」升级为真实的系统亚克力。
    /// 液态玻璃仍作为兼容性回退方案保留，可在设置中手动选择。
    /// </summary>
    private static void MigrateConfig(AppConfig config)
    {
        if (config.SchemaVersion >= 2)
        {
            return;
        }

        if (config.Appearance.BackgroundMode == BackgroundMode.LiquidGlass)
        {
            config.Appearance.BackgroundMode = BackgroundMode.Acrylic;
        }

        config.SchemaVersion = 2;
    }

    public void ShowSettings()
    {
        _settingsWindow ??= new MainWindow(this);
        _settingsWindow.ApplyConfigToControls();
        _settingsWindow.Show();
        _settingsWindow.Activate();
    }

    public void ShowWidget()
    {
        _widgetWindow ??= new WidgetWindow(this);
        _widgetWindow.ApplyConfig();
        if (Config.Window.Visible)
        {
            _widgetWindow.Show();
        }

        UpdateLessonWindowVisibility();
        SyncLessonWindowPosition();
    }

    public void HideWidget()
    {
        Config.Window.Visible = false;
        _widgetWindow?.Hide();
        _lessonWindow?.Hide();
        SaveConfig();
        RefreshTrayMenu();
    }

    public void ToggleWidgetVisibility()
    {
        Config.Window.Visible = !Config.Window.Visible;
        if (Config.Window.Visible)
        {
            ShowWidget();
        }
        else
        {
            _widgetWindow?.Hide();
            _lessonWindow?.Hide();
        }

        SaveConfig();
        RefreshTrayMenu();
    }

    /// <summary>隐藏课程提示窗口（关闭开关）。</summary>
    public void HideLesson()
    {
        Config.Schedule.Enabled = false;
        _lessonWindow?.Hide();
        SaveConfig();
        RefreshTrayMenu();
    }

    /// <summary>切换课程提示窗口显示状态。</summary>
    public void ToggleLesson()
    {
        Config.Schedule.Enabled = !Config.Schedule.Enabled;
        UpdateLessonWindowVisibility();
        SaveConfig();
        RefreshTrayMenu();
    }

    /// <summary>按配置显示或隐藏课程窗口（与悬浮窗可见性联动）。</summary>
    public void UpdateLessonWindowVisibility()
    {
        if (!Config.Schedule.Enabled || !Config.Window.Visible)
        {
            _lessonWindow?.Hide();
            return;
        }

        _lessonWindow ??= new LessonWindow(this);
        _lessonWindow.ApplyConfig();
        _lessonWindow.Show();
        SyncLessonWindowPosition();
    }

    /// <summary>吸附模式下让课程窗口跟随倒计时窗口的位置与宽度。</summary>
    public void SyncLessonWindowPosition()
    {
        if (_lessonWindow is null || _widgetWindow is null)
        {
            return;
        }

        if (!Config.Schedule.Enabled || !Config.Schedule.FollowWidget)
        {
            return;
        }

        if (!_lessonWindow.IsVisible || !_widgetWindow.IsVisible)
        {
            return;
        }

        _lessonWindow.FollowTo(_widgetWindow);
    }

    /// <summary>背景模式在「系统亚克力 / 自绘」之间切换时，需要重建悬浮窗（AllowsTransparency 不可运行时修改）。</summary>
    public void RecreateWidget()
    {
        var previous = _widgetWindow;
        _widgetWindow = null;
        ShowWidget();

        if (previous is not null)
        {
            previous.ForceClose = true;
            previous.Close();
        }
    }

    /// <summary>同上，重建课程提示窗口。</summary>
    public void RecreateLessonWindow()
    {
        var previous = _lessonWindow;
        _lessonWindow = null;
        UpdateLessonWindowVisibility();

        if (previous is not null)
        {
            previous.ForceClose = true;
            previous.Close();
        }
    }

    public void ApplyConfigChanges()
    {
        Config.IsFirstRun = false;
        _widgetWindow?.ApplyConfig();
        UpdateLessonWindowVisibility();
        SyncLessonWindowPosition();
        SaveConfig();
        RefreshTrayMenu();
    }

    public void SetAutoStart(bool enabled)
    {
        _autoStartService.SetEnabled(enabled);
        Config.Behavior.AutoStart = enabled;
        SaveConfig();
        RefreshTrayMenu();
    }

    public void SaveConfig()
    {
        try
        {
            _configService.Save(Config);
        }
        catch (Exception ex)
        {
            Forms.MessageBox.Show($"配置保存失败：{ex.Message}", "Desktop Countdown", Forms.MessageBoxButtons.OK, Forms.MessageBoxIcon.Warning);
        }
    }

    public void ExitApplication()
    {
        IsExiting = true;
        if (_widgetWindow is not null)
        {
            Config.Window.Left = _widgetWindow.Left;
            Config.Window.Top = _widgetWindow.Top;
            Config.Window.Width = _widgetWindow.Width;
            Config.Window.Height = _widgetWindow.Height;
        }

        if (_lessonWindow is not null && !Config.Schedule.FollowWidget)
        {
            Config.Schedule.Window.Left = _lessonWindow.Left;
            Config.Schedule.Window.Top = _lessonWindow.Top;
            Config.Schedule.Window.Width = _lessonWindow.Width;
            Config.Schedule.Window.Height = _lessonWindow.Height;
        }

        SaveConfig();
        _notifyIcon?.Dispose();
        Shutdown();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _notifyIcon?.Dispose();
        _singleInstance?.Dispose();
        base.OnExit(e);
    }

    private void CreateTrayIcon()
    {
        var iconPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "app.ico");
        System.Drawing.Icon? trayIcon = null;
        if (System.IO.File.Exists(iconPath))
        {
            try { trayIcon = new System.Drawing.Icon(iconPath); } catch { }
        }

        _notifyIcon = new Forms.NotifyIcon
        {
            Text = "桌面倒计时",
            Icon = trayIcon ?? System.Drawing.SystemIcons.Application,
            Visible = true
        };
        _notifyIcon.DoubleClick += (_, _) => Dispatcher.Invoke(ShowSettings);
        RefreshTrayMenu();
    }

    private void RefreshTrayMenu()
    {
        if (_notifyIcon is null)
        {
            return;
        }

        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add(Config.Window.Visible ? "隐藏悬浮窗" : "显示悬浮窗", null, (_, _) => Dispatcher.Invoke(ToggleWidgetVisibility));
        menu.Items.Add("打开设置", null, (_, _) => Dispatcher.Invoke(ShowSettings));

        var lessonItem = new Forms.ToolStripMenuItem("课程提示窗口")
        {
            Checked = Config.Schedule.Enabled,
            CheckOnClick = true,
            Enabled = Config.Window.Visible
        };
        lessonItem.CheckedChanged += (_, _) => Dispatcher.Invoke(() =>
        {
            if (lessonItem.Checked == Config.Schedule.Enabled)
            {
                return;
            }

            ToggleLesson();
        });
        menu.Items.Add(lessonItem);

        var topmostItem = new Forms.ToolStripMenuItem("始终置顶")
        {
            Checked = Config.Window.Topmost,
            CheckOnClick = true
        };
        topmostItem.CheckedChanged += (_, _) => Dispatcher.Invoke(() =>
        {
            Config.Window.Topmost = topmostItem.Checked;
            ApplyConfigChanges();
        });
        menu.Items.Add(topmostItem);

        var lockedItem = new Forms.ToolStripMenuItem("锁定位置")
        {
            Checked = Config.Window.Locked,
            CheckOnClick = true
        };
        lockedItem.CheckedChanged += (_, _) => Dispatcher.Invoke(() =>
        {
            Config.Window.Locked = lockedItem.Checked;
            ApplyConfigChanges();
        });
        menu.Items.Add(lockedItem);

        var autoStartItem = new Forms.ToolStripMenuItem("开机自启")
        {
            Checked = Config.Behavior.AutoStart,
            CheckOnClick = true
        };
        autoStartItem.CheckedChanged += (_, _) => Dispatcher.Invoke(() =>
        {
            try
            {
                SetAutoStart(autoStartItem.Checked);
            }
            catch (Exception ex)
            {
                autoStartItem.Checked = Config.Behavior.AutoStart;
                Forms.MessageBox.Show($"开机自启设置失败：{ex.Message}", "Desktop Countdown", Forms.MessageBoxButtons.OK, Forms.MessageBoxIcon.Warning);
            }
        });
        menu.Items.Add(autoStartItem);

        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add("退出", null, (_, _) => Dispatcher.Invoke(ExitApplication));
        _notifyIcon.ContextMenuStrip = menu;
    }
}
