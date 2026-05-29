namespace DesktopCountdown.Models;

public sealed class AppConfig
{
    public int SchemaVersion { get; set; } = 1;
    public bool IsFirstRun { get; set; } = true;
    public CountdownConfig Countdown { get; set; } = new();
    public DisplayUnitConfig DisplayUnits { get; set; } = new();
    public AppearanceConfig Appearance { get; set; } = new();
    public WindowStateConfig Window { get; set; } = new();
    public BehaviorConfig Behavior { get; set; } = new();
}
