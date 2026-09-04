using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using DesktopCountdown.Models;

// UseWindowsForms 会隐式引入 System.Drawing，以下别名用于消除 Color / Brush 的二义性。
using Color = System.Windows.Media.Color;
using Brush = System.Windows.Media.Brush;

namespace DesktopCountdown.Helpers;

/// <summary>
/// 统一生成悬浮窗背景画刷，倒计时窗口与课程窗口共用。
/// </summary>
public static class AppearanceBrushFactory
{
    /// <summary>
    /// 创建内容背景画刷。
    /// </summary>
    /// <param name="appearance">外观配置。</param>
    /// <param name="systemBackdrop">是否使用系统亚克力背景（此时仅绘制一层着色，避免遮挡系统模糊层）。</param>
    public static System.Windows.Media.Brush Create(AppearanceConfig appearance, bool systemBackdrop)
    {
        if (systemBackdrop)
        {
            return CreateAcrylicTint(appearance);
        }

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
            return ColorHelper.BrushFrom(appearance.BackgroundColor, new SolidColorBrush(Color.FromArgb(210, 255, 255, 255)));
        }

        if (appearance.BackgroundMode == BackgroundMode.Gradient)
        {
            return new LinearGradientBrush(
                ColorHelper.ColorFrom(appearance.BackgroundColor, Color.FromArgb(210, 255, 255, 255)),
                ColorHelper.ColorFrom(appearance.AccentColor, Color.FromArgb(180, 124, 183, 255)),
                35);
        }

        // 模拟玻璃（液态玻璃）：三段式渐变，兼容 Windows 10 与纯透明窗口场景。
        var brush = new LinearGradientBrush { StartPoint = new System.Windows.Point(0, 0), EndPoint = new System.Windows.Point(1, 1) };
        brush.GradientStops.Add(new GradientStop(Color.FromArgb(210, 255, 255, 255), 0));
        brush.GradientStops.Add(new GradientStop(Color.FromArgb(120, 124, 183, 255), 0.48));
        brush.GradientStops.Add(new GradientStop(Color.FromArgb(170, 255, 255, 255), 1));
        return brush;
    }

    /// <summary>
    /// 系统亚克力之上叠加的着色层。透明度上限 0x99，保证文字可读的同时保留模糊质感。
    /// </summary>
    public static System.Windows.Media.Brush CreateAcrylicTint(AppearanceConfig appearance)
    {
        var color = ColorHelper.ColorFrom(appearance.AcrylicTintColor, Color.FromArgb(0x66, 17, 24, 32));
        color.A = Math.Min(color.A, (byte)0x99);
        return new SolidColorBrush(color);
    }
}
