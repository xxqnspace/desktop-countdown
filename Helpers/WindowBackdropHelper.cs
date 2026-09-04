using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace DesktopCountdown.Helpers;

/// <summary>
/// 系统背景类型（仅 Windows 11 支持 DWMWA_SYSTEMBACKDROP_TYPE，Windows 10 回退到 accent 模糊）。
/// </summary>
public enum SystemBackdropKind
{
    None,
    Mica,
    Acrylic,
    Tabbed
}

/// <summary>
/// 通过 DWM 原生接口为窗口启用真实的系统背景（亚克力 / 云母）。
/// 前提：窗口必须是普通窗口（AllowsTransparency = false），且 Background 不绘制内容，否则系统背景会被覆盖。
/// </summary>
public static class WindowBackdropHelper
{
    private const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;
    private const int DWMWA_SYSTEMBACKDROP_TYPE = 38;

    private const int DWMWCP_DEFAULT = 0;
    private const int DWMWCP_DONOTROUND = 1;
    private const int DWMWCP_ROUND = 2;
    private const int DWMWCP_ROUNDSMALL = 3;

    private const int DWMSBT_NONE = 1;
    private const int DWMSBT_MAINWINDOW = 2;
    private const int DWMSBT_TRANSIENTWINDOW = 3;
    private const int DWMSBT_TABBEDWINDOW = 4;

    private const int WCA_ACCENT_POLICY = 19;
    private const int ACCENT_ENABLE_BLURBEHIND = 3;
    private const int ACCENT_ENABLE_ACRYLICBLURBEHIND = 4;

    /// <summary>Windows 11（build 22000+）支持 DWMWA_SYSTEMBACKDROP_TYPE。</summary>
    public static bool IsWindows11 => Environment.OSVersion.Version.Build >= 22000;

    /// <summary>Windows 10 及以上均可尝试，Win10 走 accent 模糊回退。</summary>
    public static bool IsSupported => Environment.OSVersion.Version.Major >= 10;

    [DllImport("dwmapi.dll", PreserveSig = false)]
    private static extern void DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int valueSize);

    [DllImport("user32.dll")]
    private static extern int SetWindowCompositionAttribute(IntPtr hwnd, ref WindowCompositionAttributeData data);

    /// <summary>
    /// 为窗口应用系统背景。建议在 SourceInitialized 之后调用。
    /// </summary>
    /// <returns>是否应用成功；失败时调用方应回退为自绘背景，避免出现黑色窗口。</returns>
    public static bool Apply(Window window, SystemBackdropKind kind, string? tintColor = null, bool roundCorners = true)
    {
        if (kind == SystemBackdropKind.None)
        {
            return false;
        }

        var hwnd = new WindowInteropHelper(window).Handle;
        if (hwnd == IntPtr.Zero)
        {
            return false;
        }

        bool applied;
        if (IsWindows11)
        {
            var backdrop = kind switch
            {
                SystemBackdropKind.Mica => DWMSBT_MAINWINDOW,
                SystemBackdropKind.Tabbed => DWMSBT_TABBEDWINDOW,
                SystemBackdropKind.Acrylic => DWMSBT_TRANSIENTWINDOW,
                _ => DWMSBT_NONE
            };

            applied = TrySetAttribute(hwnd, DWMWA_SYSTEMBACKDROP_TYPE, backdrop);
            if (!applied)
            {
                applied = ApplyLegacyAcrylic(hwnd, tintColor);
            }
        }
        else
        {
            applied = ApplyLegacyAcrylic(hwnd, tintColor);
        }

        if (roundCorners)
        {
            TrySetAttribute(hwnd, DWMWA_WINDOW_CORNER_PREFERENCE, DWMWCP_ROUND);
        }

        return applied;
    }

    /// <summary>清除系统背景，恢复普通窗口外观。</summary>
    public static void Clear(Window window)
    {
        var hwnd = new WindowInteropHelper(window).Handle;
        if (hwnd == IntPtr.Zero)
        {
            return;
        }

        TrySetAttribute(hwnd, DWMWA_SYSTEMBACKDROP_TYPE, DWMSBT_NONE);
        TrySetAttribute(hwnd, DWMWA_WINDOW_CORNER_PREFERENCE, DWMWCP_DEFAULT);
    }

    private static bool TrySetAttribute(IntPtr hwnd, int attribute, int value)
    {
        try
        {
            DwmSetWindowAttribute(hwnd, attribute, ref value, 4);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>Windows 10 或 DWMWA_SYSTEMBACKDROP_TYPE 不可用时的回退方案。</summary>
    private static bool ApplyLegacyAcrylic(IntPtr hwnd, string? tintColor)
    {
        var fallback = System.Windows.Media.Color.FromArgb(0x99, 0x11, 0x18, 0x20);
        var color = ColorHelper.ColorFrom(tintColor, fallback);
        var abgr = (color.A << 24) | (color.B << 16) | (color.G << 8) | color.R;

        var accent = new AccentPolicy
        {
            AccentState = ACCENT_ENABLE_ACRYLICBLURBEHIND,
            AccentFlags = 2,
            GradientColor = abgr,
            AnimationId = 0
        };

        var size = Marshal.SizeOf<AccentPolicy>();
        IntPtr buffer = IntPtr.Zero;
        try
        {
            buffer = Marshal.AllocHGlobal(size);
            Marshal.StructureToPtr(accent, buffer, false);
            var data = new WindowCompositionAttributeData
            {
                Attribute = WCA_ACCENT_POLICY,
                Data = buffer,
                SizeOfData = size
            };
            return SetWindowCompositionAttribute(hwnd, ref data) == 0;
        }
        catch
        {
            // 回退失败时保持普通外观，不影响主流程。
            return false;
        }
        finally
        {
            if (buffer != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(buffer);
            }
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct WindowCompositionAttributeData
    {
        public int Attribute;
        public IntPtr Data;
        public int SizeOfData;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct AccentPolicy
    {
        public int AccentState;
        public int AccentFlags;
        public int GradientColor;
        public int AnimationId;
    }
}
