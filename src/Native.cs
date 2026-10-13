using System;
using System.Runtime.InteropServices;

namespace SoundDrought;

internal static partial class Native
{
    private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
    private const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;
    private const int DWMWCP_ROUND = 2;

    [LibraryImport("dwmapi.dll")]
    private static partial int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

    public static void UseDarkTitleBar(IntPtr hwnd)
    {
        int enabled = 1;
        DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, ref enabled, sizeof(int));
    }

    private const int SM_CXSIZEFRAME = 32;
    private const int SM_CXPADDEDBORDER = 92;

    [LibraryImport("user32.dll")]
    private static partial int GetSystemMetricsForDpi(int index, uint dpi);

    [LibraryImport("user32.dll")]
    private static partial uint GetDpiForWindow(IntPtr hwnd);

    /// <summary>How far a maximized window without a system frame reaches past the screen edge, in WPF units.</summary>
    public static System.Windows.Thickness MaximizedFrameThickness(IntPtr hwnd)
    {
        uint dpi = GetDpiForWindow(hwnd);
        if (dpi == 0) dpi = 96;
        double frame = GetSystemMetricsForDpi(SM_CXSIZEFRAME, dpi) + GetSystemMetricsForDpi(SM_CXPADDEDBORDER, dpi);
        return new System.Windows.Thickness(frame * 96.0 / dpi);
    }

    /// <summary>Rounded corners for borderless windows (Windows 11; ignored on Windows 10).</summary>
    public static void UseRoundedCorners(IntPtr hwnd)
    {
        int preference = DWMWCP_ROUND;
        DwmSetWindowAttribute(hwnd, DWMWA_WINDOW_CORNER_PREFERENCE, ref preference, sizeof(int));
    }
}
