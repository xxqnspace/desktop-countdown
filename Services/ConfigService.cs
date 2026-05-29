using System.IO;
using System.Text.Json;
using DesktopCountdown.Models;

namespace DesktopCountdown.Services;

public sealed class ConfigService
{
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    public string ConfigPath { get; }

    public ConfigService()
    {
        ConfigPath = ResolveConfigPath();
    }

    public AppConfig Load()
    {
        if (!File.Exists(ConfigPath))
        {
            return new AppConfig();
        }

        try
        {
            var json = File.ReadAllText(ConfigPath);
            return JsonSerializer.Deserialize<AppConfig>(json, _jsonOptions) ?? new AppConfig();
        }
        catch
        {
            TryBackupBrokenConfig();
            return new AppConfig();
        }
    }

    public void Save(AppConfig config)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(ConfigPath)!);
        var json = JsonSerializer.Serialize(config, _jsonOptions);
        var tempPath = ConfigPath + ".tmp";
        File.WriteAllText(tempPath, json);
        File.Move(tempPath, ConfigPath, true);
    }

    private static string ResolveConfigPath()
    {
        var appDirectory = AppContext.BaseDirectory;
        var appConfigPath = Path.Combine(appDirectory, "config.json");

        try
        {
            Directory.CreateDirectory(appDirectory);
            var probe = Path.Combine(appDirectory, ".write-test");
            File.WriteAllText(probe, "ok");
            File.Delete(probe);
            return appConfigPath;
        }
        catch
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            return Path.Combine(appData, "DesktopCountdown", "config.json");
        }
    }

    private void TryBackupBrokenConfig()
    {
        try
        {
            var backupPath = Path.Combine(
                Path.GetDirectoryName(ConfigPath)!,
                $"config.broken.{DateTime.Now:yyyyMMdd-HHmmss}.json");
            File.Copy(ConfigPath, backupPath, true);
        }
        catch
        {
            // Best effort only.
        }
    }
}
