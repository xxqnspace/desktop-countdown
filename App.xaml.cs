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
        Config.Behavior.AutoStart = _autoStartService.IsEnabled();

        CreateTrayIcon();
        ShowWidget();

        var isAutoStart = e.Args.Any(arg => arg.Equals("--autostart", StringComparison.OrdinalIgnoreCase));
        if (Config.IsFirstRun || !isAutoStart)
        {
            ShowSettings();
        }
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
    }

    public void HideWidget()
    {
        Config.Window.Visible = false;
        _widgetWindow?.Hide();
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
        }

        SaveConfig();
        RefreshTrayMenu();
    }

    public void ApplyConfigChanges()
    {
        Config.IsFirstRun = false;
        _widgetWindow?.ApplyConfig();
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
