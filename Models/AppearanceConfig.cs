using System.Text.Json.Serialization;

namespace DesktopCountdown.Models;

public sealed class AppearanceConfig
{
    public BackgroundMode BackgroundMode { get; set; } = BackgroundMode.LiquidGlass;
    public string BackgroundColor { get; set; } = "#CCFFFFFF";
    public string AccentColor { get; set; } = "#7CB7FF";
    public double Opacity { get; set; } = 0.86;
    public double BlurRadius { get; set; } = 24;
    public double CornerRadius { get; set; } = 18;
    public bool BorderEnabled { get; set; } = true;
    public string BorderColor { get; set; } = "#66FFFFFF";
    public string? BackgroundImagePath { get; set; }
    public ImageStretchMode ImageStretch { get; set; } = ImageStretchMode.UniformToFill;
    public string FontFamily { get; set; } = "Segoe UI";
    public string TextColor { get; set; } = "#FFFFFFFF";
}

[JsonConverter(typeof(JsonStringEnumConverter<BackgroundMode>))]
public enum BackgroundMode
{
    LiquidGlass,
    Solid,
    Gradient,
    Image
}

[JsonConverter(typeof(JsonStringEnumConverter<ImageStretchMode>))]
public enum ImageStretchMode
{
    UniformToFill,
    Uniform,
    Fill
}
